import json
import httpx
import pytest
from app.llm.client import ProviderClient

@pytest.mark.parametrize('provider', ['openai','localvllm','gemini','anthropic'])
async def test_provider_payload_and_usage(monkeypatch,provider):
    monkeypatch.setenv('LLM_PROVIDER',provider);monkeypatch.setenv('LLM_MODEL','fixture-model');monkeypatch.setenv('LLM_API_KEY','fixture-key')
    monkeypatch.delenv('LLM_BASE_URL',raising=False)
    hint={'message':'Em hãy kiểm tra điều kiện biên.','steps':[],'suspectedLines':[],'guidingQuestion':None}
    def handler(request):
        body=json.loads(request.content)
        if provider in ('openai','localvllm'):
            assert body['response_format']['json_schema']['strict'] is True
            return httpx.Response(200,json={'choices':[{'message':{'content':json.dumps(hint)}}],'usage':{'prompt_tokens':10,'completion_tokens':5}})
        if provider=='gemini':
            assert body['generationConfig']['responseJsonSchema']['additionalProperties'] is False
            return httpx.Response(200,json={'candidates':[{'content':{'parts':[{'text':json.dumps(hint)}]}}],'usageMetadata':{'promptTokenCount':10,'candidatesTokenCount':5}})
        assert body['tool_choice']['name']=='hint'
        return httpx.Response(200,json={'content':[{'type':'tool_use','name':'hint','input':hint}],'usage':{'input_tokens':10,'output_tokens':5}})
    async with httpx.AsyncClient(transport=httpx.MockTransport(handler)) as http:
        result=await ProviderClient(http).complete('system','data')
        assert result.hint.message==hint['message'] and result.tokens_in==10 and result.tokens_out==5
