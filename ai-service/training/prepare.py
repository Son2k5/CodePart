"""Normalize labeled JSONL, deduplicate code and split strictly by problem family."""
import argparse
import hashlib
import json
from pathlib import Path
import pandas as pd

def prepare(source, output, seed=42):
    rows = [json.loads(x) for x in Path(source).read_text(encoding='utf-8').splitlines() if x.strip()]
    frame = pd.DataFrame(rows)
    required = {'problemId', 'sourceCode', 'language', 'stage', 'errorCategory'}
    if not required <= set(frame): raise ValueError(f'Required columns: {required}')
    frame = frame[frame.language.isin(['python', 'cpp', 'java'])].copy()
    rejected = int((~frame.stage.isin(['Start','InProgress','CannotRun','LogicError','PartialPass'])).sum())
    frame = frame[frame.stage.isin(['Start','InProgress','CannotRun','LogicError','PartialPass'])].copy()
    for name, default in {'starterCode':'', 'verdict':'none', 'passed':0, 'total':0, 'failedSamples':0}.items():
        if name not in frame: frame[name] = default
        else: frame[name] = frame[name].fillna(default)
    frame['hash'] = frame.sourceCode.map(lambda s: hashlib.sha256(s.encode()).hexdigest())
    frame = frame.drop_duplicates('hash')
    # Use canonical problem IDs across sources; aliases MUST be merged by the curator.
    def split(problem):
        n = int(hashlib.sha256(f'{seed}:{problem}'.encode()).hexdigest()[:8], 16) % 100
        return 'train' if n < 80 else 'val' if n < 90 else 'test'
    frame['split'] = frame.problemId.map(split)
    out = Path(output); out.mkdir(parents=True, exist_ok=True)
    for name in ('train', 'val', 'test'):
        part = frame[frame.split == name]
        if part.empty: raise ValueError(f'Empty {name} split; supply more distinct problems')
        part.to_parquet(out / f'{name}.parquet', index=False)
    card = {'seed': seed, 'sha256': hashlib.sha256(Path(source).read_bytes()).hexdigest(), 'rows': len(frame), 'unreviewedExcluded':rejected,
            'problems': int(frame.problemId.nunique()), 'splits': frame.split.value_counts().to_dict(),
            'stageCounts': frame.stage.value_counts().to_dict(), 'categoryCounts': frame.errorCategory.value_counts().to_dict(),
            'balance': 'class_weight=balanced on training only; test distribution preserved',
            'limitations': 'Verdict-derived labels are weak labels. Accepted is InProgress, not Start. Detailed categories require human annotations. Check cross-source problem aliases and license before use.'}
    (out / 'dataset-card.json').write_text(json.dumps(card, indent=2), encoding='utf-8')
    (out / 'DATASET_CARD.md').write_text('# Dataset card\n\n```json\n' + json.dumps(card, indent=2) + '\n```\n', encoding='utf-8')

if __name__ == '__main__':
    p = argparse.ArgumentParser(); p.add_argument('source'); p.add_argument('output'); p.add_argument('--seed', type=int, default=42)
    a = p.parse_args(); prepare(a.source, a.output, a.seed)
