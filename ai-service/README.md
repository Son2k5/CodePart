# CodePath AI Assistant

RAG học liệu, cấu hình embedding/Qdrant, migration, giao diện và quy trình nạp tài liệu: [Hướng dẫn RAG](../api/docs/RAG.md).

Python chỉ phân tích văn bản/AST, không thực thi code học sinh. Judge0 là nơi duy nhất chạy code. Luồng tích hợp hiện tại là **Code Judge** (`/problems/:id`, Monaco), không phải màn hình Exercise cũ.

## Chạy local

Python 3.11+, .NET 8 SDK, Node theo frontend hiện có, MySQL và Redis của dự án. Tại thư mục `ai-service`:

`uv.lock` khóa dependency trên các phiên bản Python hỗ trợ. Khuyến nghị cài `uv==0.13.1` và chạy `uv sync --python 3.11 --locked --extra test --extra training`; Docker cũng dùng lock này. Cách pip dưới đây phù hợp thử nhanh nhưng có thể resolve version khác lock.

```powershell
py -3.11 -m venv .venv
.venv/Scripts/python -m pip install -e ".[test,training]"
$env:SERVICE_API_KEY = "<khóa nội bộ ngẫu nhiên ít nhất 32 ký tự>"
$env:LLM_PROVIDER = "openai"
$env:LLM_API_KEY = "<khóa nhà cung cấp>"
$env:LLM_MODEL = "<model hỗ trợ JSON schema>"
$env:LLM_ADVANCED_MODEL = "<model cho LogicError hoặc cấp 3>"
$env:REDIS_URL = "redis://localhost:6379/0"
.venv/Scripts/python -m uvicorn app.main:app --host 127.0.0.1 --port 8000
```

Không có khóa LLM thì backend vẫn trả hint rule-based khi gọi dịch vụ thất bại. Không có service key hợp lệ thì Python từ chối khởi động. `.env.example` chỉ là mẫu; Python không tự đọc `.env`.

Ở terminal API, bổ sung các biến trong `api/.env.ai.example` vào cấu hình hiện có. `Ai__ServiceKey` phải trùng `SERVICE_API_KEY`; `Ai__AnonymizationKey` phải là khóa riêng. Bật `Judge__Enabled` và cấu hình Judge0 như hiện tại. Áp dụng migration trên database đích sau khi đã kiểm tra connection:

```powershell
cd api
dotnet run --project src/Api/CodePath.Api -- --migrate-judge
dotnet run --project src/Api/CodePath.Api
```

Migration `20261016030551_AiAssistantInteractions` tạo bảng `ai_interactions`. Không tự chạy migration trong quá trình triển khai mã nguồn này.

Frontend dùng API hiện có, không chứa LLM key. Chạy `npm run dev --prefix frontend` từ root. Đăng nhập học sinh đã enroll vào lớp/course của bài Published, mở `/problems/:id`.

## Contract và xử lý lỗi

- Contract Python: `openapi.json`, hoặc `/openapi.json` trên service nội bộ; header `X-Service-Key`.
- `POST /api/ai/assist`: chỉ nhận `problemId`, `language`, `sourceCode`, `type`, `level`, `question`. Không nhận stage/verdict/quota từ client.
- JSON response .NET: `{ interactionId, hint, remaining, unlockedLevel, code }`; `hint` giữ schema Python.
- `Accept: text/event-stream` trả SSE `delta`, rồi `result`. **Chỉ stream sau khi toàn bộ hint qua guard**, không phát token LLM chưa kiểm tra.
- `POST /api/ai/{interactionId}/feedback` nhận `{ helpful: true|false }`, chỉ chủ sở hữu được ghi.
- SignalR `/api/ai/hub` phát `AiHint` cho user ID đã xác thực. Client có thể dùng bearer header với LongPolling; frontend hiện dùng SSE cho yêu cầu trực tiếp.
- `INVALID_CONTEXT`: 422 ở Python; input .NET không hợp lệ: 400. `AI_UNAVAILABLE`: 503 ở Python, .NET chuyển thành hint dự phòng và `code=AI_UNAVAILABLE`.
- Guard chặn hai lần: Python trả HTTP 200 với `guardStatus=fallback`, header `X-AI-Code=GUARD_FALLBACK`; .NET trả thêm `code=GUARD_FALLBACK`.
- `QUOTA`/`RATE_LIMIT`: 429; `LEVEL_LOCKED`: 409; không enroll: 403. Redis lỗi không cho vượt quota; core Run/Submit không phụ thuộc AI hoạt động.
- Deadline Python 19 giây; HttpClient .NET 20 giây, bao gồm retry. Circuit breaker và retry chỉ cho lỗi tạm thời. Frontend 25 giây.

Quota hiện tại: 30 hint/ngày UTC, 5/phút, một yêu cầu đang chạy/học sinh. Redis Lua kiểm tra và tăng bộ đếm nguyên tử trên nhiều replica. Hint dự phòng cũng tiêu thụ lượt. L2/L3 yêu cầu đã nhận cấp trước thành công và có ≥2 lần submit sai sau hint gần nhất hoặc chờ ≥3 phút. Các ngưỡng nằm ở `AiPolicy`.

Backend lấy submission của chính học sinh từ DB, chỉ dùng verdict nếu code/ngôn ngữ khớp. Kết quả Run do server lưu Redis một giờ, khóa bằng hash code; kết quả mới hơn được ưu tiên. Dữ liệu test ẩn, stdout/stderr của test ẩn không gửi Python. Source code có thể tự chứa thông tin cá nhân do người học nhập: không thêm email/tên/MSSV vào contract; `studentRef` là HMAC ID.

