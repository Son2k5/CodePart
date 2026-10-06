# CodePath API

Backend ASP.NET Core 8 dùng Clean Architecture theo các layer thuần, EF Core 8,
MySQL 8.4 (Pomelo), Redis và Judge0.

## Chạy local từ database sạch

1. Bảo đảm dịch vụ MySQL cài trực tiếp trên máy đang chạy. Môi trường development
   không khởi động MySQL bằng Docker.

   ```powershell
   Get-Service MySQL93
   ```

2. Tạo cấu hình local từ `.env.example` và điền tài khoản MySQL local. Connection
   string phải giữ `GuidFormat=Binary16;DateTimeKind=Utc`.

   ```powershell
   Copy-Item .env.example .env
   ```

3. Tạo hoặc cập nhật database local bằng EF Core migration:

   ```powershell
   dotnet restore CodePath.sln
   dotnet ef database update `
     --project src/Infrastructure/CodePath.Infrastructure/CodePath.Infrastructure.csproj `
     --startup-project src/Api/CodePath.Api/CodePath.Api.csproj
   ```

4. Nếu cần Redis local bằng Docker, chỉ khởi động riêng Redis. Lệnh này không có
   và không được khởi động container MySQL:

   ```powershell
   docker compose up -d redis
   docker compose ps
   ```

5. Chạy API:

   ```powershell
   dotnet run --project src/Api/CodePath.Api/CodePath.Api.csproj
   ```

API tự chạy migration `InitialCreate` và seed bài tập mẫu. Hai tài khoản
`admin01`/`admin02` chỉ được seed khi `ADMIN01_PASSWORD` và `ADMIN02_PASSWORD`
đã được cấu hình và thỏa chính sách mật khẩu.

Kiểm tra readiness tại `GET /health`; response phải có hai check `mysql` và
`redis` ở trạng thái `healthy`.

## Kiểm thử

```powershell
dotnet test CodePath.sln
```

Integration tests dùng Testcontainers để tạo MySQL 8.4 và Redis độc lập, áp dụng
migration thật, rồi xóa container sau khi chạy.

> Không chạy integration tests trong môi trường local nếu không muốn Testcontainers
> khởi động container MySQL phục vụ kiểm thử.

## Docker production

Production Compose dựng Redis có xác thực, API và Nginx phục vụ frontend qua HTTPS.
MySQL là dịch vụ bên ngoài Compose; cấu hình `DATABASE_HOST`, `DATABASE_PORT`,
`DATABASE_NAME` và `DATABASE_USER` phải trỏ tới MySQL mà máy chạy Docker có thể truy cập.
Password, JWT key và TLS private key được mount dưới dạng Docker secrets, không truyền
qua environment.

1. Tạo cấu hình và thư mục secrets:

```powershell
Copy-Item .env.production.example .env.production
New-Item -ItemType Directory -Force secrets, secrets/tls
```

2. Tạo từng file được khai báo ở cuối `docker-compose.production.yml`. JWT key
phải là ít nhất 64 byte ngẫu nhiên mật mã; không dùng UUID, mật khẩu người dùng
hay giá trị mẫu. Ví dụ nếu máy có OpenSSL:

```powershell
openssl rand -base64 64 | Set-Content -Encoding ascii -NoNewline secrets/jwt_signing_key.txt
openssl rand -base64 48 | Set-Content -Encoding ascii -NoNewline secrets/database_password.txt
openssl rand -base64 48 | Set-Content -Encoding ascii -NoNewline secrets/redis_password.txt
```

Điền riêng `smtp_password.txt`, hai mật khẩu admin và đặt certificate tại
`secrets/tls/fullchain.pem`, private key tại `secrets/tls/privkey.pem`. Toàn bộ
thư mục `secrets/` đã bị Git ignore.

3. Kiểm tra cấu hình rồi khởi động:

```powershell
docker compose --env-file .env.production -f docker-compose.yml -f docker-compose.production.yml config --quiet
docker compose --env-file .env.production -f docker-compose.yml -f docker-compose.production.yml up -d --build
```

Chỉ Nginx publish cổng `80/443`; Redis và API nằm trong network nội bộ. MySQL không
được Docker Compose tạo hoặc quản lý.
Nginx chuyển HTTP sang HTTPS, đặt security headers và chỉ proxy `/api` cùng các
health endpoint. API chỉ tin `X-Forwarded-*` từ subnet proxy nội bộ đã cấu hình.

## Migration

Migration PostgreSQL cũ được lưu trữ tại
`database/migrations-archive/postgresql` và không còn được compile/chạy. Migration
MySQL đang hoạt động nằm tại
`src/Infrastructure/CodePath.Infrastructure/Persistence/Migrations`.
