import argparse
import hashlib
import json
import platform
import time
from datetime import datetime, timezone
from pathlib import Path
import joblib
import numpy as np
import pandas as pd
import sklearn
from sklearn.pipeline import Pipeline, FeatureUnion
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import accuracy_score, f1_score, ConfusionMatrixDisplay
from training.features import NumericFeatures, CodeText

def pipeline():
    return Pipeline([('features', FeatureUnion([
        ('tokens', Pipeline([('code', CodeText()), ('tfidf', TfidfVectorizer(max_features=20000, ngram_range=(1,2), token_pattern=r'\b\w+\b'))])),
        ('numeric', NumericFeatures())])), ('classifier', LogisticRegression(class_weight='balanced', max_iter=1000, random_state=42))])

def train(data, registry, version):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    root = Path(data)
    frames = {s: pd.read_parquet(root / f'{s}.parquet').fillna('') for s in ('train', 'val', 'test')}
    for a, b in [('train','val'),('train','test'),('val','test')]:
        if set(frames[a].problemId) & set(frames[b].problemId): raise ValueError('Problem leakage')
    out = Path(registry) / 'stage-error' / version
    out.mkdir(parents=True, exist_ok=False)
    models, metrics = {}, {}
    X = {s: f.to_dict('records') for s, f in frames.items()}
    for target in ('stage', 'errorCategory'):
        if frames['train'][target].nunique() < 2: raise ValueError(f'Need multiple {target} labels')
        model = pipeline().fit(X['train'], frames['train'][target])
        models[target] = model
        for split in ('val','test'):
            pred = model.predict(X[split]); actual = frames[split][target]
            metrics[f'{target}/{split}'] = {'accuracy': accuracy_score(actual, pred), 'macroF1': f1_score(actual, pred, average='macro', zero_division=0)}
            ConfusionMatrixDisplay.from_predictions(actual, pred, xticks_rotation=45)
            plt.tight_layout(); plt.savefig(out / f'{target}-{split}.png'); plt.close()
    timings = []
    for row in X['test'][:200]:
        start = time.perf_counter()
        models['stage'].predict_proba([row]); models['errorCategory'].predict_proba([row])
        timings.append((time.perf_counter()-start)*1000)
    metrics['inferenceP95Ms'] = float(np.percentile(timings, 95))
    from app.contracts import AssistRequest
    from app.stage.detector import detect
    rule_predictions = [detect(AssistRequest.model_validate({'requestId':'eval','studentRef':'anonymous','problem':{'title':'','statement':'','language':r['language'],'starterCode':r.get('starterCode','')},
        'sourceCode':r['sourceCode'], 'runState':{'lastAction':'submit' if r.get('verdict','none') != 'none' else 'none',
        'verdicts':[{'testCaseId':'1','isSample':True,'verdict':r.get('verdict','none')}], 'passed':r.get('passed',0), 'total':r.get('total',0)}})).stage for r in X['test']]
    metrics['ruleBaseline'] = {'accuracy': accuracy_score(frames['test'].stage, rule_predictions), 'macroF1': f1_score(frames['test'].stage, rule_predictions, average='macro', zero_division=0)}
    joblib.dump(models, out / 'model.joblib')
    metadata = {'version':version,'createdAt':datetime.now(timezone.utc).isoformat(), 'metrics':metrics,
                'python':platform.python_version(),'sklearn':sklearn.__version__, 'dataset':json.loads((root/'dataset-card.json').read_text()),
                'modelSha256':hashlib.sha256((out/'model.joblib').read_bytes()).hexdigest()}
    (out/'metadata.json').write_text(json.dumps(metadata, indent=2), encoding='utf-8')
    (out/'report.md').write_text('# Classifier evaluation\n\n```json\n'+json.dumps(metrics,indent=2)+'\n```\n\nWeak labels do not establish pedagogical accuracy. Review detailed labels before deployment.\n', encoding='utf-8')
    return out

if __name__ == '__main__':
    p=argparse.ArgumentParser(); p.add_argument('data'); p.add_argument('--registry',default='models'); p.add_argument('--version',required=True)
    a=p.parse_args(); print(train(a.data,a.registry,a.version))
