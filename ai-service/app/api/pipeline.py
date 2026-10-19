import logging
from app.contracts import AssistResponse, Hint
from app.stage.detector import detect
from app.hints.prompts import build, fallback, VERSION
from app.guard.hint_guard import violations
from app.cache.redis_cache import cache_key

log = logging.getLogger('hint_guard')

async def assist(c, llm, cache=None, classifier=None, model_identity=''):
    d = detect(c, classifier)
    key = cache_key(c, d, model_identity + '|' + getattr(classifier, 'version', 'rules'))
    if cache:
        try:
            cached = await cache.get(key)
            if cached:
                result = AssistResponse.model_validate_json(cached)
                if not violations(result, c, d.stage):
                    return result.model_copy(update={'tokensIn': 0, 'tokensOut': 0})
        except Exception:
            log.warning('cache_read_failed')
    tokens_in = tokens_out = 0
    for attempt in range(2):
        system, user = build(c, d, strict=attempt > 0)
        completion = await llm.complete(system, user, advanced=c.request.level == 3 or d.stage == 'LogicError')
        tokens_in += completion.tokens_in
        tokens_out += completion.tokens_out
        reasons = violations(completion.hint, c, d.stage)
        if not reasons:
            result = AssistResponse(**completion.hint.model_dump(), stage=d.stage, stageConfidence=d.confidence,
                errorCategory=d.category, level=c.request.level, guardStatus='regenerated' if attempt else 'ok',
                modelVersion=completion.model, promptVersion=VERSION, tokensIn=tokens_in, tokensOut=tokens_out)
            if cache:
                try: await cache.setex(key, 600, result.model_dump_json())
                except Exception: log.warning('cache_write_failed')
            return result
        log.warning('hint_blocked request=%s reasons=%s attempt=%d', c.requestId, reasons, attempt)
    return AssistResponse(message=fallback(d.category), steps=[], suspectedLines=[], guidingQuestion=None,
        stage=d.stage, stageConfidence=d.confidence, errorCategory=d.category, level=c.request.level,
        guardStatus='fallback', modelVersion='rule-based', promptVersion=VERSION, tokensIn=tokens_in, tokensOut=tokens_out)
