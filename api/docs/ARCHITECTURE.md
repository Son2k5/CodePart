# Kiến trúc CodePart

Tài liệu liên quan: [Quản lý lớp](CLASS.md) · [Giáo viên và bài tập](TEACHER.md).

## Công nghệ và cấu trúc

Backend dùng ASP.NET Core 8, Clean Architecture, MediatR, FluentValidation, EF Core 8 và Pomelo MySQL. Frontend dùng React, TypeScript, Vite, Tailwind CSS, React Query và Monaco Editor.

Các project nằm trong api/src:

| Project | Trách nhiệm |
| --- | --- |
| Api/CodePath.Api | Endpoint, xác thực, middleware và cấu hình |
| Application/CodePath.Application | Command/query, validation, DTO và interface |
| Domain/CodePath.Domain | Entity và nghiệp vụ |
| Infrastructure/CodePath.Infrastructure | EF Core, MySQL, Redis, JWT và Judge0 |
| Shared/CodePath.Shared.Kernel | Entity cơ sở, Result và kiểu dùng chung |
| Shared/CodePath.Shared.Web | HTTP và xử lý lỗi |

Phụ thuộc: Api → Application/Infrastructure/Shared.Web; Infrastructure → Application; Application → Domain/Shared.Kernel; Domain → Shared.Kernel. Mapping DTO thực hiện thủ công. API thành công trả DTO; lỗi dùng ProblemDetails kèm mã lỗi.

## Dữ liệu và chạy local

Dùng MySQL cài trên máy và Redis. Không dùng PostgreSQL hoặc container database.

| DbContext | Dữ liệu | Bảng lịch sử migration |
| --- | --- | --- |
| AppDbContext | Tài khoản, phiên, lớp, lớp học phần và bài tập cũ | __EFMigrationsHistory |
| JudgeDbContext | Các bảng judge_* phục vụ chấm code | __JudgeMigrationsHistory |

Hai context dùng cùng database MySQL local; GUID lưu Binary(16), thời gian UTC. Ứng dụng không tự migrate khi khởi động thông thường. Từ thư mục api:

~~~powershell
dotnet run --project src/Api/CodePath.Api -- --migrate
dotnet run --project src/Api/CodePath.Api -- --migrate-judge
~~~

Lệnh đầu có seed, cần cấu hình mật khẩu admin; lệnh Judge cần bật Judge. Khi dùng dotnet ef, chỉ rõ --context AppDbContext hoặc JudgeDbContext. Kiểm tra database đích trước khi chạy; không áp dụng cả SQL thủ công và EF migration cho cùng thay đổi.

Các bước HTTPS và khởi động nằm trong [hướng dẫn chạy dự án](../../HUONG_DAN_CHAY_DU_AN.txt). Cấu hình theo api/.env.example, giữ nguyên bí mật trong api/.env hiện có. Frontend mặc định https://localhost:5173, API https://localhost:5001. Để trống VITE_API_BASE_URL để dùng proxy /api; đồng bộ CORS và URL frontend nếu biến môi trường ghi đè appsettings.

Docker dùng hai file độc lập: `api/docker-compose.yml` cho dev (Redis; profile AI/RAG tùy chọn), `api/docker-compose.production.yml` cho production (API/nginx/Redis; profile AI/RAG tùy chọn). MySQL và Judge0 nằm bên ngoài, không được Compose khởi tạo. Không ghép file dev vào production. Xem [COMPOSE.md](COMPOSE.md). /health kiểm tra MySQL/Redis; /health/live kiểm tra tiến trình.

## Xác thực và phiên

