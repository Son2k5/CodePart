"""Import manually labeled, anonymized JSONL exports. Never infer truth from helpful alone."""
import argparse
import json
from pathlib import Path

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('input');p.add_argument('output');a=p.parse_args()
    with open(a.output,'w',encoding='utf-8') as output:
        for line in Path(a.input).read_text(encoding='utf-8').splitlines():
            row=json.loads(line)
            if not row.get('reviewedStage') or not row.get('reviewedErrorCategory'): continue
            # Explicit allowlist prevents name/email/student ID from entering a training corpus.
            safe={k:row[k] for k in ('problemId','sourceCode','language')}
            safe.update(stage=row['reviewedStage'],errorCategory=row['reviewedErrorCategory'],
                        helpful=row.get('helpful'),provenance='CodePath teacher-reviewed export',labelQuality='reviewed',
                        verdict=row.get('verdict','none'),passed=row.get('passed',0),total=row.get('total',0))
            output.write(json.dumps(safe,ensure_ascii=False)+'\n')
