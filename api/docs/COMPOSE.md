# Hai cấu hình Docker Compose

Chỉ còn hai file, mỗi file chạy độc lập:

| Môi trường | File | Mặc định | Profile `ai` | Profile `rag` |
| --- | --- | --- | --- | --- |
| Development | `docker-compose.yml` | Redis | Thêm AI | Thêm AI, Qdrant, indexer |
| Production | `docker-compose.production.yml` | Redis, API, web/nginx | Thêm AI | Thêm AI, Qdrant, indexer |

Không ghép hai file bằng nhiều cờ `-f`. MySQL và Judge0 vẫn là dịch vụ bên ngoài; Compose không tạo database hay chạy migration. Development giữ cách chạy .NET/Vite trên host để debug/hot reload.

## Development

Chạy các lệnh từ thư mục `api`. Dùng `.env` hiện tại, bổ sung biến thiếu từ `.env.example`; không ghi đè khóa và connection string đã có.

```powershell
# Redis; .NET và Vite chạy trên host như trước
docker compose up -d

# Redis + AI (tùy chọn)
docker compose --profile ai up -d --build

# Redis + AI + Qdrant + indexer (tùy chọn)
docker compose --profile rag up -d --build

# Hoặc chỉ Qdrant nếu chạy Python trên host
docker compose up -d qdrant
```

Các cổng chỉ bind `127.0.0.1`: Redis 6379, AI 8000, indexer 8001, Qdrant 6333. Có thể đổi bằng `REDIS_PORT`, `AI_PORT`, `AI_INDEXER_PORT`, `QDRANT_PORT`; cập nhật URL của API trên host tương ứng.

Để .NET dùng AI/RAG:

- `Ai__Enabled=true`; `Ai__BaseUrl=http://127.0.0.1:8000/`.
- Container lấy `SERVICE_API_KEY` từ `AI_SERVICE_KEY`, hoặc `Ai__ServiceKey` nếu biến đầu chưa có. Giữ khóa API và Python trùng nhau. Mẫu dev có khóa local mặc định, không dùng ở production.
- `Ai__AnonymizationKey` là khóa khác, ít nhất 32 ký tự.
- RAG: `Rag__Enabled=true`; `Rag__IndexBaseUrl=http://127.0.0.1:8001/`.
- Điền provider/model/key LLM. RAG cần thêm endpoint/model/dimensions/key embedding. Một container healthy không chứng minh tài khoản LLM/embedding đã hoạt động.
- Từ container, dùng `host.docker.internal` để gọi LLM/embedding trên host; không dùng `localhost`. Từ .NET chạy trên host, dùng URL localhost như trên.
- AI container kết nối Redis qua `redis:6379`, không qua cổng host đã publish.

## Production

Chuẩn bị `.env.production` từ `.env.production.example` và các file secrets/TLS được khai báo trong Compose. File production tự có image, volume, network và healthcheck Redis; không còn phụ thuộc file dev.

| Chế độ | Biến trong `.env.production` |
| --- | --- |
| Cơ bản | `COMPOSE_PROFILES=`; `AI_ENABLED=false`; `RAG_ENABLED=false` |
| AI | `COMPOSE_PROFILES=ai`; `AI_ENABLED=true`; `RAG_ENABLED=false` |
| AI + RAG | `COMPOSE_PROFILES=rag`; `AI_ENABLED=true`; `RAG_ENABLED=true` |

Profile chọn **container**; feature flag chọn **tính năng .NET**. Đặt cả hai theo bảng. Có thể dùng `--profile ai`/`--profile rag` thay `COMPOSE_PROFILES`, nhưng vẫn phải đặt feature flags.

```powershell
docker compose --env-file .env.production -f docker-compose.production.yml config --quiet
docker compose --env-file .env.production -f docker-compose.production.yml up -d --build
docker compose --env-file .env.production -f docker-compose.production.yml ps
```

Chỉ nginx publish 80/443. Redis có mật khẩu, API/AI/indexer/Qdrant ở mạng `backend`. Python giữ chế độ non-root, filesystem chỉ đọc, tmpfs, giới hạn CPU/RAM/PID và healthcheck.

Khi bật AI, điền `AI_SERVICE_KEY`, `AI_ANONYMIZATION_KEY`, `AI_REDIS_URL`, `LLM_MODEL`, `LLM_ADVANCED_MODEL` và cấu hình provider. `AI_REDIS_URL` dạng `redis://:URL_ENCODED_PASSWORD@redis:6379/0`, mật khẩu phải trùng `secrets/redis_password.txt`.

Khi bật RAG, điền `QDRANT_API_KEY`, `EMBEDDING_BASE_URL`, `EMBEDDING_MODEL`, `EMBEDDING_DIMENSIONS` đúng model và khóa embedding nếu dịch vụ yêu cầu. AI và indexer dùng cùng cấu hình embedding. Qdrant production từ chối khởi động khi chưa có API key; AI/indexer kiểm tra các biến bắt buộc khi khởi động profile, nên deployment cơ bản không bị buộc cung cấp khóa cho tính năng chưa dùng.

## Dữ liệu và chuyển từ cấu hình cũ

- Giữ tên volume logic cũ: `codepath-redis-data`, `qdrant-local-data` (dev), `qdrant-data` (production).
- Giữ cùng Compose project name và thư mục `api` khi cập nhật stack hiện có để Docker tiếp tục dùng volume trước đó. Nếu deployment cũ dùng `-p`, tiếp tục dùng đúng giá trị đó.
- Không có lệnh `down -v`, xóa volume hoặc migration trong thay đổi này.
- Dev và production không dành để chạy đồng thời trên cùng project/Docker host: container Redis hiện có vẫn giữ tên `codepath-redis`.
- Với stack production trước đây dùng nhiều overlay, thay lệnh bằng file production duy nhất và chọn profile tương ứng. Dữ liệu DB/MySQL bên ngoài không bị Compose quản lý.

## Kiểm tra và dừng

```powershell
# Kiểm tra cả sáu tổ hợp: dev/prod × cơ bản/AI/RAG, không cần Docker daemon
python tests/compose/test_config.py

# Dừng dev, gồm các profile tùy chọn, giữ volume
docker compose --profile "*" stop

# Dừng production, giữ volume
docker compose --env-file .env.production -f docker-compose.production.yml --profile "*" stop
```

Kiểm tra `config` xác nhận cấu trúc, nội suy biến, dịch vụ và mạng; không thay thế chạy thật với MySQL, Judge0, chứng chỉ, SMTP và tài khoản LLM/embedding của môi trường đích.

Tham khảo cách bật profile và dependency: [Docker Compose profiles](https://docs.docker.com/compose/how-tos/profiles/), [services / depends_on](https://docs.docker.com/reference/compose-file/services/#depends_on).
