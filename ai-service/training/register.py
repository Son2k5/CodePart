"""Select/roll back an immutable local model version; restart AI service after selection."""
import argparse
import json
import os
from pathlib import Path
from app.stage.model import Classifier

def register(registry, version):
    root=Path(registry).resolve()
    selected=(root/'stage-error'/version).resolve()
    if selected.parent != root/'stage-error': raise ValueError('Invalid version')
    metadata=json.loads((selected/'metadata.json').read_text())
    if metadata['metrics']['inferenceP95Ms'] >= 50: raise ValueError('CPU inference p95 exceeds 50 ms')
    Classifier(selected)  # Verify artifact hash and loadability before activation.
    temporary=root/'active.tmp'
    temporary.write_text(json.dumps({'path':str(selected),'version':version,'sha256':metadata['modelSha256']},indent=2))
    os.replace(temporary,root/'active.json')
    return selected

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('version');p.add_argument('--registry',default='models')
    a=p.parse_args(); print(register(a.registry,a.version))
