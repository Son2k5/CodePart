"""Exercise prepare→train→evaluate→register with clearly synthetic data, without deployment."""
import json
from pathlib import Path
from training.prepare import prepare
from training.train import train
from training.register import register

if __name__=='__main__':
    root=Path('data/smoke');root.mkdir(parents=True,exist_ok=True)
    rows=[]
    for i in range(120):
        for stage,category,verdict,code,passed,total in [
            ('Start','Unknown','none',f'# problem {i}',0,0),
            ('InProgress','Unknown','none',f'n = int(input())\n# solve problem {i}',0,0),
            ('CannotRun','SyntaxError','CompileError',f'print({i}',0,1),
            ('LogicError','WrongAlgorithm','WrongAnswer',f'print({i}-1)',0,10),
            ('PartialPass','MissingEdgeCase','WrongAnswer',f'print(max(0,{i}))',8,10)]:
            rows.append(dict(problemId=f'smoke-{i}',sourceCode=code,starterCode=code if stage=='Start' else '',language='python',stage=stage,errorCategory=category,verdict=verdict,passed=passed,total=total))
    source=root/'input.jsonl';source.write_text('\n'.join(json.dumps(r) for r in rows),encoding='utf-8')
    prepare(source,root/'prepared')
    train(root/'prepared',root/'models','smoke-v1')
    register(root/'models','smoke-v1')
    print('Synthetic smoke completed. Do not deploy this model or report these metrics as research results.')
