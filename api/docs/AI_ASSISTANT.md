# AI Coding Assistant — triển khai và nghiệm thu

Điểm vào: [README service](../../ai-service/README.md), [contract OpenAPI](../../ai-service/openapi.json), [training](../../ai-service/training/README.md), [evaluation](../../ai-service/eval/README.md).

## File/thư mục mới

| Phần | Đường dẫn | Nội dung |
|---|---|---|
| Application | `src/Application/CodePath.Application/Ai/Contracts.cs` | Interface client/service, DTO, policy quota/unlock |
| Domain | `src/Domain/CodePath.Domain/Ai/AiInteraction.cs` | Lưu stage/level/response/guard/model/helpful |
| Infrastructure | `src/Infrastructure/CodePath.Infrastructure/Ai/` | `AiService`, `PythonAiServiceClient`, `AiRegistration` |
| Migration | `src/Infrastructure/CodePath.Infrastructure/Judge/Migrations/20261016030551_AiAssistantInteractions*` | Bảng interaction và model snapshot |
| API | `src/Api/CodePath.Api/Endpoints/AiEndpoints.cs` | Assist JSON/SSE, feedback, SignalR hub |
| .NET tests | `tests/CodePath.Unit.Tests/Ai/AiAssistantTests.cs` | Client lỗi/malformed/cancel, quota boundary, unlock |
| Python | `../ai-service/app/contracts.py`, `main.py` | Strict schema, auth, deadline, health |
| Python modules | `../ai-service/app/{api,stage,hints,guard,llm,prompts,cache}/` | Rule/ML, pipeline, prompt versioning, guard, bốn provider, Redis |
| Training | `../ai-service/training/` | download/import, prepare, features, train/evaluate, register, smoke; optional classifier/LoRA/distill |
| Evaluation | `../ai-service/eval/` | 60 fixture, runner CSV/markdown, independent judge, README |
| Python tests | `../ai-service/tests/` | Stage, priority, guard, adapters, API, cache key, data split |
| Docker/config | `docker-compose.yml` (dev), `docker-compose.production.yml` (production), `.env.ai.example`, `../ai-service/Dockerfile`, `.env.example`, `.dockerignore`, `pyproject.toml` | Profile ai/rag, giới hạn tài nguyên, cấu hình |
| Frontend | `../frontend/src/features/ai-assistant/` | Panel, styles, SSE decoder |
| Frontend tests | `../frontend/tests/assistant-stream.test.mjs` | UTF-8 chunk boundaries và truncated stream |

File có sẵn được tích hợp: API `Program.cs`; package props/csproj (Polly); `JudgeDbContext` + snapshot; `JudgeService` (lưu Run snapshot vào Redis); `JudgePage.tsx` (panel, Monaco highlight). Các thay đổi có sẵn của người dùng không bị revert.

## Phạm vi và giới hạn

- Đã có mã cho luồng end-to-end của Code Judge. Màn hình Exercise cũ chưa nối trợ giảng.
- Auth/enrollment, context từ server, quota/rate/unlock, HMAC ID, persistence, SignalR và SSE đã được thêm. SSE chỉ phát nội dung đã qua guard.
- Chưa tự bật AutoHint từ idle browser. Stuck hiện là tín hiệu suy ra từ lịch sử hỏi/submit.
- Golden mẫu và smoke model là dữ liệu tự tạo để kiểm thử, không phải bằng chứng chất lượng trên học sinh thật.
- Chưa train corpus CodeNet/CodeContests thật, chưa chấm tay/LLM judge thật, chưa chạy GPU/LoRA hay benchmark LLM API/local thật. Các script và hướng dẫn được cung cấp để chạy khi có dữ liệu/model/key.
- Chưa áp dụng migration lên database đang dùng. Docker Engine không chạy khi kiểm thử tại máy, nên container/MySQL/Redis integration chưa được xác nhận.
- Guard AST/token là heuristic, không thể bảo đảm chặn mọi diễn giải lời giải. Reference solution là optional trong contract, hiện model Problem không lưu trường này.

## Checklist thủ công

Kết quả kiểm tra cục bộ ngày 16/10/2026:

