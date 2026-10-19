from unittest.mock import AsyncMock
import httpx
import pytest
from app.api.pipeline import assist
from app.contracts import Hint
from app.llm.client import Completion, ProviderClient
from app.cache.redis_cache import cache_key
from app.stage.detector import detect

def completion(text): return Completion(Hint(message=text,steps=[],suspectedLines=[],guidingQuestion=None),'mock',10,5)

async def test_regenerate(context):
    llm=AsyncMock(); llm.complete.side_effect=[completion('```python\nprint(1)\n```'),completion('Em hãy xác định đầu ra.')]
    result=await assist(context,llm)
    assert result.guardStatus=='regenerated' and result.tokensIn==20
    assert llm.complete.call_count==2

async def test_fallback(context):
    llm=AsyncMock(); llm.complete.return_value=completion('```python\nprint(1)\n```')
    result=await assist(context,llm)
    assert result.guardStatus=='fallback'
    assert llm.complete.call_count==2

async def test_invalid_json(monkeypatch):
    monkeypatch.setenv('LLM_MODEL','mock')
    async with httpx.AsyncClient(transport=httpx.MockTransport(lambda r:httpx.Response(200,json={'choices':[{'message':{'content':'not json'}}]}))) as http:
        with pytest.raises(ValueError): await ProviderClient(http).complete('system','user')

async def test_timeout_circuit(monkeypatch):
    monkeypatch.setenv('LLM_MODEL','mock')
    def fail(r): raise httpx.ReadTimeout('timeout')
    async with httpx.AsyncClient(transport=httpx.MockTransport(fail)) as http:
        llm=ProviderClient(http)
        for _ in range(3):
            with pytest.raises(httpx.ReadTimeout): await llm.complete('s','u')
        with pytest.raises(RuntimeError,match='AI_UNAVAILABLE'): await llm.complete('s','u')

def test_cache_question_and_model(context):
    key=cache_key(context,detect(context),'m1')
    context.request.question='Vì sao?'
    assert key!=cache_key(context,detect(context),'m1')
    assert cache_key(context,detect(context),'m1')!=cache_key(context,detect(context),'m2')
