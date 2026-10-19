import os
import time
from abc import ABC, abstractmethod
from dataclasses import dataclass
import httpx
from tenacity import retry, stop_after_attempt, wait_fixed, retry_if_exception_type
from app.contracts import Hint

@dataclass
class Completion:
    hint: Hint
    model: str
    tokens_in: int = 0
    tokens_out: int = 0

class LlmClient(ABC):
    @abstractmethod
    async def complete(self, system, user, advanced=False) -> Completion: ...

class TransientProviderError(RuntimeError): pass

class ProviderClient(LlmClient):
    def __init__(self, http):
        self.http = http
        self.provider = os.getenv('LLM_PROVIDER', 'openai').lower()
        if self.provider not in ('openai', 'localvllm', 'gemini', 'anthropic'):
            raise ValueError('Unsupported LLM_PROVIDER')
        self.failures = 0
        self.open_until = 0.0

    async def complete(self, system, user, advanced=False, response_model=Hint, max_tokens=1200):
        if time.monotonic() < self.open_until:
            raise RuntimeError('AI_UNAVAILABLE')
        try:
            result = await self._request(system, user, advanced, response_model, max_tokens)
            self.failures = 0
            return result
        except Exception:
            self.failures += 1
            if self.failures >= 3: self.open_until = time.monotonic() + 30
            raise

    @retry(stop=stop_after_attempt(2), wait=wait_fixed(.2), retry=retry_if_exception_type((httpx.TransportError, TransientProviderError)), reraise=True)
    async def _request(self, system, user, advanced, response_model=Hint, max_tokens=1200):
        model = os.getenv('LLM_ADVANCED_MODEL' if advanced else 'LLM_MODEL', '')
        if not model: raise RuntimeError('LLM_MODEL is required')
        key = os.getenv('LLM_API_KEY', '')
        schema = response_model.model_json_schema()
        headers = {'Authorization': f'Bearer {key}'}
        if self.provider in ('openai', 'localvllm'):
            base = os.getenv('LLM_BASE_URL', 'https://api.openai.com/v1').rstrip('/')
            url = base + '/chat/completions'
            body = {'model': model, 'messages': [{'role': 'system', 'content': system}, {'role': 'user', 'content': user}],
                    'max_completion_tokens': max_tokens, 'response_format': {'type': 'json_schema', 'json_schema': {'name': 'hint', 'strict': True, 'schema': schema}}}
        elif self.provider == 'gemini':
            base = os.getenv('LLM_BASE_URL', 'https://generativelanguage.googleapis.com/v1beta').rstrip('/')
            url = f'{base}/models/{model}:generateContent'
            headers = {'x-goog-api-key': key}
            body = {'systemInstruction': {'parts': [{'text': system}]}, 'contents': [{'parts': [{'text': user}]}],
                    'generationConfig': {'maxOutputTokens': max_tokens, 'responseMimeType': 'application/json', 'responseJsonSchema': schema}}
        else:
            url = os.getenv('LLM_BASE_URL', 'https://api.anthropic.com/v1').rstrip('/') + '/messages'
            headers = {'x-api-key': key, 'anthropic-version': '2023-06-01'}
            body = {'model': model, 'max_tokens': max_tokens, 'system': system, 'messages': [{'role': 'user', 'content': user}],
                    'tools': [{'name': 'hint', 'description': 'Provide a tutoring hint', 'input_schema': schema}],
                    'tool_choice': {'type': 'tool', 'name': 'hint'}}
        response = await self.http.post(url, headers=headers, json=body, timeout=40 if max_tokens > 1200 else 8)
        if response.status_code == 429 or response.status_code >= 500:
            raise TransientProviderError('AI_UNAVAILABLE')
        response.raise_for_status()
        data = response.json()
        if self.provider in ('openai', 'localvllm'):
            hint = response_model.model_validate_json(data['choices'][0]['message']['content'])
            usage = data.get('usage', {})
            return Completion(hint, model, usage.get('prompt_tokens', 0), usage.get('completion_tokens', 0))
        if self.provider == 'gemini':
            hint = response_model.model_validate_json(data['candidates'][0]['content']['parts'][0]['text'])
            usage = data.get('usageMetadata', {})
            return Completion(hint, model, usage.get('promptTokenCount', 0), usage.get('candidatesTokenCount', 0))
        hint = response_model.model_validate(next(x['input'] for x in data['content'] if x['type'] == 'tool_use' and x['name'] == 'hint'))
        usage = data.get('usage', {})
        return Completion(hint, model, usage.get('input_tokens', 0), usage.get('output_tokens', 0))