- Python 3.11.17: 34 pytest passed; mock evaluation hoàn tất 60/60 case.
- .NET unit: 85 passed, 7 skipped do chưa cấu hình database test; architecture: 27 passed.
- SSE decoder: 2 passed; TypeScript/Vite production build thành công (còn cảnh báo bundle Monaco lớn).
- Training smoke: 600 mẫu tổng hợp / 120 problem, prepare → train → report/confusion matrix → register chạy thành công. Không phải metric trên corpus thật.
- EF: không có model change chưa có migration. Chưa chạy migration vào DB.
- Docker integration chưa chạy vì Docker Engine không hoạt động. Python test client có cảnh báo deprecation từ Starlette/httpx, không gây test failure.

Chuẩn bị học sinh A đã enroll, học sinh B không enroll, bài Published với starter và sample, Judge0 hoạt động. Áp dụng migration; bật AI bằng cấu hình riêng.

| Tình huống | Kết quả cần kiểm tra |
|---|---|
| Code rỗng, chưa Run | Start; hỏi đầu vào/đầu ra, không code |
| Chỉ starter, chưa Run | Start dù starter dài |
| Viết dở code | InProgress; một bước nhỏ, không viết hộ |
| Run lỗi cú pháp | CannotRun; giải thích tiếng Việt; dòng hợp lệ được highlight |
| Run lỗi NameError/TypeError | CannotRun; category từ error sample |
| Submit WA, 0 test pass | LogicError; cấp 1 trước |
| Pass 8/15 | PartialPass; lưới pass/fail, edge case |
| TLE hoặc MLE dù 0 pass | PartialPass, Complexity |
| WA + RuntimeError | CannotRun thắng ưu tiên |
| Sửa code sau lần Run/Submit | Không dùng verdict cũ cho code mới; highlight cũ xóa |
| Trace sample sai | Hướng dẫn suy luận bảng biến, không tuyên bố đã chạy code |
| Trace chỉ test ẩn sai | Không đưa input/expected; yêu cầu ví dụ công khai |
| Prompt injection trong code/đề/question | Không làm theo chỉ thị; mọi trường trả lời qua guard |
| Xin “đưa luôn đáp án” | Gợi mở; không đưa chương trình hoàn chỉnh |
| LLM trả code >3 dòng hoặc giống reference | Regenerate 1 lần; nếu tiếp tục vi phạm trả fallback |
| LLM trả malformed JSON | AI_UNAVAILABLE + hint rule-based; Run/Submit vẫn dùng được |
| LLM timeout, AI service sập | Không chờ quá deadline HTTP 20 giây; hint dự phòng |
| Cấp 3 khi chưa cấp 2 | LEVEL_LOCKED, không gọi Python |
| Cấp tiếp sau 2 lần sai hoặc 3 phút | Cho phép khi đã có cấp trước thành công |
| 6 yêu cầu/phút, 31 yêu cầu/ngày | RATE_LIMIT/QUOTA; thử hai tab/replica để kiểm tra atomicity |
| Học sinh B hoặc ID bài ngoài lớp | 403, không gọi LLM/không ghi interaction |
| Gửi verdict/pass/stage/quota giả trong body | Không dùng các field giả; context dựng lại từ DB/Redis |
| Feedback bằng ID interaction của người khác | 404, không cập nhật |
| Redis cache hỏng | Python bỏ qua cache; Redis quota hỏng thì không cho vượt quota |
| Network SSE cắt giữa JSON | Không coi là hint hoàn tất; báo lỗi để thử lại |
| Mở user A và B, nghe SignalR | Chỉ user đúng nhận AiHint; không phát broadcast |
| Kiểm tra network/log | Không gửi tên/email/MSSV; không có hidden input/expected/stdout/stderr |
| Compose production | AI không có published port; healthcheck chạy; tài nguyên bị giới hạn |

Mọi số liệu accuracy/leakage/cost trong khóa luận cần kèm nguồn dataset, model/prompt version, phương pháp chấm và trạng thái mock/real. Không dùng báo cáo smoke làm benchmark thực tế.
