# C?p nh?t th?ng tin User

## Ch?y v? c?u h?nh

T? th? m?c `api`:

```powershell
dotnet ef database update --project src/Infrastructure/CodePath.Infrastructure/CodePath.Infrastructure.csproj --startup-project src/Api/CodePath.Api/CodePath.Api.csproj --context AppDbContext
dotnet test tests/CodePath.Unit.Tests/CodePath.Unit.Tests.csproj --filter FullyQualifiedName~CodePath.Unit.Tests.Users
```

Migration m?i: `AddUserProfile`. Kh?ng t? ??ng ?p d?ng v?o database ?ang d?ng trong qu? tr?nh tri?n khai t?nh n?ng n?y.
C?u h?nh connection MySQL 8 qua `ConnectionStrings__Database`; provider Pomelo d?ng `ServerVersion.AutoDetect` n?n database ph?i k?t n?i ???c khi kh?i t?o context.
Compose m?c ??nh v?n d?ng MySQL tr?n host. N?u mu?n MySQL Docker: `docker compose --profile mysql up -d mysql`, m?c ??nh c?ng 3307, database/user `codepath`. ??t `MYSQL_PASSWORD`, `MYSQL_ROOT_PASSWORD` tr??c khi ch?y; connection API ph?i d?ng c?ng password. Kh?ng c? service PostgreSQL. Migration PostgreSQL c? n?m ngo?i project trong `database/migrations-archive/postgresql`, kh?ng ???c EF compile/ch?y.

Cloudinary d?ng bi?n m?i tr??ng ho?c user-secrets, kh?ng commit credentials:

- `Cloudinary__CloudName`
- `Cloudinary__ApiKey`
- `Cloudinary__ApiSecret`

Upload th?c hi?n server-side qua HTTPS, theo [Cloudinary Upload API](https://cloudinary.com/documentation/image_upload_api_reference). C?c endpoint profile/password kh?ng c?n Cloudinary. Kh?ng g?i upload th?t trong unit test. N?u DB conflict/failure sau khi upload th?nh c?ng, ?nh m?i c? th? ch?a ???c tham chi?u v? c?n d?n tr?n Cloudinary; m?i upload d?ng public ID ri?ng, kh?ng ghi ?? ?nh ?ang d?ng.

## Contract

D?ng `users.http`, s?a base URL v? token. Profile response kh?ng ch?a password hash ho?c security version.

- `GET /api/users/me`: l?y profile v? `version`.
- `PUT /api/users/me`: g?i l?i `version`; PUT thay th? c?c field profile, nullable b? b? qua trong JSON s? th?nh null. `fullName` b?t bu?c. Gender: `Male`, `Female`, `Other`, ho?c null. Avatar URL ch? cho HTTPS.
- Student d?ng `administrativeClassId`, ?nh x? `User.ClassId` (entity `Class` hi?n c?); teacher d?ng `facultyId`, `academicTitle`. Field kh?ng thu?c role b? t? ch?i.
- `PUT /api/users/me/password`: old/new/confirm password; 5 l?n/15 ph?t/user/process. ??i th?nh c?ng tr? 204 v? ph?i ??ng nh?p l?i. Sliding-window limiter hi?n l? in-memory theo convention project; tri?n khai nhi?u API instance c?n limiter ph?n t?n ?? gi? c?ng quota to?n c?m.
- `POST /api/users/me/avatar`: multipart `file`, `version`; jpg/jpeg/png/webp, t?i ?a 2 MiB, ki?m tra magic bytes tr??c khi Cloudinary x? l? ?nh.
- `PUT /api/admin/users/{id}`: profile + role + status + version. Admin kh?ng t? h? quy?n ho?c v? hi?u h?a ch?nh m?nh. Chuy?n sang Student y?u c?u ?? c? StudentId. Email v? m? sinh vi?n gi? nguy?n qua c?c DTO n?y.

Self-service t? ch?i m?i field kh?ng khai b?o, bao g?m email, studentCode/studentId, role, status, userId. ID actor ch? ??c JWT. Validation tr? 400, thi?u quy?n 403, d? li?u c? ho?c race EF tr? 409, rate limit 429, theo ProblemDetails.

Password hash, SecurityVersion v? thu h?i t?t c? refresh token ???c l?u trong m?t transaction. JWT validation hi?n c? ki?m tra SecurityVersion t? database; kh?ng c?n th?m blacklist Redis cho thao t?c n?y. Role/status thay ??i c?ng t?ng SecurityVersion v? thu h?i refresh token.

Admin update l?u AuditLog c?ng transaction: ActorId, TargetUserId, UserUpdated, OldValue/NewValue JSON, CreatedAt; ch? snapshot DTO an to?n. AuditLog.DataJson hi?n h?u gi? t??ng th?ch c?c ch?c n?ng admin tr??c ??.

## MySQL v? concurrency

`Version` int l? concurrency token; GET tr? version v? m?i c?p nh?t profile/avatar/admin ph?i g?i version ??. EF t?ng version v?i tracked User updates; SecurityVersion c?ng l? concurrency token ?? ph?t hi?n thay ??i phi?n ngo?i lu?ng.
Gi? Guid `binary(16)` theo to?n b? schema hi?n c? (kh?ng ??i c?c kh?a li?n quan sang char(36)). C?c field profile d?ng varchar + utf8mb4_unicode_ci; StudentId gi? collation ph?n bi?t hoa th??ng c?. Email varchar(256) gi? unique index v?i ROW_FORMAT=DYNAMIC, StudentId varchar(50) unique. Audit old/new d?ng MySQL json. Kh?ng d?ng xmin/jsonb/citext/Npgsql.

??i collation c?a users c? th? ph?t hi?n c?c gi? tr? unique ?? t?n t?i nh?ng nay ???c coi l? tr?ng; c?n ki?m tra d? li?u n?u migration b?o duplicate key. Migration ch? thay ??i users/audit_logs.
