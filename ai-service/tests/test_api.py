import os
from unittest.mock import AsyncMock
from fastapi.testclient import TestClient
from app.main import app
from app.contracts import Hint
from app.llm.client import Completion

def test_auth_schema_and_mock(monkeypatch,context):
    monkeypatch.setenv('SERVICE_API_KEY','s'*32)
    monkeypatch.delenv('REDIS_URL',raising=False)
    with TestClient(app) as client:
        assert client.post('/v1/assist',json=context.model_dump()).status_code==401
        headers={'X-Service-Key':'s'*32}
        assert client.post('/v1/assist',headers=headers,json={}).json()['code']=='INVALID_CONTEXT'
        app.state.llm=AsyncMock()
        app.state.llm.complete.return_value=Completion(Hint(message='Em hãy xác định đầu ra.',steps=[],suspectedLines=[],guidingQuestion=None),'mock')
        result=client.post('/v1/assist',headers=headers,json=context.model_dump())
        assert result.status_code==200 and result.json()['stage']=='Start'
        app.state.llm.complete.side_effect=TimeoutError()
        assert client.post('/v1/assist',headers=headers,json=context.model_dump()).json()['code']=='AI_UNAVAILABLE'

def test_deadline(monkeypatch,context):
    import asyncio
    import app.main as module
    monkeypatch.setenv('SERVICE_API_KEY','s'*32);monkeypatch.delenv('REDIS_URL',raising=False)
    monkeypatch.setattr(module,'DEADLINE_SECONDS',.01)
    async def slow(*args,**kwargs): await asyncio.sleep(1)
    with TestClient(app) as client:
        app.state.llm=AsyncMock();app.state.llm.complete.side_effect=slow
        response=client.post('/v1/assist',headers={'X-Service-Key':'s'*32},json=context.model_dump())
        assert response.status_code==503 and response.json()['code']=='AI_UNAVAILABLE'
        app.state.llm.complete.side_effect=ValueError('malformed provider JSON')
        assert client.post('/v1/assist',headers={'X-Service-Key':'s'*32},json=context.model_dump()).json()['code']=='AI_UNAVAILABLE'
