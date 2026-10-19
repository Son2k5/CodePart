# Training tái lập

Pipeline baseline CPU: JSONL → prepare → parquet theo problem → TF-IDF + AST/numeric + LogisticRegression cân bằng trọng số → evaluation → registry. Không script nào chạy code học sinh.

## Dữ liệu

Nguồn: [Project CodeNet](https://github.com/IBM/Project_CodeNet), [CodeContests](https://github.com/google-deepmind/code_contests), HumanEval, MBPP và export CodePath có annotation giáo viên. CodeContests language enum theo [schema chính thức](https://raw.githubusercontent.com/google-deepmind/code_contests/main/contest_problem.proto).

```powershell
# Tải CodeNet từ trang phát hành chính thức, giải nén; lưu URL + checksum archive trong hồ sơ dataset.
python -m training.download codenet data/codenet.jsonl --root D:/datasets/Project_CodeNet --limit 100000
# Pin revision SHA của dataset, không dùng main cho thí nghiệm tái lập.
python -m training.download codecontests data/contests.jsonl --revision <commit-sha>
python -m training.download humaneval data/humaneval.jsonl --revision <commit-sha>
python -m training.download mbpp data/mbpp.jsonl --revision <commit-sha>
python -m training.import_interactions reviewed-export.jsonl data/codepath.jsonl
```

CodeContests incorrect solutions chưa có verdict cụ thể được gắn `NeedsReview` và **loại khỏi training** đến khi có người gán nhãn. Không biến mọi incorrect solution thành LogicError. HumanEval/MBPP là lời giải tham khảo, không tạo lỗi giả bằng việc chạy code. CodeNet verdict chỉ tạo weak label; WrongAnswer→WrongAlgorithm là nhãn thô, không chứng minh thuật toán sai.

Schema tối thiểu mỗi dòng:

```json
{"problemId":"canonical-problem-id","sourceCode":"...","language":"python","stage":"LogicError","errorCategory":"OffByOne","verdict":"WrongAnswer","passed":0,"total":10,"starterCode":"","provenance":"teacher-reviewed","labelQuality":"reviewed"}
```

Ngôn ngữ `python|cpp|java`. Hợp nhất `problemId` cho các bài trùng giữa nguồn trước prepare; không đổi ID theo ngôn ngữ hay theo submission. `prepare` loại code trùng SHA, hash problem cùng seed để chia 80/10/10; kiểm tra tập problem không giao nhau. Các bài gần trùng khác ID cần rà soát thủ công. Dữ liệu Start/InProgress và lỗi chi tiết cần thu thập/annotation thật; không thể suy ra đầy đủ từ verdict corpus.

Export CodePath phải loại thông tin nhận dạng, kiểm tra cả comment trong source. Importer chỉ nhận allowlist field và bắt buộc `reviewedStage`, `reviewedErrorCategory`; `helpful` được giữ như metadata, không xem là nhãn đúng/sai.

## Train, evaluate, register và rollback

```powershell
python -m training.prepare data/combined-reviewed.jsonl data/prepared --seed 42
python -m training.train data/prepared --registry models --version v1
python -m training.register v1 --registry models
$env:MODEL_MANIFEST = "models/active.json"
# Khởi động lại service sau khi đổi manifest.
```

Artifacts: `models/stage-error/v1/model.joblib`, `metadata.json`, `report.md`, confusion matrix PNG cho stage/category trên val/test. Metadata lưu checksum dataset/model, Python/scikit-learn version, seed trong dataset card, accuracy, macro-F1, baseline rule-only và p95 CPU. Val/test giữ phân phối ban đầu; chỉ train dùng class_weight balanced.

Registry từ chối p95 ≥50 ms. P95 chỉ là benchmark trên máy chạy; đo lại với kích thước code thực tế và concurrency production. Không coi threshold là SLA đã chứng minh. Inference Python load artifact tin cậy qua config; joblib không phải định dạng an toàn cho file không tin cậy. Rollback bằng `training.register <old-version>` và restart, hoặc đặt `MODEL_PATH` trực tiếp tới version trước. Version mới không được ghi đè.

Các metric cao trên dữ liệu weak-label chứa verdict có thể chỉ phản ánh quy tắc verdict. Cần thêm benchmark không có verdict/ablation và nhãn teacher-reviewed để đánh giá giá trị ML. Model stage được train để so sánh nhưng runtime luôn giữ rule priority; error model chỉ tinh chỉnh category khi confidence ≥0.65.

## Nâng cấp classifier và LoRA tùy chọn

Cài `pip install -e ".[transformer]"` trong môi trường GPU riêng, pin dependency và revision model trước thí nghiệm.

```powershell
python -m training.finetune_classifier data/prepared experiments/codebert --model microsoft/codebert-base --revision <commit-sha>
# Có thể thay bằng microsoft/unixcoder-base.
python -m training.distill eval/golden.jsonl data/hints-candidates.jsonl
# Giáo viên duyệt các dòng rồi đặt reviewed=true; dùng problemId chung cho các cấp/stage cùng bài.
python -m training.lora data/hints-reviewed.jsonl experiments/lora --revision <commit-sha>
# QLoRA cần cài bitsandbytes, thêm --qlora trên GPU hỗ trợ.
```

Colab/Kaggle hoặc máy thuê: mount dataset/artifacts vào storage bền vững, chọn CUDA GPU hỗ trợ bf16 (nếu không có, điều chỉnh dtype/config), cài PyTorch phù hợp CUDA, không để key trong notebook commit. Baseline LoRA dùng rank 16, alpha 32, dropout 0.05, LR 2e-4, 2 epoch, batch 1 × accumulation 16; tách validation theo problem. Bắt đầu model nhỏ; tăng model lên 7B cần VRAM phù hợp.

Serve adapter bằng vLLM có hỗ trợ LoRA/structured output, hoặc merge adapter trong môi trường thí nghiệm rồi serve endpoint OpenAI-compatible. Đặt `LLM_PROVIDER=localvllm`, URL và model name đúng server. **Phải chạy benchmark API và local cùng golden set độc lập, so leakage/level/correctness/cost/latency và chấm tay trước khi bật**. Các script GPU là tùy chọn, chưa được chạy trong phiên triển khai này; không tự kích hoạt model LoRA.
