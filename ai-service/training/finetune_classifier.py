"""Optional CodeBERT/UniXcoder classifier upgrade. Never runs student code."""
import argparse
import json
from pathlib import Path
import numpy as np
from datasets import Dataset
from sklearn.metrics import f1_score
from transformers import AutoTokenizer, AutoModelForSequenceClassification, Trainer, TrainingArguments
import pandas as pd

if __name__ == '__main__':
    p=argparse.ArgumentParser(); p.add_argument('data'); p.add_argument('output'); p.add_argument('--model',default='microsoft/codebert-base'); p.add_argument('--revision',required=True)
    a=p.parse_args(); root=Path(a.data)
    frames={s:pd.read_parquet(root/f'{s}.parquet') for s in ('train','val','test')}
    labels=sorted(frames['train'].errorCategory.unique()); mapping={v:i for i,v in enumerate(labels)}
    tokenizer=AutoTokenizer.from_pretrained(a.model,revision=a.revision)
    datasets={}
    for split,f in frames.items():
        if not set(f.errorCategory) <= set(labels): raise ValueError('Unseen evaluation label')
        ds=Dataset.from_dict({'text':f.sourceCode.tolist(),'labels':[mapping[v] for v in f.errorCategory]})
        datasets[split]=ds.map(lambda b:tokenizer(b['text'],truncation=True,padding='max_length',max_length=512),batched=True).remove_columns('text')
    model=AutoModelForSequenceClassification.from_pretrained(a.model,revision=a.revision,num_labels=len(labels),id2label=dict(enumerate(labels)),label2id=mapping)
    trainer=Trainer(model=model,args=TrainingArguments(output_dir=a.output,seed=42,num_train_epochs=3,per_device_train_batch_size=8,eval_strategy='epoch',save_strategy='epoch',report_to=[]),
        train_dataset=datasets['train'],eval_dataset=datasets['val'],compute_metrics=lambda p:{'macro_f1':f1_score(p.label_ids,np.argmax(p.predictions,axis=1),average='macro')})
    trainer.train(); trainer.save_model(a.output); tokenizer.save_pretrained(a.output)
    (Path(a.output)/'test-metrics.json').write_text(json.dumps(trainer.evaluate(datasets['test']),indent=2))
