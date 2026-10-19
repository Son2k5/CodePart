import asyncio
import hashlib
import math
import os
import re
from urllib.parse import urlsplit
from uuid import NAMESPACE_URL, uuid5
import httpx
from app.rag.chunking import markdown_chunks, pdf_pages
from app.rag.contracts import Passage


class RagService:
    def __init__(self, http):
        self.http = http
        self.base = os.getenv('QDRANT_URL', 'http://qdrant:6333').rstrip('/')
        self.model = os.getenv('EMBEDDING_MODEL', '')
        self.dimensions = int(os.getenv('EMBEDDING_DIMENSIONS', '1024'))
        self.embedding_url = os.getenv('EMBEDDING_BASE_URL', '').rstrip('/')
        identity = self.embedding_url + '|' + self.model + '|' + str(self.dimensions) + '|chunks-v1'
        self.collection = 'codepath_learning_' + hashlib.sha256(identity.encode()).hexdigest()[:16]
        self.headers = {'api-key': os.getenv('QDRANT_API_KEY', '')}
        self.ready = False
        self.lock = asyncio.Lock()
        self.min_score = float(os.getenv('RAG_MIN_SCORE', '0.35'))

    async def qdrant(self, method, path, **kwargs):
        r = await self.http.request(method, self.base + path, headers=self.headers, **kwargs)
        r.raise_for_status()
        return r.json()

    async def ensure_collection(self):
        async with self.lock:
            if self.ready:
                return
            path = '/collections/' + self.collection
            r = await self.http.get(self.base + path, headers=self.headers)
            if r.status_code == 404:
                created = await self.http.put(self.base + path, headers=self.headers,
                    json={'vectors': {'size': self.dimensions, 'distance': 'Cosine'}})
                if created.status_code not in (200, 409):
                    created.raise_for_status()
            else:
                r.raise_for_status()
            for field in ('topicId', 'revision'):
                await self.qdrant('PUT', path + '/index?wait=true', json={'field_name': field, 'field_schema': 'keyword'})
            self.ready = True

    async def embed(self, texts):
        if not self.embedding_url or not self.model:
            raise RuntimeError('EMBEDDING_NOT_CONFIGURED')
        response = await self.http.post(self.embedding_url + '/embeddings',
            headers={'Authorization': 'Bearer ' + os.getenv('EMBEDDING_API_KEY', '')},
            json={'model': self.model, 'input': texts}, timeout=45)
        response.raise_for_status()
        rows = sorted(response.json()['data'], key=lambda x: x['index'])
        if [x['index'] for x in rows] != list(range(len(texts))):
            raise ValueError('INVALID_EMBEDDING_RESPONSE')
        vectors = [x['embedding'] for x in rows]
        if any(len(v) != self.dimensions or not all(math.isfinite(x) for x in v) for v in vectors):
            raise ValueError('INVALID_EMBEDDING_DIMENSIONS')
        return vectors

    async def download_pdf(self, url):
        uri = urlsplit(url or '')
        hosts = {x.strip().lower() for x in os.getenv('RAG_ASSET_HOSTS', 'res.cloudinary.com,api.cloudinary.com').split(',') if x.strip()}
        if uri.scheme != 'https' or uri.hostname not in hosts or uri.username or uri.password or uri.port not in (None, 443):
            raise ValueError('ASSET_HOST_NOT_ALLOWED')
        # A separate request has no service/provider credentials and never follows redirects.
        async with self.http.stream('GET', url, follow_redirects=False, timeout=45) as r:
            if r.status_code >= 400:
                # Retry the durable job with a fresh signed URL on storage/network failures.
                r.raise_for_status()
            if r.status_code != 200:
                raise ValueError('ASSET_DOWNLOAD_FAILED')
            data = bytearray()
            async for part in r.aiter_bytes():
                data.extend(part)
                if len(data) > 20 * 1024 * 1024:
                    raise ValueError('PDF_TOO_LARGE')
        if not data.startswith(b'%PDF-'):
            raise ValueError('INVALID_PDF')
        return bytes(data)

    async def index(self, request):
        passages = []
        for doc in request.documents:
            if doc.kind == 'pdf':
                pages = await asyncio.to_thread(pdf_pages, await self.download_pdf(doc.url))
            else:
                pages = [(None, doc.text or '')]
            for page, text in pages:
                for index, (heading, content) in enumerate(markdown_chunks(text)):
                    identity = f'{request.topicId}/{request.revision}/{doc.documentId}/{doc.blockId}/{page}/{index}'
                    passages.append(Passage(chunkId=uuid5(NAMESPACE_URL, identity), documentId=doc.documentId,
                        blockId=doc.blockId, topicId=request.topicId, classId=request.classId,
                        revision=request.revision, version=request.version, title=doc.title,
                        page=page, heading=heading, content=content))
                    if len(passages) > 1500:
                        raise ValueError('TOPIC_TOO_LARGE')
        # No deletion of the old revision. .NET only activates this revision after success.
        await self.ensure_collection()
        for start in range(0, len(passages), 16):
            batch = passages[start:start + 16]
            vectors = await self.embed([p.heading + '\n' + p.content for p in batch])
            await self.qdrant('PUT', f'/collections/{self.collection}/points?wait=true', json={'points': [
                {'id': str(p.chunkId), 'vector': vector, 'payload': p.model_dump(mode='json', exclude={'score'})}
                for p, vector in zip(batch, vectors)]})
        return {'chunks': len(passages), 'collection': self.collection}

    @staticmethod
    def scope_filter(scopes):
        # Pair the revision with its topic; two independent OR lists permit cross-pair matches.
        return {'should': [{'must': [
            {'key': 'topicId', 'match': {'value': str(s.topicId)}},
            {'key': 'revision', 'match': {'value': str(s.revision)}}]} for s in scopes]}

    async def delete(self, scope):
        await self.ensure_collection()
        await self.qdrant('POST', f'/collections/{self.collection}/points/delete?wait=true',
                          json={'filter': self.scope_filter([scope])})

    async def search(self, request):
        await self.ensure_collection()
        vector = (await self.embed([request.query]))[0]
        data = await self.qdrant('POST', f'/collections/{self.collection}/points/query', json={
            'query': vector, 'filter': self.scope_filter(request.scopes),
            'limit': request.limit * 3, 'with_payload': True, 'score_threshold': self.min_score})
        allowed = {(str(s.topicId), str(s.revision)) for s in request.scopes}
        result, seen, chars = [], set(), 0
        for row in data['result']['points']:
            p = Passage.model_validate({**row['payload'], 'score': row['score']})
            if (str(p.topicId), str(p.revision)) not in allowed:
                raise ValueError('RETRIEVAL_SCOPE_MISMATCH')
            fingerprint = re.sub(r'\s+', ' ', p.content).strip()
            if fingerprint in seen or chars + len(p.content) > 16000:
                continue
            seen.add(fingerprint)
            chars += len(p.content)
            result.append(p)
            if len(result) == request.limit:
                break
        return {'passages': result, 'collection': self.collection}
