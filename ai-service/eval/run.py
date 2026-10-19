import argparse
import asyncio
import csv
import json
import os
import time
from pathlib import Path
import httpx
from app.api.pipeline import assist
from app.contracts import AssistRequest, Hint
from app.guard.hint_guard import violations
from app.llm.client import ProviderClient, Completion

class FixtureLlm:
    async def complete(self, system,user,advanced=False):
        return Completion(Hint(message='Em hãy xác định đầu vào, đầu ra và tự lập bảng giá trị biến trên một ví dụ nhỏ.',steps=[],suspectedLines=[],guidingQuestion='Bước nào khác với kết quả em dự đoán?'),'fixture-mock')

async def run(args):
    cases=[json.loads(line) for line in Path(args.golden).read_text(encoding='utf-8').splitlines() if line.strip()]
    if not 50 <= len(cases) <= 100: raise ValueError('Golden set requires 50–100 cases')
    output=Path(args.output); output.mkdir(parents=True,exist_ok=True)
    rows=[]
    async with httpx.AsyncClient(timeout=8) as http:
        llm=FixtureLlm() if args.mock else ProviderClient(http)
        for case in cases:
            context=AssistRequest.model_validate(case['context']); start=time.perf_counter()
            try:
                async with asyncio.timeout(20): result=await assist(context,llm)
                row={'id':case['id'],'stageCorrect':int(result.stage==case['expectedStage']),
                     'guardStatus':result.guardStatus,'automatedLeak':int(bool(violations(result,context,result.stage))),
                     'tokensIn':result.tokensIn,'tokensOut':result.tokensOut,'latencyMs':round((time.perf_counter()-start)*1000,2),
                     'costUsd':(result.tokensIn*args.input_price+result.tokensOut*args.output_price)/1_000_000 if args.input_price is not None and args.output_price is not None else '',
                     'message':result.message,'hintJson':result.model_dump_json(),'manualLevelAppropriate':case.get('manualLevelAppropriate'),
                     'manualCorrect':case.get('manualCorrect'),'manualLeakage':case.get('manualLeakage'),'error':''}
            except Exception as error:
                row={'id':case['id'],'stageCorrect':0,'guardStatus':'error','automatedLeak':'','tokensIn':0,'tokensOut':0,
                     'latencyMs':round((time.perf_counter()-start)*1000,2),'costUsd':'','message':'','hintJson':'',
                     'manualLevelAppropriate':None,'manualCorrect':None,'manualLeakage':None,'error':type(error).__name__}
            rows.append(row)
    with (output/'results.csv').open('w',newline='',encoding='utf-8') as f:
        writer=csv.DictWriter(f,fieldnames=rows[0].keys()); writer.writeheader(); writer.writerows(rows)
    completed=[r for r in rows if not r['error']]
    report={'mode':'mock smoke only' if args.mock else 'provider','cases':len(rows),'completed':len(completed),
            'stageAccuracy':sum(r['stageCorrect'] for r in rows)/len(rows),
            'automatedLeakRate':sum(r['automatedLeak'] for r in completed)/max(1,len(completed)),
            'guardInterventionRate':sum(r['guardStatus']!='ok' for r in completed)/max(1,len(completed)),
            'latencyMeanMs':sum(r['latencyMs'] for r in rows)/len(rows),
            'latencyP95Ms':sorted(r['latencyMs'] for r in rows)[min(len(rows)-1,int(len(rows)*.95))],
            'humanReviewCompleted':sum(r['manualCorrect'] is not None for r in rows),
            'humanLeakRate':sum(r['manualLeakage'] is True for r in rows)/sum(r['manualLeakage'] is not None for r in rows) if any(r['manualLeakage'] is not None for r in rows) else None,
            'humanLevelFitRate':sum(r['manualLevelAppropriate'] is True for r in rows)/sum(r['manualLevelAppropriate'] is not None for r in rows) if any(r['manualLevelAppropriate'] is not None for r in rows) else None,
            'limitations':'Automated leakage detection is the same guard, not an independent correctness oracle. Human/independent judge review is required; mock results do not establish LLM quality.'}
    (output/'report.md').write_text('# Assistant evaluation\n\n```json\n'+json.dumps(report,indent=2)+'\n```\n',encoding='utf-8')
    (output/'metadata.json').write_text(json.dumps({'provider':os.getenv('LLM_PROVIDER'),'model':os.getenv('LLM_MODEL'),'report':report},indent=2),encoding='utf-8')
    print(json.dumps(report,indent=2))

if __name__=='__main__':
    p=argparse.ArgumentParser(); p.add_argument('--golden',default='eval/golden.jsonl'); p.add_argument('--output',default='reports/eval'); p.add_argument('--mock',action='store_true')
    p.add_argument('--input-price',type=float); p.add_argument('--output-price',type=float)
    asyncio.run(run(p.parse_args()))