- Vai trò Admin, Teacher, Student; policy Active* yêu cầu tài khoản hoạt động.
- Access token mặc định 15 phút; refresh token 7 ngày, giới hạn tuyệt đối 7 ngày.
- Refresh token lưu dạng băm SHA-256, luân chuyển khi refresh; tái sử dụng làm thu hồi họ token.
- Cookie HttpOnly/Secure; mật khẩu BCrypt cost 12.
- Phiên dùng sid và SecurityVersion; hết hạn khi không hoạt động 60 phút.
- Cập nhật hoạt động được tiết lưu 5 phút; polling có SkipActivityTracking không kéo dài phiên.
- Frontend đồng bộ đăng xuất giữa các tab; mã lỗi gồm SESSION_IDLE_TIMEOUT và SESSION_EXPIRED.
- Đăng nhập có rate limit, hàng chờ xử lý và Redis backoff. Chỉ tin forwarded headers từ proxy được cấu hình; không ghi password/token vào log.

## Khung code và chấm bài

Giáo viên/admin tạo bài gắn với đúng một ClassId hoặc CourseId. Học viên chọn bài đã xuất bản của lớp mình và mở khung code dùng chung tại /problems/:id. Đề, starter code, ngôn ngữ và sample thay đổi theo bài; bản nháp tách theo bài/ngôn ngữ.

JudgeAccess kiểm tra phân công và enrollment thực tế trong MySQL. Học viên rời lớp hoặc bị loại sẽ mất quyền xem/chạy/nộp bài.

Run chạy tối đa hai sample hoặc một input tùy chỉnh. Submit lưu snapshot, trả HTTP 202 và đưa vào bounded Channel. Worker quét database để phục hồi job, gọi Judge0 theo từng test và tính điểm theo trọng số.

MySQL có unique key với ActiveSlot để kiểm soát bài đang Pending/Running; lease và token fencing bảo vệ xử lý job. Lỗi tạm thời thử lại tối đa ba lần mỗi test. Ledger (UserId, ProblemId) ngăn thưởng lặp. Outbox phát thông báo ít nhất một lần; LLM chưa triển khai.

Source giới hạn 64 KiB UTF-8; input/output 1 MiB. DTO học viên không lộ dữ liệu hidden test. Markdown đề bài được lọc HTML.

## Judge0

Mẫu cấu hình: api/.env.judge.example và appsettings.Judge.example.json.

~~~dotenv
Judge__Enabled=false
Judge__BaseUrl=http://127.0.0.1:2358/
Judge__ApiToken=
~~~

Chỉ bật Judge khi có dịch vụ Judge0 riêng và đã áp dụng migration. Run/Submit cần dịch vụ này; không chạy code học viên trực tiếp trong tiến trình API.

Giới hạn hiện tại: Judge0 có thể biên dịch lại mỗi test; chưa compile-once. Frontend polling khoảng 1,5 giây, chưa SignalR. Rate limit áp dụng trên từng tiến trình API. Một số lỗi hết bộ nhớ có thể thành RuntimeError. Sandbox của dịch vụ Judge0 bên ngoài chưa được xác minh trong môi trường local này.

## Kiểm tra và tài nguyên frontend

Unit test và kiểm tra kiến trúc nằm trong api/tests/CodePath.Unit.Tests và CodePath.Architecture.Tests. Test Judge có database cần JUDGE_TEST_CONNECTION trỏ vào database MySQL kiểm thử riêng. Integration test dùng Testcontainers MySQL/Redis; không chạy bộ này nếu muốn tránh database Docker.

Script k6 trong api/tests/load có kịch bản NAT, phân tán, tấn công và refresh. Chuẩn bị tài khoản đã xác minh, BASE_URL và các biến đăng nhập theo script. Chưa có kết quả chạy tải để khẳng định năng lực; khi đo cần lưu thông số máy, cấu hình và kết quả.

Landing page: frontend/src/pages/LandingPage.tsx, components và data/content.ts trong features/landing. Giữ anchor và hỗ trợ reduced motion khi chỉnh sửa.

Ảnh codepart-study-group.jpg và codepart-student-login.jpg là ảnh minh họa tạo bằng AI, không đại diện sinh viên/khuôn viên thật. Icon ngôn ngữ từ Devicon v2.16.0; giữ [giấy phép Devicon](../../frontend/public/images/languages/LICENSE.devicon). Nhãn hiệu thuộc chủ sở hữu tương ứng.
