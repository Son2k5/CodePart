# Đánh giá trợ giảng

`golden.jsonl` gồm 60 fixture giảng dạy tự biên soạn, 12 bài × 5 stage. Đây là dữ liệu kiểm thử mẫu, **không phải 60 bài nộp thực tế của học sinh**. Để dùng cho chương kết quả khóa luận, thay/bổ sung bộ 50–100 case có nguồn gốc, verdict, nhãn giáo viên và đầy đủ cấp 1/2/3, TLE/MLE, injection, lỗi IO.

```powershell
python -m eval.run --mock --output reports/smoke
# Đã cấu hình LLM_*: chạy provider thật; không truyền --mock.
python -m eval.run --output reports/api --input-price <USD-per-million> --output-price <USD-per-million>
# Đổi LLM_PROVIDER/BASE_URL/MODEL sang local rồi chạy cùng golden:
python -m eval.run --output reports/local --input-price 0 --output-price 0
```

Giá phải lấy từ tài khoản/model đang dùng; không có giá thì cost để trống. Giá 0 ở local chỉ là token billing, chưa tính GPU/hạ tầng. Eval chạy pipeline trong process, latency chưa bao gồm .NET/database/network frontend. `results.csv` chứa latency, token/cost, stage đúng/sai, guard status, lỗi provider, message và cột chấm tay. `report.md` tổng hợp; lỗi provider vẫn nằm ở mẫu số stage accuracy để không che thất bại.

`automatedLeakRate` dùng cùng HintGuard nên không phải phép đo độc lập. `guardInterventionRate` là tỷ lệ regenerate/fallback; không đồng nghĩa leakage thành công. Một hint lọt guard vẫn có thể lộ thuật toán bằng văn xuôi.

Chấm ít nhất một mẫu phân tầng theo stage/level: đúng nguyên nhân? hợp cấp? còn để học sinh tự làm? có lộ lời giải/test ẩn? Điền `manualCorrect`, `manualLevelAppropriate`, `manualLeakage` vào golden với true/false rồi rerun; giữ bản chấm và reviewer riêng.

Judge độc lập tùy chọn, OpenAI-compatible, cấu hình `JUDGE_MODEL`, `JUDGE_API_KEY`, `JUDGE_BASE_URL`:

```powershell
python -m eval.review reports/api/results.csv reports/api/judge.jsonl
```

Judge nhận problem/reference và hint trong chế độ đánh giá offline, không thuộc luồng học sinh. Không đưa nhãn judge thẳng vào training nếu chưa kiểm tra. Script này gọi API có tính phí khi người vận hành chạy; chưa được gọi trong phiên triển khai. So sánh API/local phải kèm tỷ lệ lỗi, latency p95, chi phí hạ tầng và đánh giá giáo viên, không chỉ stage accuracy từ rule.
