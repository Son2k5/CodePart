# CodePath RAG

## Đã triển khai

- Trang `/classes/:classId/learning`: tạo chương/Topic, soạn Markdown, tải PDF qua Asset Service hiện có, lưu nháp, công bố, xem trạng thái indexing và yêu cầu xử lý lại.
- Sinh viên mở học liệu của lớp đã tham gia, tìm kiếm ngữ nghĩa, hỏi đáp và mở nguồn tham khảo qua API kiểm tra quyền. Lớp tham gia bằng mã cũng xuất hiện ở mục học liệu trong `/classes`.
- Gợi ý tại `/problems/:id` truy xuất lý thuyết từ các Topic đã gắn bài tập, chỉ khi sinh viên có quyền đọc và Topic đã mở khóa. Giữ nguyên quota, các cấp hint, guard và phát SSE sau kiểm tra. Khi retrieval lỗi, dùng pipeline hint cũ.
- Luồng riêng cho giảng viên: sinh bản nháp một bài lập trình hoặc 3–5 câu quiz, chỉnh sửa, kiểm chứng lời giải/test qua Judge0, xác nhận rà soát và xuất dữ liệu. Đáp án và lời giải chỉ ở bảng bản nháp, không được đưa vào Qdrant.
- Transactional outbox trong MySQL, job có lease/retry, vector gắn với revision, dọn revision cũ, nguồn bị thu hồi không còn được trả về dù chưa dọn vector.
- Indexer Python riêng trong Compose để bóc tách PDF không chiếm tài nguyên tiến trình phục vụ gợi ý.

Đây là indexing/RAG, **không huấn luyện lại hoặc fine-tune LLM**. Chưa có tài liệu thật được nạp và chưa có số đo chất lượng thực nghiệm. Vector retrieval là cấu hình ban đầu; hybrid, reranker, OCR, đọc DOCX và transcript video chưa được bật trong bản này. Quiz đã duyệt được xuất thành JSON; không tự tạo một hệ thống thi quiz mới.

## Điều kiện chạy

Giữ các cấu hình MySQL, Redis, tài khoản và Judge0 của dự án. Cần thêm:

1. Qdrant (Compose ghim `v1.17.1`).
2. Dịch vụ embedding có API `POST <EMBEDDING_BASE_URL>/embeddings`, nhận `{model,input:[...]}`, trả `{data:[{index,embedding:[...]}]}`. Có thể là máy inference cục bộ hoặc dịch vụ hosted tương thích. Không tự tải model lớn vào container AI 768 MB.
3. LLM hiện có được cấu hình và hỗ trợ schema JSON như trước.
4. Cloudinary nếu dùng upload PDF. Markdown không cần Cloudinary.

`EMBEDDING_MODEL` và `EMBEDDING_DIMENSIONS` phải khớp model thực tế. Giá trị `1024` trong file mẫu chỉ là cấu hình gợi ý, không phải giá trị áp dụng cho mọi model. `EMBEDDING_API_KEY` độc lập với `LLM_API_KEY`. Bản triển khai không tự chọn hay mua dịch vụ.

## Migration

Hai migration mới của `AppDbContext`:

- `LearningRagOutbox`: `learning_rag_jobs`.
- `LearningRagDrafts`: `learning_rag_drafts`.

Chạy sau khi trỏ đúng database và chuẩn bị sao lưu theo quy trình của dự án:

```powershell
cd api
dotnet run --project src/Api/CodePath.Api -- --migrate
```

Migration chưa được áp dụng vào database của bạn trong lần triển khai mã nguồn này. Phải áp dụng trước khi dùng bản API mới để sửa/công bố Topic: outbox được ghi cùng transaction kể cả khi worker đang tắt. Không xóa bảng hoặc chạy `EnsureCreated` trên database hiện hữu.

Có thể xem trước SQL trong [`api/database/rag-migration.sql`](../database/rag-migration.sql). Script này dành cho database đã áp dụng `LectureDelivery`, không phải script khởi tạo toàn bộ dự án và không chạy lặp lại tùy ý.

## Chạy local

