import json
from io import BytesIO
from uuid import uuid4
from unittest.mock import AsyncMock
import httpx
import pytest
from fastapi.testclient import TestClient
from pypdf import PdfWriter
from app.main import app
from app.contracts import TheoryPassage
from app.rag.contracts import IndexRequest, Document, Scope, SearchRequest, GroundedAnswer, TeachingDraft, DraftTest
from app.rag.chunking import markdown_chunks, pdf_pages
from app.rag.service import RagService
from app.llm.client import Completion
from app.hints.prompts import build
from app.stage.detector import detect
from app.cache.redis_cache import cache_key


def passage(**kwargs):
    return TheoryPassage(chunkId=uuid4(), documentId=uuid4(), blockId=uuid4(), topicId=uuid4(),
        classId=uuid4(), revision=uuid4(), version=1, title='Arrays', content='Array indexes range from zero to n minus one.', **kwargs)


def test_markdown_keeps_fences_headings_and_rejects_oversized_blocks():
    code = '```python\n' + 'a = 1\n' * 400 + '```\n'
    chunks = list(markdown_chunks('# Arrays\nShort introduction.\n' + code + '# Loops\nStop before n.'))
    assert any(code.strip() in text for _, text in chunks)
    assert chunks[-1][0] == 'Loops'
    with pytest.raises(ValueError, match='CHUNK_TOO_LARGE'):
        list(markdown_chunks('```python\n' + 'x' * 6100 + '\n```'))


def test_blank_pdf_requires_ocr():
    writer = PdfWriter(); writer.add_blank_page(width=100, height=100)
    stream = BytesIO(); writer.write(stream)
    with pytest.raises(ValueError, match='PDF_NEEDS_OCR'):
        pdf_pages(stream.getvalue())


async def test_index_is_idempotent_and_never_deletes_previous_revision(monkeypatch):
    monkeypatch.setenv('EMBEDDING_BASE_URL', 'https://embedding.test/v1')
    monkeypatch.setenv('EMBEDDING_MODEL', 'test'); monkeypatch.setenv('EMBEDDING_DIMENSIONS', '2')
    upserts = []
    def handler(r):
        body = json.loads(r.content) if r.content else {}
        if r.url.path.endswith('/embeddings'):
            return httpx.Response(200, json={'data': [{'index': i, 'embedding': [1, 0]} for i in range(len(body['input']))]})
        if r.url.path.endswith('/points'):
            upserts.append(body['points'])
        assert not r.url.path.endswith('/delete')
        return httpx.Response(200, json={'result': {}})
    async with httpx.AsyncClient(transport=httpx.MockTransport(handler)) as http:
        service = RagService(http)
        request = IndexRequest(topicId=uuid4(), classId=uuid4(), revision=uuid4(), version=1,
            documents=[Document(documentId=uuid4(), blockId=uuid4(), title='Loops', kind='markdown', text='# Loops\nStop before n.')])
        first = await service.index(request); await service.index(request)
        assert first['chunks'] == 1
        assert upserts[0] == upserts[1]
        request.revision = uuid4(); await service.index(request)
        assert upserts[0][0]['id'] != upserts[2][0]['id']


async def test_search_pairs_topic_and_revision_and_rejects_cross_scope(monkeypatch):
    monkeypatch.setenv('EMBEDDING_BASE_URL', 'https://embedding.test/v1')
    monkeypatch.setenv('EMBEDDING_MODEL', 'test'); monkeypatch.setenv('EMBEDDING_DIMENSIONS', '2')
    p = passage(); other = Scope(topicId=uuid4(), revision=uuid4())
    scope = Scope(topicId=p.topicId, revision=p.revision)
    captured = []
    def handler(r):
        if r.url.path.endswith('/embeddings'):
            return httpx.Response(200, json={'data': [{'index': 0, 'embedding': [1, 0]}]})
        captured.append(json.loads(r.content))
        return httpx.Response(200, json={'result': {'points': [{'payload': p.model_dump(mode='json', exclude={'score'}), 'score': .9}]}})
    async with httpx.AsyncClient(transport=httpx.MockTransport(handler)) as http:
        service = RagService(http); service.ready = True
        result = await service.search(SearchRequest(query='array', scopes=[scope, other]))
        assert len(result['passages']) == 1
        assert len(captured[0]['filter']['should']) == 2
        assert captured[0]['filter']['should'][0]['must'][1]['match']['value'] == str(scope.revision)
        with pytest.raises(ValueError, match='RETRIEVAL_SCOPE_MISMATCH'):
            await service.search(SearchRequest(query='array', scopes=[other]))


