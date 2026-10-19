import hashlib
import json
from app.hints.prompts import VERSION

def cache_key(c, d, model):
    # Include every semantic input: question/history/sample outcomes cannot share stale hints.
    data = c.model_dump(mode='json', exclude={'requestId', 'studentRef'})
    data['sourceCode'] = c.sourceCode.replace('\r\n', '\n').rstrip()
    payload = [VERSION, model, d.stage, d.category, c.request.level, data]
    return 'ai:hint:' + hashlib.sha256(json.dumps(payload, sort_keys=True).encode()).hexdigest()