## Stage, prompt, guard và cache

Ưu tiên: CannotRun > PartialPass > LogicError > InProgress > Start. Accepted hoàn toàn không phải lỗi, được xếp InProgress. ML không được đổi stage rule-based; bổ sung confidence/category. Các nhãn chi tiết như OffByOne cần annotation thật, không được suy diễn từ mọi WrongAnswer.

`prompts/strategies.yaml` có 15 chiến lược (5 stage × 3 cấp); `system.jinja` có chỉ thị tiếng Việt, delimiter dữ liệu và TraceTestCase. Prompt version gồm tên + SHA256. Reference solution chỉ dùng tại guard, không đưa generator. Hidden input/expected/actual bị loại khỏi prompt. Prompt giới hạn 100 KB; server cắt ngắn đề và sample trước khi gửi.

Guard kiểm tra mọi trường message/steps/question: code block tổng cộng >3 dòng, code ở Start/InProgress, chương trình có stdin/stdout hoặc main, AST Python/token Java/C++ chuẩn hóa identifier, giống reference, dữ liệu test ẩn và dòng nghi ngờ ngoài phạm vi. Vi phạm được log bằng request ID/lý do, không log nội dung code. Regenerate đúng một lần, sau đó hint mẫu theo error category.

Guard là heuristic, không chứng minh không rò rỉ bằng diễn giải/paraphrase. Cần đánh giá độc lập và giáo viên kiểm tra. Runtime không có reference nếu bài trong DB không cung cấp reference; trường này vẫn có trong contract cho nguồn dữ liệu sau này.

Redis cache 10 phút, hash toàn bộ ngữ cảnh có ý nghĩa: problem, stage, level, category, code, question, lịch sử, verdict, prompt/model version. Cache hỏng được bỏ qua; cache hit được kiểm tra guard lại và ghi token usage bằng 0 cho lần gọi mới.

Tín hiệu stuck dùng chuỗi submit sai, code lặp ở các lần hỏi và khoảng thời gian giữa các lần hỏi cùng code. Đây chưa phải đo toàn bộ thời gian editor idle; AutoHint tự động là phần tùy chọn chưa bật.

## Provider

`LLM_PROVIDER`: `openai`, `gemini`, `anthropic`, `localvllm`. `LLM_BASE_URL` phải đúng provider:

| Provider | Base URL mặc định khi không đặt biến |
|---|---|
| OpenAI | `https://api.openai.com/v1` |
| Gemini | `https://generativelanguage.googleapis.com/v1beta` |
| Anthropic | `https://api.anthropic.com/v1` |
| LocalVLLM | Cần đặt URL server tương thích OpenAI |

OpenAI/LocalVLLM dùng JSON schema; Gemini dùng `responseJsonSchema`; Anthropic ép gọi tool `hint`. Mọi kết quả đều parse lại bằng Pydantic. Các adapter đã được kiểm thử bằng HTTP mock, chưa gọi tài khoản LLM thật. LocalVLLM phải hỗ trợ structured output/max_completion_tokens của adapter.

Tài liệu đã đối chiếu: [OpenAI structured outputs](https://developers.openai.com/api/docs/guides/structured-outputs), [Gemini structured outputs](https://ai.google.dev/gemini-api/docs/structured-output), [Anthropic tools](https://platform.claude.com/docs/en/agents-and-tools/tool-use/define-tools).

## Docker

Từ `api`, điền `.env.production` theo `.env.production.example`, đặt `COMPOSE_PROFILES=ai`, `AI_ENABLED=true` và các khóa/model AI rồi chạy:

```powershell
docker compose --env-file .env.production -f docker-compose.production.yml config --quiet
docker compose --env-file .env.production -f docker-compose.production.yml up -d --build
```

AI chạy non-root, filesystem chỉ đọc, 1 CPU/768 MB, healthcheck, không publish cổng; dùng network backend và Redis chung. Network cần egress để gọi LLM API. `AI_REDIS_URL` phải chứa thông tin xác thực phù hợp Redis production. Mount model directory read-only và đặt `MODEL_PATH` nếu bật classifier.

Development có thể dùng `docker compose --profile ai up -d --build` trong `api` để chạy Redis + AI (AI mở ở `127.0.0.1:8000`); .NET vẫn chạy trên host. Profile `rag` thêm Qdrant và indexer. Xem [hai cấu hình Compose](../api/docs/COMPOSE.md). Không ghép file dev vào production.

## Kiểm thử và tài liệu tiếp theo

```powershell
# Trong ai-service
.venv/Scripts/python -m pytest -q
.venv/Scripts/python -m eval.run --mock
.venv/Scripts/python -m training.smoke
# Từ root
dotnet test api/tests/CodePath.Unit.Tests
dotnet test api/tests/CodePath.Architecture.Tests
node --test frontend/tests/assistant-stream.test.mjs
npm run build --prefix frontend
```

`training.smoke` tạo model giả lập ở `data/smoke`; chỉ chạy vào thư mục sạch, không tự ghi đè version. Không dùng model này làm kết quả khóa luận hoặc deployment.

- [Training và registry](training/README.md)
- [Evaluation và chấm tay](eval/README.md)
- [Danh sách file, trạng thái và checklist nghiệm thu](../api/docs/AI_ASSISTANT.md)