Các file `.env.example` là mẫu; Python không tự đọc chúng. Thiết lập biến môi trường trong từng terminal hoặc cấu hình công cụ khởi chạy của bạn. Không đưa khóa thật vào frontend/git.

Terminal Qdrant, đặt `QDRANT_API_KEY` rồi chạy:

```powershell
cd api
docker compose up -d qdrant
```

Terminal Python, đặt các biến trong `ai-service/.env.example`:

```text
SERVICE_API_KEY=<khóa nội bộ ít nhất 32 ký tự>
QDRANT_URL=http://127.0.0.1:6333
QDRANT_API_KEY=<khóa Qdrant>
EMBEDDING_BASE_URL=<base URL kết thúc bằng /v1>
EMBEDDING_MODEL=<model trên dịch vụ embedding>
EMBEDDING_DIMENSIONS=<số chiều thực tế>
EMBEDDING_API_KEY=<nếu dịch vụ yêu cầu>
LLM_PROVIDER=<provider hiện có>
LLM_MODEL=<model hỗ trợ schema>
LLM_API_KEY=<khóa provider>
```

```powershell
cd ai-service
# Dùng uv đã cài hoặc chạy python -m uv nếu uv nằm trong virtualenv.
uv sync --locked --extra test --extra training
.venv/Scripts/python -m uvicorn app.main:app --host 127.0.0.1 --port 8000
```

Khởi chạy một terminal Python thứ hai với cùng cấu hình RAG/service key cho indexer:

```powershell
cd ai-service
.venv/Scripts/python -m uvicorn app.main:app --host 127.0.0.1 --port 8001 --limit-concurrency 4
```

Terminal .NET, bổ sung:

```text
Ai__Enabled=true
Ai__BaseUrl=http://127.0.0.1:8000/
Ai__ServiceKey=<cùng SERVICE_API_KEY>
Ai__AnonymizationKey=<khóa riêng ít nhất 32 ký tự>
Rag__Enabled=true
Rag__IndexBaseUrl=http://127.0.0.1:8001/
```

Chạy API và frontend như hiện tại. Máy phát triển ít tài nguyên có thể trỏ `Rag__IndexBaseUrl` về cổng 8000 để dùng chung tiến trình, nhưng xử lý PDF khi đó có thể ảnh hưởng latency.

## Chạy Compose

Điền `.env.production` theo `api/.env.production.example`, đặt `COMPOSE_PROFILES=rag`, `AI_ENABLED=true`, `RAG_ENABLED=true` cùng các khóa/model AI và embedding. Nếu embedding chạy trên host Windows, dùng hostname truy cập được từ container, ví dụ `host.docker.internal`; `localhost` bên trong container trỏ về chính container.

```powershell
cd api
docker compose --env-file .env.production -f docker-compose.production.yml config --quiet
docker compose --env-file .env.production -f docker-compose.production.yml up -d --build
```

File production độc lập không publish cổng Qdrant, Python hoặc indexer. Qdrant có API key. API gọi Python bằng `X-Service-Key`. Tệp PDF chỉ tải từ host storage được cấu hình, không follow redirect, không kèm khóa AI. Cloudinary hiện sinh signed URL từ `api.cloudinary.com`; allowlist mặc định chứa host này và `res.cloudinary.com`.

Development có thể chạy cả Redis, AI, Qdrant và indexer bằng `docker compose --profile rag up -d --build` trong `api`, thay cho các terminal Python local. Không chạy đồng thời Python local và container trên cùng cổng 8000/8001. Xem [COMPOSE.md](COMPOSE.md).

## Nạp tài liệu của bạn

1. Giáo viên/admin mở lớp → **Học liệu và trợ giảng**.
2. Tạo chương và bài học; thêm văn bản Markdown hoặc PDF tối đa 20 MB.
3. **Lưu và công bố**. Upload xong chưa đồng nghĩa tài liệu đã được công bố.
4. Theo dõi **Trợ giảng học liệu**: Pending → Processing → Ready, hoặc Failed với mã lỗi.
5. Với Topic đã công bố trước khi thêm RAG, bấm **Xử lý lại học liệu** để backfill. Không tự động nạp tài liệu ở mọi lớp vào dịch vụ AI.
6. Khi Ready, thử tìm kiếm và hỏi đáp. Muốn dùng trong hint, gắn bài tập vào Topic bằng chức năng quản lý bài tập/endpoint `PUT /api/topics/{id}/exercises` đã có. Hints chỉ tìm trong các Topic có liên kết này.

