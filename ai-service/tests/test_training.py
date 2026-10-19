import json
from pathlib import Path
import pandas as pd
from training.prepare import prepare
from training.features import features

def test_ast_is_not_execution():
    f=features({'sourceCode':'import os\nos.remove("never-executed")\nfor x in []:\n if x:\n  print(x)','language':'python'})
    assert f['loops']==1 and f['branches']==1 and f['depth']>0

def test_problem_split_and_dedup(tmp_path):
    source=tmp_path/'input.jsonl'
    rows=[{'problemId':f'p{i}','sourceCode':f'print({i},{j})','language':'python','stage':'LogicError','errorCategory':'WrongAlgorithm'} for i in range(100) for j in range(2)]
    source.write_text('\n'.join(json.dumps(r) for r in rows+[rows[0]]))
    prepare(source,tmp_path/'out')
    parts=[pd.read_parquet(tmp_path/'out'/f'{s}.parquet') for s in ['train','val','test']]
    assert sum(len(f) for f in parts)==200
    for a,b in [(0,1),(1,2),(0,2)]: assert not set(parts[a].problemId)&set(parts[b].problemId)