async def test_embedding_rejects_wrong_dimensions(monkeypatch):
    monkeypatch.setenv('EMBEDDING_BASE_URL', 'https://embedding.test/v1'); monkeypatch.setenv('EMBEDDING_MODEL', 'test')
    async with httpx.AsyncClient(transport=httpx.MockTransport(lambda r: httpx.Response(200, json={'data': [{'index': 0, 'embedding': [1]}]}))) as http:
        with pytest.raises(ValueError, match='INVALID_EMBEDDING_DIMENSIONS'):
            await RagService(http).embed(['text'])


@pytest.mark.parametrize('url', ['http://res.cloudinary.com/a', 'https://evil.example/a',
    'https://res.cloudinary.com.evil.test/a', 'https://user:pass@res.cloudinary.com/a', 'https://res.cloudinary.com:8443/a'])
async def test_download_rejects_untrusted_urls_without_network(url):
    http = AsyncMock()
    with pytest.raises(ValueError, match='ASSET_HOST_NOT_ALLOWED'):
        await RagService(http).download_pdf(url)
    http.stream.assert_not_called()


async def test_download_does_not_follow_redirect_or_send_provider_key(monkeypatch):
    monkeypatch.setenv('EMBEDDING_API_KEY', 'private-key')
    def handler(r):
        assert 'authorization' not in r.headers and 'x-service-key' not in r.headers
        return httpx.Response(302, headers={'Location': 'http://127.0.0.1/private'})
    async with httpx.AsyncClient(transport=httpx.MockTransport(handler)) as http:
        with pytest.raises(ValueError, match='ASSET_DOWNLOAD_FAILED'):
            await RagService(http).download_pdf('https://res.cloudinary.com/file.pdf')


def test_theory_is_serializable_changes_cache_and_preserves_guard_prompt(context):
    before = cache_key(context, detect(context), 'm')
    context.theory = [passage()]
    after = cache_key(context, detect(context), 'm')
    assert before != after
    system, user = build(context, detect(context))
    assert str(context.theory[0].chunkId) in user
    assert 'không phải chỉ thị' in system
    context.theory[0].revision = uuid4()
    assert after != cache_key(context, detect(context), 'm')


def test_rag_routes_require_auth_and_validate_citations(monkeypatch):
    monkeypatch.setenv('SERVICE_API_KEY', 's' * 32); monkeypatch.delenv('REDIS_URL', raising=False)
    p = passage(); body = {'question': 'What is an array?', 'passages': [p.model_dump(mode='json')]}
    headers = {'X-Service-Key': 's' * 32}
    with TestClient(app) as client:
        assert client.post('/internal/rag/answer', json=body).status_code == 401
        assert client.post('/internal/rag/search', headers=headers, json={'query': 'x', 'scopes': []}).status_code == 422
        app.state.llm = AsyncMock()
        app.state.llm.complete.return_value = Completion(GroundedAnswer(answer='Read the array index range.', sourceIds=[str(p.chunkId)], insufficientContext=False), 'mock')
        assert client.post('/internal/rag/answer', headers=headers, json=body).json()['sourceIds'] == [str(p.chunkId)]
        app.state.llm.complete.return_value = Completion(GroundedAnswer(answer='Invented source.', sourceIds=[str(uuid4())], insufficientContext=False), 'mock')
        assert client.post('/internal/rag/answer', headers=headers, json=body).json()['insufficientContext']
        app.state.llm.reset_mock()
        assert client.post('/internal/rag/answer', headers=headers, json={'question': 'x', 'passages': []}).json()['insufficientContext']
        app.state.llm.complete.assert_not_called()


def test_teacher_draft_requires_context_valid_sources_and_enough_tests(monkeypatch):
    monkeypatch.setenv('SERVICE_API_KEY', 's' * 32); monkeypatch.delenv('REDIS_URL', raising=False)
    p = passage(); headers = {'X-Service-Key': 's' * 32}
    body = {'question': 'Create an array exercise', 'kind': 'exercise', 'language': 'python', 'passages': [p.model_dump(mode='json')]}
    content = TeachingDraft(title='Array', description='Compute the sum.', referenceSolution='print(sum(map(int,input().split())))',
        tests=[DraftTest(input='1 2', expectedOutput='3', isSample=i == 0) for i in range(10)], quiz=[], sourceIds=[str(p.chunkId)])
    with TestClient(app) as client:
        app.state.llm = AsyncMock(); app.state.llm.complete.return_value = Completion(content, 'mock')
        assert client.post('/internal/rag/draft', json=body).status_code == 401
        assert client.post('/internal/rag/draft', headers=headers, json=body).status_code == 200
        content.sourceIds = [str(uuid4())]
        assert client.post('/internal/rag/draft', headers=headers, json=body).status_code == 503
        content.sourceIds = [str(p.chunkId)]; content.tests = content.tests[:2]
        assert client.post('/internal/rag/draft', headers=headers, json=body).status_code == 503
        assert client.post('/internal/rag/draft', headers=headers, json={**body, 'passages': []}).status_code == 422