Nên gửi tài liệu kèm tên môn, chương/Topic, lớp được phép dùng, ngôn ngữ lập trình, và phân biệt tài liệu sinh viên với đáp án dành riêng cho giảng viên. Không đưa lời giải hoàn chỉnh hoặc test ẩn vào bài giảng công khai rồi kỳ vọng RAG tự biến chúng thành dữ liệu riêng tư.

Chunking theo heading/đoạn, mục tiêu khoảng 1.800 ký tự và trần 6.000 ký tự, **không phải cam kết 500 token**. Code fence không bị cắt đôi: nếu quá dài, job báo `CHUNK_TOO_LARGE` để tác giả chia lại. PDF giữ số trang vật lý; không suy đoán số trang in trong nội dung. PDF trống/scan hoàn toàn trả `PDF_NEEDS_OCR`; PDF hỗn hợp chỉ có những trang đọc được văn bản. Giới hạn 500 trang, 2 triệu ký tự mỗi PDF, 1.500 chunks mỗi Topic. Video/hình ảnh không có transcript/OCR sẽ không được index.

## Dòng đời và phân quyền

- Revision hiện tại sử dụng `Topic.RowVersion`; thay đổi draft/metadata cũng có thể tạo job mới cho snapshot đã công bố. Đây là lựa chọn thận trọng, có thể tốn thêm embedding. Truy vấn không dùng revision cũ trong lúc revision mới chưa Ready.
- Worker đọc snapshot đã công bố, xử lý bất đồng bộ; cập nhật Topic và outbox được lưu cùng `SaveChangesAsync`.
- Job chỉ thành Ready sau khi tất cả chunks được ghi thành công. Partial writes không được truy vấn vì .NET chỉ truyền scope Ready.
- Job nhận lease 5 phút, có token sở hữu; deadline index 250 giây và Python 240 giây; tối đa 5 lần thử tự động. Lỗi tài liệu được đánh dấu Failed ngay. Có nút retry thủ công.
- Scope luôn là cặp `topicId + revision`, không phải hai danh sách lọc độc lập. Không có quy tắc `Guid.Empty = public`.
- Sinh viên phải đang đăng ký lớp active, Topic công bố và thỏa lịch/điều kiện mở khóa. Giáo viên phải đang quản lý lớp hoặc có quyền template tương ứng.
- Kiểm tra lại quyền/phiên bản sau retrieval và sau generation, kể cả hint lấy từ cache. Khi thay đổi trả `CONTEXT_CHANGED` hoặc không dùng nguồn cũ.
- Xóa/thu hồi quyền có hiệu lực qua database trước khi garbage collection vector chạy. Collection cũ sau khi đổi embedding cần được dọn theo quy trình vận hành; không tự xóa toàn bộ collection.
- Identity collection gồm endpoint embedding, model, số chiều và version chunker. Hai tiến trình Python phải dùng cùng cấu hình. Sau thay đổi model/endpoint, reindex từng Topic; trạng thái Ready cũ trong MySQL không chứng minh collection mới đã có dữ liệu.
- Cache hint bao gồm các đoạn theory và revision. Q&A hiện chưa cache, tránh dùng câu trả lời cũ sau thay đổi quyền.

## API

Mọi endpoint bên ngoài đi qua .NET và yêu cầu xác thực.

