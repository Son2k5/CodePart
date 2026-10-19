"""Evaluate labeled retrieval cases against the internal RAG service, without inventing labels.

python -m eval.run_rag cases.jsonl --output reports/rag.json
Each case: {query, scopes:[{topicId,revision}], relevantDocumentIds:[uuid,...]}.
Empty relevantDocumentIds means the annotator expects no supporting document.
"""
import argparse
import asyncio
import json
import math
import os
import statistics
import time
from pathlib import Path
import httpx


async def evaluate(path, base):
    cases = [json.loads(line) for line in Path(path).read_text(encoding='utf-8').splitlines() if line.strip()]
    if not cases:
        raise ValueError('No labeled cases')
    rows = []
    async with httpx.AsyncClient(base_url=base.rstrip('/') + '/', timeout=15,
            headers={'X-Service-Key': os.environ['SERVICE_API_KEY']}) as client:
        for number, case in enumerate(cases, 1):
            started = time.perf_counter()
            expected = set(case['relevantDocumentIds'])
            try:
                response = await client.post('internal/rag/search', json={'query': case['query'], 'scopes': case['scopes'], 'limit': 5})
                response.raise_for_status()
                found = list(dict.fromkeys(p['documentId'] for p in response.json()['passages']))
                hits = set(found) & expected
                rank = next((i for i, item in enumerate(found, 1) if item in expected), None)
                rows.append({'case': number, 'recall': len(hits) / len(expected) if expected else None,
                    'reciprocalRank': 1 / rank if rank else 0 if expected else None,
                    'emptyCorrect': not found if not expected else None, 'error': False,
                    'latencyMs': round((time.perf_counter() - started) * 1000, 2)})
            except (httpx.HTTPError, KeyError, ValueError):
                rows.append({'case': number, 'error': True, 'latencyMs': round((time.perf_counter() - started) * 1000, 2)})
    def mean(key):
        values = [r[key] for r in rows if r.get(key) is not None]
        return statistics.mean(values) if values else None
    latencies = sorted(r['latencyMs'] for r in rows)
    return {'cases': len(rows), 'errors': sum(r['error'] for r in rows),
        'recallAt5SuccessfulRequests': mean('recall'), 'mrrAt5SuccessfulRequests': mean('reciprocalRank'),
        'noSourceAccuracySuccessfulRequests': mean('emptyCorrect'),
        'latencyP50Ms': statistics.median(latencies), 'latencyP95Ms': latencies[max(0, math.ceil(.95 * len(rows)) - 1)],
        'results': rows}


def main():
    parser = argparse.ArgumentParser(); parser.add_argument('cases'); parser.add_argument('--output', default='reports/rag.json')
    parser.add_argument('--base-url', default='http://127.0.0.1:8000')
    args = parser.parse_args()
    result = asyncio.run(evaluate(args.cases, args.base_url))
    output = Path(args.output); output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps({k: v for k, v in result.items() if k != 'results'}, indent=2))


if __name__ == '__main__':
    main()
