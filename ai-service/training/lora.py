"""Optional GPU experiment; only reviewed distillation data, split by problem."""
import argparse
import hashlib
import json
from pathlib import Path
import torch
from datasets import Dataset
from peft import LoraConfig
from transformers import AutoModelForCausalLM, AutoTokenizer, BitsAndBytesConfig
from trl import SFTConfig, SFTTrainer

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('input');p.add_argument('output');p.add_argument('--model',default='Qwen/Qwen2.5-Coder-1.5B-Instruct');p.add_argument('--revision',required=True);p.add_argument('--qlora',action='store_true')
    a=p.parse_args()
    if not torch.cuda.is_available(): raise RuntimeError('This experiment requires a CUDA GPU')
    rows=[r for r in map(json.loads,Path(a.input).read_text(encoding='utf-8').splitlines()) if r.get('reviewed') is True]
    def validation(r): return int(hashlib.sha256(r['problemId'].encode()).hexdigest()[:8],16)%10==0
    train=[{'messages':r['messages']} for r in rows if not validation(r)]
    val=[{'messages':r['messages']} for r in rows if validation(r)]
    if not train or not val: raise ValueError('Need reviewed examples from enough independent problems')
    tokenizer=AutoTokenizer.from_pretrained(a.model,revision=a.revision)
    tokenizer.pad_token=tokenizer.eos_token
    model=AutoModelForCausalLM.from_pretrained(a.model,revision=a.revision,torch_dtype=torch.bfloat16,
        quantization_config=BitsAndBytesConfig(load_in_4bit=True,bnb_4bit_quant_type='nf4',bnb_4bit_compute_dtype=torch.bfloat16) if a.qlora else None)
    trainer=SFTTrainer(model=model,processing_class=tokenizer,train_dataset=Dataset.from_list(train),eval_dataset=Dataset.from_list(val),
        peft_config=LoraConfig(r=16,lora_alpha=32,lora_dropout=.05,target_modules=['q_proj','k_proj','v_proj','o_proj'],task_type='CAUSAL_LM'),
        args=SFTConfig(output_dir=a.output,seed=42,num_train_epochs=2,per_device_train_batch_size=1,gradient_accumulation_steps=16,
                       learning_rate=2e-4,bf16=True,gradient_checkpointing=True,eval_strategy='epoch',save_strategy='epoch',report_to=[]))
    trainer.train();trainer.save_model(a.output);tokenizer.save_pretrained(a.output)
    Path(a.output,'EXPERIMENT_ONLY.txt').write_text('Benchmark against the API on independent golden cases, including human leakage/level review, before enabling LocalVLLM.')
