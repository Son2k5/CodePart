"""Optional teacher generation: only guarded hints become candidate training examples."""
import argparse
import asyncio
import json
from pathlib import Path
import httpx
from app.api.pipeline import assist
from app.contracts import AssistRequest
from app.hints.prompts import build
from app.stage.detector import detect
from app.llm.client import ProviderClient

async def main(args):
    async with httpx.AsyncClient(timeout=8) as http:
        llm=ProviderClient(http)
        with open(args.output,'w',encoding='utf-8') as output:
            for line in Path(args.input).read_text(encoding='utf-8').splitlines():
                row=json.loads(line); c=AssistRequest.model_validate(row.get('context',row))
                async with asyncio.timeout(20): hint=await assist(c,llm)
                if hint.guardStatus=='fallback': continue
                system,user=build(c,detect(c))
                answer={k:getattr(hint,k) for k in ('message','steps','suspectedLines','guidingQuestion')}
                output.write(json.dumps({'problemId':row.get('problemId',c.problem.title), 'stage':hint.stage,'level':hint.level,
                    'reviewed':False,'teacherModel':hint.modelVersion,'promptVersion':hint.promptVersion,
                    'messages':[{'role':'system','content':system},{'role':'user','content':user},{'role':'assistant','content':json.dumps(answer,ensure_ascii=False)}]},ensure_ascii=False)+'\n')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('input');p.add_argument('output');asyncio.run(main(p.parse_args()))
