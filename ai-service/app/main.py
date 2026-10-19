import asyncio
import hmac
import os
import json
from pathlib import Path
from contextlib import asynccontextmanager
import httpx
from fastapi import FastAPI, HTTPException, Depends, Response
from fastapi.security import APIKeyHeader
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from redis.asyncio import Redis
from app.contracts import AssistRequest, AssistResponse, ErrorResponse
from app.api.pipeline import assist
from app.llm.client import ProviderClient

@asynccontextmanager
async def lifespan(app):
    if len(os.getenv('SERVICE_API_KEY', '')) < 32:
        raise RuntimeError('SERVICE_API_KEY must contain at least 32 characters')
    async with httpx.AsyncClient(timeout=8, limits=httpx.Limits(max_connections=30)) as http:
        app.state.llm = ProviderClient(http)
        from app.rag.service import RagService
        app.state.rag = RagService(http)
        app.state.cache = Redis.from_url(os.environ['REDIS_URL'], socket_timeout=.3, socket_connect_timeout=.3) if os.getenv('REDIS_URL') else None
        app.state.classifier = None
        model_path = os.getenv('MODEL_PATH')
        if os.getenv('MODEL_MANIFEST'):
            model_path = json.loads(Path(os.environ['MODEL_MANIFEST']).read_text())['path']
        if model_path:
            from app.stage.model import Classifier
            app.state.classifier = Classifier(model_path)
        yield
        if app.state.cache: await app.state.cache.aclose()

app = FastAPI(title='CodePath AI', version='1.0.0', lifespan=lifespan)
service_key = APIKeyHeader(name='X-Service-Key', auto_error=False)
DEADLINE_SECONDS = 19

@app.exception_handler(RequestValidationError)
async def invalid_context(request, exc):
    return JSONResponse({'code': 'INVALID_CONTEXT'}, status_code=422)

async def authenticate(x_service_key: str | None = Depends(service_key)):
    expected = os.getenv('SERVICE_API_KEY', '')
    if not expected or not hmac.compare_digest(x_service_key or '', expected):
        raise HTTPException(401, detail={'code': 'UNAUTHORIZED'})

@app.get('/health')
async def health(): return {'status': 'ok'}

@app.post('/v1/assist', response_model=AssistResponse, dependencies=[Depends(authenticate)],
          responses={422:{'model':ErrorResponse},503:{'model':ErrorResponse},
                     200:{'description':'Guarded hint; X-AI-Code=GUARD_FALLBACK when guardStatus=fallback'}})
async def endpoint(c: AssistRequest, response: Response):
    from app.hints.prompts import build
    from app.stage.detector import detect
    try:
        build(c, detect(c))
    except ValueError:
        return JSONResponse({'code': 'INVALID_CONTEXT'}, status_code=422)
    try:
        async with asyncio.timeout(DEADLINE_SECONDS):
            result = await assist(c, app.state.llm, app.state.cache, app.state.classifier,
                                '|'.join(os.getenv(k, '') for k in ('LLM_PROVIDER', 'LLM_MODEL', 'LLM_ADVANCED_MODEL', 'MODEL_PATH')))
            if result.guardStatus == 'fallback': response.headers['X-AI-Code'] = 'GUARD_FALLBACK'
            return result
    except Exception:
        return JSONResponse({'code': 'AI_UNAVAILABLE'}, status_code=503)

from app.rag.routes import router as rag_router
app.include_router(rag_router(authenticate))
