"""Download supported public corpora into a common JSONL format; never execute solutions."""
import argparse
import csv
import json
from pathlib import Path

LABELS = {'Accepted': ('InProgress', 'Unknown'), 'Wrong Answer': ('LogicError', 'WrongAlgorithm'),
          'Runtime Error': ('CannotRun', 'RuntimeError'), 'Compile Error': ('CannotRun', 'SyntaxError'),
          'Compilation Error': ('CannotRun', 'SyntaxError'),
          'Time Limit Exceeded': ('PartialPass', 'Complexity'), 'Memory Limit Exceeded': ('PartialPass', 'Complexity')}

def codenet(root):
    root = Path(root)
    for metadata in sorted((root / 'metadata').glob('p*.csv')):
        with metadata.open(encoding='utf-8') as f:
            for row in csv.DictReader(f):
                lang = {'Python': 'python', 'C++': 'cpp', 'Java': 'java'}.get(row['language'])
                if not lang or row['status'] not in LABELS: continue
                path = root / 'data' / row['problem_id'] / row['language'] / (row['submission_id'] + '.' + row['filename_ext'])
                if not path.is_file(): continue
                stage, category = LABELS[row['status']]
                yield dict(problemId='codenet:' + row['problem_id'], sourceCode=path.read_text(encoding='utf-8', errors='replace'),
                    language=lang, stage=stage, errorCategory=category, verdict=row['status'].replace(' ', '').replace('CompilationError','CompileError'), provenance='Project CodeNet', labelQuality='weak')

def download(name, revision):
    from datasets import load_dataset
    ids = {'codecontests': 'deepmind/code_contests', 'humaneval': 'openai/openai_humaneval', 'mbpp': 'google-research-datasets/mbpp'}
    dataset = load_dataset(ids[name], revision=revision)
    for split, rows in dataset.items():
        for i, row in enumerate(rows):
            pid = f'{name}:' + str(row.get('name', row.get('task_id', i)))
            if name == 'codecontests':
                for field, stage, category in [('solutions', 'InProgress', 'Unknown'), ('incorrect_solutions', 'NeedsReview', 'Unknown')]:
                    for lang, code in zip(row[field]['language'], row[field]['solution']):
                        language = {2: 'cpp', 3: 'python', 4: 'java'}.get(lang)
                        if language:
                            # Incorrect CodeContests solutions have no precise verdict; do not invent one.
                            yield dict(problemId=pid, sourceCode=code, language=language, stage=stage, errorCategory=category,
                                       verdict='none', provenance=f'{name}@{revision}/{split}', labelQuality='weak')
            else:
                code = row['prompt'] + row['canonical_solution'] if name == 'humaneval' else row['code']
                yield dict(problemId=pid, sourceCode=code, language='python', stage='InProgress', errorCategory='Unknown',
                           verdict='Accepted', provenance=f'{name}@{revision}/{split}', labelQuality='reference')

if __name__ == '__main__':
    p = argparse.ArgumentParser(); p.add_argument('dataset', choices=['codenet', 'codecontests', 'humaneval', 'mbpp']); p.add_argument('output')
    p.add_argument('--root'); p.add_argument('--revision', help='Pinned HuggingFace commit SHA; required for reproducibility')
    p.add_argument('--limit', type=int, default=100000)
    a = p.parse_args()
    if a.dataset == 'codenet' and not a.root: p.error('--root must point to an extracted Project CodeNet archive')
    if a.dataset != 'codenet' and not a.revision: p.error('--revision is required')
    source = codenet(a.root) if a.dataset == 'codenet' else download(a.dataset, a.revision)
    with open(a.output, 'w', encoding='utf-8') as f:
        for i, row in enumerate(source):
            if i >= a.limit: break
            f.write(json.dumps(row, ensure_ascii=False) + '\n')