| Endpoint | Vai trò |
|---|---|
| `GET /api/learning/classes` | Liệt kê lớp active sinh viên đang tham gia |
| `POST /api/topics/{id}/rag/search` | `{question}`; trả những đoạn phù hợp đã kiểm tra quyền |
| `POST /api/topics/{id}/rag/ask` | `{question}`; câu trả lời và citation đã xác thực ID |
| `GET /api/topics/{id}/rag/sources/{blockId}?version=N` | Kiểm tra quyền rồi trả Markdown hoặc signed PDF URL |
| `GET /api/topics/{id}/rag/status` | Người quản lý lớp xem trạng thái |
| `POST /api/topics/{id}/rag/reindex` | Người quản lý yêu cầu indexing/backfill |
| `GET/POST /api/topics/{id}/rag/drafts` | Xem hoặc tạo bản nháp dành cho giảng viên |
| `PUT /api/topics/{id}/rag/drafts/{draftId}` | Chỉnh sửa với `rowVersion`; xóa trạng thái kiểm chứng/duyệt cũ |
| `POST .../drafts/{draftId}/verify` | Chạy reference qua Judge0, 2 giây CPU/128 MB mỗi test |
| `POST .../drafts/{draftId}/review` | `{rowVersion}`; bài lập trình phải qua verify |

Search/ask/generate/verify có quota Redis chung theo user: 5 lượt/phút, 50 lượt/ngày UTC. Hint giữ quota riêng hiện tại. Luồng verify chỉ cho một yêu cầu đang chạy mỗi user, deadline toàn bộ 3 phút. Không chạy code trong Python hay API process.

Q&A trả `groundingStatus=sources_provided` khi có source IDs hợp lệ; đây **không** phải chứng minh mọi mệnh đề đều đúng. Không có nguồn phù hợp thì `insufficient_context`, không gọi LLM nếu retrieval rỗng. Hint hiển thị “học liệu tham khảo” cho các đoạn truy xuất; không giả vờ có citation chính xác từng câu.

Luồng giảng viên lưu `Draft` → verify → `Reviewed`. Không tự công bố: giảng viên dùng đề và bộ test đã xuất để nhập vào ngân hàng bài tập hiện có, sau đó xuất bản. Duyệt quiz chỉ xác nhận người quản lý đã rà soát, không có kiểm chứng tự động về tính đúng của đáp án. Việc lời giải chạy qua test do cùng LLM sinh cũng chưa chứng minh đề đúng; giảng viên cần kiểm tra thêm các trường hợp độc lập.

## Kiểm thử và đánh giá sau khi có tài liệu

```powershell
cd ai-service
.venv/Scripts/python -m pytest -q
.venv/Scripts/python -m eval.run_rag data/rag-eval.jsonl --output reports/rag.json
```

Mỗi dòng của tập đánh giá có cấu trúc:

```json
{"query":"Câu hỏi được giảng viên gán nhãn","scopes":[{"topicId":"UUID thực","revision":"UUID thực"}],"relevantDocumentIds":["UUID tài liệu liên quan"]}
```

`documentId` là `blockId` với Markdown, `assetId` với PDF. Dùng `relevantDocumentIds: []` cho câu hỏi không có nguồn hỗ trợ. Script gọi API nội bộ bằng `SERVICE_API_KEY`, đo recall@5/MRR trên document ID, độ chính xác trả rỗng, latency p50/p95 và báo riêng số lỗi. Các metric chất lượng chỉ tính trên request thành công; luôn đọc cùng error count. Không log nội dung tài liệu, câu hỏi hay khóa trong báo cáo.

```powershell
# Từ root
dotnet test api/tests/CodePath.Unit.Tests
dotnet test api/tests/CodePath.Architecture.Tests
npm run build --prefix frontend
node --test frontend/tests/assistant-stream.test.mjs
```

Các test hiện có bao phủ cặp topic/revision, cross-scope rejection, cache version, trích dẫn không tồn tại, URL/redirect PDF, tài liệu scan, outbox cùng save, quyền lớp/lịch mở khóa và thu hồi giáo viên trong cùng request. Test authorization dùng EF InMemory; không thay thế integration test MySQL/Redis/Qdrant thật. Docker Desktop chưa chạy trong môi trường triển khai mã nguồn nên chưa thực hiện E2E thật, migration database hay gọi provider trả phí.

Nguồn kỹ thuật: [Qdrant upsert](https://api.qdrant.tech/api-reference/points/upsert-points), [delete theo filter](https://api.qdrant.tech/api-reference/points/delete-points), [payload/filter index](https://qdrant.tech/documentation/manage-data/payload/).
