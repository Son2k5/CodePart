# CodePath – Phase 1: Authentication & User Management Flow

Tài liệu thiết kế chi tiết và toàn bộ source code mẫu các tầng (Domain, Application, Infrastructure, API, Configuration) cho Phase 1: Register / Login / Logout (School Email `@hanu.edu.vn`), phân loại tài khoản, OTP xác thực Redis, JWT token rotation, blacklist logout và phân quyền.

---

## 1. Tổng quan Kiến trúc & Nguyên tắc Thiết kế

- **Tech Stack**: ASP.NET Core 8 Web API, EF Core 8, PostgreSQL, Redis 7, BCrypt.Net, System.IdentityModel.Tokens.Jwt.
- **Mô hình**: Clean Architecture kết hợp Modular Monolith.
  - **Module `Users`**: Quản lý thông tin cốt lõi của người dùng, phân quyền, trạng thái và lưu trữ tại Postgres schema `users`.
  - **Module `Auth`**: Quản lý phiên làm việc (Refresh Tokens tại Postgres schema `auth`), xác thực mật khẩu, cấp/thu hồi JWT, quản lý OTP và Blacklist token tại Redis.
  - **Quy tắc phụ thuộc một chiều (Strict One-Way Dependency - Tránh Circular Dependency)**:
    - Tầng `Application` chỉ phụ thuộc `Domain` và `Shared.Kernel`. Tầng này định nghĩa các Interface trừu tượng (`IUsersDbContext`, `IAuthDbContext`, `IPasswordHasher`, `IJwtTokenService`, `IOtpService`,...).
    - Tầng `Infrastructure` tham chiếu `Application` để triển khai các Interface này (kết nối DB, Redis, Token).
    - **Tuyệt đối không để `Application` tham chiếu `Infrastructure`**. Mọi Handler chỉ được inject Interface (ví dụ `IUsersDbContext`, `IAuthDbContext`), không inject trực tiếp lớp `DbContext` của Infrastructure.
  - **Giao tiếp liên module**: `Auth.Application` gửi `Command/Query` sang `Users.Application` thông qua MediatR `ISender.Send()`. Không tham chiếu chéo Domain/Infrastructure giữa các module.
- **Quy tắc phân loại tài khoản & bảo mật đăng ký**:
  - Chỉ chấp nhận email domain `@hanu.edu.vn` (normalize lowercase + trim).
  - Khớp regex `^\d+@hanu.edu.vn$` (ví dụ `20231234@hanu.edu.vn`): `Role = Student`, `Status = Active`, `StudentId = 20231234` (trích xuất tự động từ local-part).
  - Các email khác (ví dụ `sondo@hanu.edu.vn`): `Role = Teacher`, `Status = Pending` (chờ Admin cấp quyền ở Phase 2, Phase 1 chặn đăng nhập), `StudentId = null`.
  - **Chống Email Squatting**: Nếu email đã tồn tại nhưng chưa xác thực OTP (`EmailVerifiedAt == null`), hệ thống cho phép ghi đè thông tin đăng ký mới và cấp lại OTP thay vì chặn người dùng hợp lệ.
  - Request DTO tuyệt đối **không** nhận `Role` và `Status` từ phía Client.
  - Admin không đăng ký qua API: seed sẵn `admin01`, `admin02` (`Role = Admin`, `Status = Active`) đọc từ cấu hình/env (bắt buộc cấu hình mật khẩu qua env, không fallback mật khẩu mặc định).

---

## 2. Danh sách Lệnh CLI Cần Chạy

Nếu các project con chưa tham chiếu các package cần thiết (đã khai báo version trong `Directory.Packages.props`):

```powershell
# Chạy tại thư mục api/
dotnet add src/Modules/Auth/CodePath.Modules.Auth.Infrastructure package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add src/Modules/Auth/CodePath.Modules.Auth.Infrastructure package System.IdentityModel.Tokens.Jwt
dotnet add src/Modules/Auth/CodePath.Modules.Auth.Infrastructure package BCrypt.Net-Next
dotnet add src/Modules/Auth/CodePath.Modules.Auth.Infrastructure package StackExchange.Redis

# Infrastructure tham chiếu Application (triển khai Interface Abstractions):
dotnet add src/Modules/Auth/CodePath.Modules.Auth.Infrastructure reference src/Modules/Auth/CodePath.Modules.Auth.Application/CodePath.Modules.Auth.Application.csproj
dotnet add src/Modules/Users/CodePath.Modules.Users.Infrastructure reference src/Modules/Users/CodePath.Modules.Users.Application/CodePath.Modules.Users.Application.csproj

# Cho Auth Application tham chiếu Users Application (giao tiếp MediatR Contracts):
dotnet add src/Modules/Auth/CodePath.Modules.Auth.Application reference src/Modules/Users/CodePath.Modules.Users.Application/CodePath.Modules.Users.Application.csproj
# LƯU Ý QUAN TRỌNG: KHÔNG ADD THAM CHIẾU TỪ Application -> Infrastructure ĐỂ TRÁNH CIRCULAR DEPENDENCY!
```

---

## 3. SHARED KERNEL & SHARED WEB

### 3.1. Base Entity Dùng Chung (Audit Fields cho toàn bộ hệ thống)
Mọi Entity trong toàn bộ hệ thống (`User`, `Faculty`, `Class`, `Course`, `Enrollment`, `RefreshToken`,...) đều kế thừa từ `BaseEntity` đặt tại `CodePath.Shared.Kernel` để chuẩn hóa 4 trường Audit: `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`:

```csharp
// File: src/Shared/CodePath.Shared.Kernel/Entities/BaseEntity.cs
using System.ComponentModel.DataAnnotations.Schema;
using CodePath.Shared.Kernel.Events;

namespace CodePath.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    
    // 4 trường Audit chuẩn hóa cho toàn bộ bảng trong hệ thống
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public string? CreatedBy { get; protected set; }
    public DateTime? UpdatedAt { get; protected set; }
    public string? UpdatedBy { get; protected set; }

    [NotMapped]
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private readonly List<IDomainEvent> _domainEvents = new();

    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();

    public void SetCreatedAudit(string? createdBy = null)
    {
        CreatedAt = DateTime.UtcNow;
        CreatedBy = createdBy;
    }

    public void Touch(string? updatedBy = null)
    {
        UpdatedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(updatedBy))
        {
            UpdatedBy = updatedBy;
        }
    }
}
```

### 3.2. Enums Dùng Chung
Được đặt tại `CodePath.Shared.Kernel` để cả 2 module và tầng Web Authorization đều sử dụng được mà không vi phạm nguyên tắc module boundary:

```csharp
// File: src/Shared/CodePath.Shared.Kernel/Enums/UserRole.cs
namespace CodePath.Shared.Kernel.Enums;

public enum UserRole
{
    Student,
    Teacher,
    Admin
}
```

```csharp
// File: src/Shared/CodePath.Shared.Kernel/Enums/UserStatus.cs
namespace CodePath.Shared.Kernel.Enums;

public enum UserStatus
{
    Pending,
    Active,
    Rejected,
    Disabled
}
```

### 3.3. Result Pattern Chuẩn Hóa & Exception Hỗ Trợ Error Code

Thống nhất chiến lược xử lý lỗi: Tầng Application sử dụng **Result Pattern** kèm `ErrorCode` cho toàn bộ các tình huống dự đoán được (expected domain errors như sai mật khẩu, trạng thái chờ duyệt, xung đột dữ liệu). Exceptions chỉ dùng cho các lỗi bất khả kháng hoặc lỗi tầng hệ thống (unexpected/infrastructure failures).

```csharp
// File: src/Shared/CodePath.Shared.Kernel/Common/Result.cs
namespace CodePath.Shared.Kernel.Common;

public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }
    public string? ErrorCode { get; }

    private Result(bool isSuccess, T? value, string? error, string? errorCode = null)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
        ErrorCode = errorCode;
    }

    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error, string? errorCode = null) => new(false, default, error, errorCode);
}
```

```csharp
// File: src/Shared/CodePath.Shared.Kernel/Exceptions/ForbiddenException.cs
namespace CodePath.Shared.Kernel.Exceptions;

public class ForbiddenException : AppException
{
    public string ErrorCode { get; }

    public ForbiddenException(string errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }
}
```

### 3.4. Cập nhật Middleware Bắt Lỗi (Problem Details - Chống Rò Rỉ Thông Tin & Ghi Log Đầy Đủ)

Middleware ghi log chi tiết mọi unhandled exception kèm `TraceId` và tuyệt đối **không rò rỉ** raw message nội bộ (PostgreSQL, connection string, stack trace) cho client ở nhánh lỗi 500:

```csharp
// File: src/Shared/CodePath.Shared.Web/Extensions/ExceptionHandlingExtensions.cs
using CodePath.Shared.Kernel.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CodePath.Shared.Web.Extensions;

public static class ExceptionHandlingExtensions
{
    public static WebApplication UseAppExceptionHandling(this WebApplication app)
    {
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var feature = context.Features.Get<IExceptionHandlerFeature>();
                var exception = feature?.Error;
                var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("CodePath.Exceptions");

                if (exception is not null)
                {
                    logger.LogError(exception, "Unhandled exception occurred. TraceId: {TraceId}", context.TraceIdentifier);
                }

                var (statusCode, title, errorCode) = exception switch
                {
                    ValidationException => (StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ", "VALIDATION_ERROR"),
                    NotFoundException => (StatusCodes.Status404NotFound, "Không tìm thấy dữ liệu", "NOT_FOUND"),
                    ConflictException => (StatusCodes.Status409Conflict, "Xung đột dữ liệu", "CONFLICT"),
                    ForbiddenException fb => (StatusCodes.Status403Forbidden, "Truy cập bị từ chối", fb.ErrorCode),
                    UnauthorizedAppException => (StatusCodes.Status401Unauthorized, "Không được phép", "UNAUTHORIZED"),
                    AppException => (StatusCodes.Status400BadRequest, "Yêu cầu không hợp lệ", "BAD_REQUEST"),
                    _ => (StatusCodes.Status500InternalServerError, "Đã có lỗi xảy ra ở máy chủ", "INTERNAL_SERVER_ERROR")
                };

                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "application/problem+json";

                var problem = new
                {
                    status = statusCode,
                    code = errorCode,
                    title,
                    detail = exception switch
                    {
                        ValidationException v => string.Join("; ", v.Errors.Select(e => e.ErrorMessage)),
                        AppException appEx => appEx.Message, // Nghiệp vụ đã kiểm soát -> an toàn trả về
                        _ => "Đã có lỗi xảy ra từ hệ thống máy chủ. Vui lòng liên hệ quản trị viên với mã traceId để được hỗ trợ." // Tuyệt đối không leak raw message cho lỗi 500
                    },
                    traceId = context.TraceIdentifier
                };

                await context.Response.WriteAsJsonAsync(problem);
            });
        });

        return app;
    }
}
```

---

## 4. MODULE USERS

### 4.1. Domain: Entity `User`

```csharp
// File: src/Modules/Users/CodePath.Modules.Users.Domain/Entities/User.cs
using CodePath.Domain.Common;
using CodePath.Shared.Kernel.Enums;

namespace CodePath.Modules.Users.Domain.Entities;

public sealed class User : BaseEntity
{
    public string FullName { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public UserRole Role { get; private set; }
    public UserStatus Status { get; private set; }
    public string? StudentId { get; private set; }
    public DateTime? EmailVerifiedAt { get; private set; }
    public DateTime? LastLoginAt { get; private set; }

    private User() { }

    public static User CreateStudent(
        string fullName,
        string email,
        string passwordHash,
        string studentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(studentId);

        var trimmedFullName = fullName.Trim();
        var trimmedStudentId = studentId.Trim();

        if (trimmedFullName.Length > 200)
            throw new ArgumentException("FullName không được vượt quá 200 ký tự.", nameof(fullName));
        if (trimmedStudentId.Length > 50)
            throw new ArgumentException("StudentId không được vượt quá 50 ký tự.", nameof(studentId));

        return new User
        {
            Id = Guid.NewGuid(),
            FullName = trimmedFullName,
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            Role = UserRole.Student,
            Status = UserStatus.Active,
            StudentId = trimmedStudentId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static User CreateTeacher(
        string fullName,
        string email,
        string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        var trimmedFullName = fullName.Trim();
        if (trimmedFullName.Length > 200)
            throw new ArgumentException("FullName không được vượt quá 200 ký tự.", nameof(fullName));

        return new User
        {
            Id = Guid.NewGuid(),
            FullName = trimmedFullName,
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            Role = UserRole.Teacher,
            Status = UserStatus.Pending, // Chờ Admin duyệt
            StudentId = null,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static User CreateAdmin(
        string fullName,
        string email,
        string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        var trimmedFullName = fullName.Trim();
        if (trimmedFullName.Length > 200)
            throw new ArgumentException("FullName không được vượt quá 200 ký tự.", nameof(fullName));

        return new User
        {
            Id = Guid.NewGuid(),
            FullName = trimmedFullName,
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            Role = UserRole.Admin,
            Status = UserStatus.Active,
            StudentId = null,
            EmailVerifiedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void VerifyEmail()
    {
        EmailVerifiedAt = DateTime.UtcNow;
        Touch();
    }

    public void RecordLoginSuccess()
    {
        LastLoginAt = DateTime.UtcNow;
        Touch();
    }

    public void UpdateStatus(UserStatus newStatus)
    {
        Status = newStatus;
        Touch();
    }

    // Chống Email Squatting: Cho phép cập nhật lại thông tin tài khoản chưa xác thực OTP
    public void UpdateUnverifiedAccount(string fullName, string passwordHash, UserRole role, string? studentId)
    {
        if (EmailVerifiedAt.HasValue)
            throw new InvalidOperationException("Không thể ghi đè tài khoản đã xác thực.");

        var trimmedFullName = fullName.Trim();
        var trimmedStudentId = studentId?.Trim();

        if (trimmedFullName.Length > 200)
            throw new ArgumentException("FullName không được vượt quá 200 ký tự.", nameof(fullName));
        if (trimmedStudentId is not null && trimmedStudentId.Length > 50)
            throw new ArgumentException("StudentId không được vượt quá 50 ký tự.", nameof(studentId));

        FullName = trimmedFullName;
        PasswordHash = passwordHash;
        Role = role;
        StudentId = trimmedStudentId;
        Touch();
    }
}
```

### 4.2. Application Abstractions: `IUsersDbContext.cs`
Được định nghĩa tại `CodePath.Modules.Users.Application.Abstractions` để tầng Application chỉ phụ thuộc interface, không phụ thuộc tầng Infrastructure (triệt tiêu hoàn toàn Circular Project Reference):

```csharp
// File: src/Modules/Users/CodePath.Modules.Users.Application/Abstractions/IUsersDbContext.cs
using CodePath.Modules.Users.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodePath.Modules.Users.Application.Abstractions;

public interface IUsersDbContext
{
    DbSet<User> Users { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

### 4.3. Infrastructure: `UsersDbContext.cs` & `UserConfiguration.cs`

```csharp
// File: src/Modules/Users/CodePath.Modules.Users.Infrastructure/Persistence/UsersDbContext.cs
using CodePath.Modules.Users.Application.Abstractions;
using CodePath.Modules.Users.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodePath.Modules.Users.Infrastructure.Persistence;

public sealed class UsersDbContext : DbContext, IUsersDbContext
{
    public const string Schema = "users";

    public UsersDbContext(DbContextOptions<UsersDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.HasDefaultSchema(Schema);
        m.ApplyConfigurationsFromAssembly(typeof(UsersDbContext).Assembly);
        base.OnModelCreating(m);
    }
}
```

```csharp
// File: src/Modules/Users/CodePath.Modules.Users.Infrastructure/Persistence/Configurations/UserConfiguration.cs
using CodePath.Modules.Users.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodePath.Modules.Users.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users", "users");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(256)
            .HasColumnType("character varying(256)");

        b.Property(x => x.PasswordHash)
            .IsRequired()
            .HasMaxLength(256);

        b.Property(x => x.FullName)
            .IsRequired()
            .HasMaxLength(200);

        // Lưu enum string vào DB
        b.Property(x => x.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        b.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        b.Property(x => x.StudentId)
            .HasMaxLength(50)
            .HasColumnType("character varying(50)");

        // 4 trường audit từ BaseEntity
        b.Property(x => x.CreatedAt).IsRequired().HasColumnType("timestamptz");
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedAt).HasColumnType("timestamptz");
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.Property(x => x.EmailVerifiedAt).HasColumnType("timestamptz");
        b.Property(x => x.LastLoginAt).HasColumnType("timestamptz");

        // Indexes
        b.HasIndex(x => x.Email).IsUnique().HasDatabaseName("ux_users_email");

        // Filtered unique index: StudentId chỉ unique khi NOT NULL
        b.HasIndex(x => x.StudentId)
            .IsUnique()
            .HasFilter("\"StudentId\" IS NOT NULL")
            .HasDatabaseName("ux_users_student_id");

        b.HasIndex(x => x.Role).HasDatabaseName("ix_users_role");
        b.HasIndex(x => x.Status).HasDatabaseName("ix_users_status");

        b.Ignore(x => x.DomainEvents);
    }
}
```

### 4.4. Migration Mới: `Update_User_For_Phase1`

```csharp
// File: src/Modules/Users/CodePath.Modules.Users.Infrastructure/Persistence/Migrations/20260919000000_Update_User_For_Phase1.cs
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodePath.Modules.Users.Infrastructure.Persistence.Migrations;

public partial class Update_User_For_Phase1 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Status",
            schema: "users",
            table: "users",
            type: "character varying(50)",
            maxLength: 50,
            nullable: false,
            defaultValue: "Active");

        // Convert dữ liệu cũ Role từ int sang varchar với PostgreSQL USING CASE (0->Student, 1->Teacher, 2->Admin)
        migrationBuilder.Sql(@"
            ALTER TABLE users.users 
            ALTER COLUMN ""Role"" TYPE character varying(50) 
            USING (
                CASE ""Role""::integer
                    WHEN 0 THEN 'Student'
                    WHEN 1 THEN 'Teacher'
                    WHEN 2 THEN 'Admin'
                    ELSE 'Student'
                END
            );
        ");

        migrationBuilder.RenameColumn(
            name: "StudentCode",
            schema: "users",
            table: "users",
            newName: "StudentId");

        // Bỏ FailedLoginCount và LockoutEnd
        migrationBuilder.DropColumn(
            name: "FailedLoginCount",
            schema: "users",
            table: "users");

        migrationBuilder.DropColumn(
            name: "LockoutEnd",
            schema: "users",
            table: "users");

        // Bổ sung CreatedBy và UpdatedBy (chuẩn hóa BaseEntity)
        migrationBuilder.AddColumn<string>(
            name: "CreatedBy",
            schema: "users",
            table: "users",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "UpdatedBy",
            schema: "users",
            table: "users",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_users_status",
            schema: "users",
            table: "users",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "ux_users_student_id",
            schema: "users",
            table: "users",
            column: "StudentId",
            unique: true,
            filter: "\"StudentId\" IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "ix_users_status", schema: "users", table: "users");
        migrationBuilder.DropIndex(name: "ux_users_student_id", schema: "users", table: "users");
        migrationBuilder.DropColumn(name: "Status", schema: "users", table: "users");
        migrationBuilder.DropColumn(name: "CreatedBy", schema: "users", table: "users");
        migrationBuilder.DropColumn(name: "UpdatedBy", schema: "users", table: "users");
        migrationBuilder.RenameColumn(name: "StudentId", schema: "users", table: "users", newName: "StudentCode");

        // Rollback Role từ string về int
        migrationBuilder.Sql(@"
            ALTER TABLE users.users 
            ALTER COLUMN ""Role"" TYPE integer 
            USING (
                CASE ""Role""
                    WHEN 'Student' THEN 0
                    WHEN 'Teacher' THEN 1
                    WHEN 'Admin' THEN 2
                    ELSE 0
                END
            );
        ");

        migrationBuilder.AddColumn<int>(
            name: "FailedLoginCount",
            schema: "users",
            table: "users",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTime>(
            name: "LockoutEnd",
            schema: "users",
            table: "users",
            type: "timestamptz",
            nullable: true);
    }
}
```

### 4.5. Seeder Idempotent cho `admin01` & `admin02` (Bắt buộc cấu hình qua Environment Variables)

```csharp
// File: src/Modules/Users/CodePath.Modules.Users.Infrastructure/Persistence/Seeders/AdminSeeder.cs
using CodePath.Modules.Users.Domain.Entities;
using CodePath.Shared.Kernel.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CodePath.Modules.Users.Infrastructure.Persistence.Seeders;

public static class AdminSeeder
{
    public static async Task SeedAdminsAsync(
        UsersDbContext dbContext,
        IConfiguration config,
        ILogger logger,
        CancellationToken ct = default)
    {
        var admin01Password = config["ADMIN01_PASSWORD"] ?? config["AdminSeed:Admin01:Password"];
        var admin02Password = config["ADMIN02_PASSWORD"] ?? config["AdminSeed:Admin02:Password"];

        if (string.IsNullOrWhiteSpace(admin01Password) || string.IsNullOrWhiteSpace(admin02Password))
        {
            logger.LogWarning("Mật khẩu Admin Seed (ADMIN01_PASSWORD / ADMIN02_PASSWORD) chưa được cấu hình. Bỏ qua seeding để bảo đảm an toàn hệ thống.");
            return;
        }

        var adminsToSeed = new[]
        {
            new
            {
                Email = (config["ADMIN01_EMAIL"] ?? config["AdminSeed:Admin01:Email"] ?? "admin01@hanu.edu.vn").Trim().ToLowerInvariant(),
                Password = admin01Password,
                FullName = config["ADMIN01_FULLNAME"] ?? config["AdminSeed:Admin01:FullName"] ?? "System Administrator 01"
            },
            new
            {
                Email = (config["ADMIN02_EMAIL"] ?? config["AdminSeed:Admin02:Email"] ?? "admin02@hanu.edu.vn").Trim().ToLowerInvariant(),
                Password = admin02Password,
                FullName = config["ADMIN02_FULLNAME"] ?? config["AdminSeed:Admin02:FullName"] ?? "System Administrator 02"
            }
        };

        foreach (var admin in adminsToSeed)
        {
            var exists = await dbContext.Users.AnyAsync(u => u.Email == admin.Email, ct);
            if (!exists)
            {
                var hash = BCrypt.Net.BCrypt.EnhancedHashPassword(admin.Password, workFactor: 12);
                var adminUser = User.CreateAdmin(admin.FullName, admin.Email, hash);
                await dbContext.Users.AddAsync(adminUser, ct);
                logger.LogInformation("Seeded admin account: {Email}", admin.Email);
            }
        }

        await dbContext.SaveChangesAsync(ct);
    }
}
```

Tích hợp gọi `AdminSeeder` vào `UsersDbContextMigrator.cs`:
```csharp
// Trong method MigrateAsync():
await _dbContext.Database.MigrateAsync(cancellationToken);
await AdminSeeder.SeedAdminsAsync(_dbContext, _configuration, _logger, cancellationToken);
```

### 4.6. Contracts của Module Users

```csharp
// File: src/Modules/Users/CodePath.Modules.Users.Application/Contracts/Users/Dtos/UserAuthDto.cs
using CodePath.Shared.Kernel.Enums;

namespace CodePath.Modules.Users.Application.Contracts.Users.Dtos;

public sealed record UserAuthDto(
    Guid Id,
    string Email,
    string FullName,
    string PasswordHash,
    UserRole Role,
    UserStatus Status,
    string? StudentId,
    DateTime? EmailVerifiedAt);
```

```csharp
// File: src/Modules/Users/CodePath.Modules.Users.Application/Contracts/Users/Commands/CreateUserCommand.cs
using CodePath.Shared.Kernel.Common;
using CodePath.Shared.Kernel.Enums;
using MediatR;

namespace CodePath.Modules.Users.Application.Contracts.Users.Commands;

public sealed record CreateUserCommand(
    string FullName,
    string Email,
    string PasswordHash,
    UserRole Role,
    UserStatus Status,
    string? StudentId) : IRequest<Result<Guid>>;
```

```csharp
// File: src/Modules/Users/CodePath.Modules.Users.Application/Contracts/Users/Queries/GetUserByEmailQuery.cs
using CodePath.Modules.Users.Application.Contracts.Users.Dtos;
using CodePath.Shared.Kernel.Common;
using MediatR;

namespace CodePath.Modules.Users.Application.Contracts.Users.Queries;

public sealed record GetUserByEmailQuery(string Email) : IRequest<Result<UserAuthDto>>;
```

```csharp
// File: src/Modules/Users/CodePath.Modules.Users.Application/Contracts/Users/Queries/GetUserByIdQuery.cs
using CodePath.Modules.Users.Application.Contracts.Users.Dtos;
using CodePath.Shared.Kernel.Common;
using MediatR;

namespace CodePath.Modules.Users.Application.Contracts.Users.Queries;

public sealed record GetUserByIdQuery(Guid UserId) : IRequest<Result<UserAuthDto>>;
```

```csharp
// File: src/Modules/Users/CodePath.Modules.Users.Application/Contracts/Users/Commands/VerifyUserEmailCommand.cs
using CodePath.Shared.Kernel.Common;
using MediatR;

namespace CodePath.Modules.Users.Application.Contracts.Users.Commands;

public sealed record VerifyUserEmailCommand(string Email) : IRequest<Result<bool>>;
```

```csharp
// File: src/Modules/Users/CodePath.Modules.Users.Application/Contracts/Users/Commands/RecordLoginSuccessCommand.cs
using CodePath.Shared.Kernel.Common;
using MediatR;

namespace CodePath.Modules.Users.Application.Contracts.Users.Commands;

public sealed record RecordLoginSuccessCommand(Guid UserId) : IRequest<Result<bool>>;
```

```csharp
// File: src/Modules/Users/CodePath.Modules.Users.Application/Contracts/Users/Commands/UpdateUserStatusCommand.cs
using CodePath.Shared.Kernel.Common;
using CodePath.Shared.Kernel.Enums;
using MediatR;

namespace CodePath.Modules.Users.Application.Contracts.Users.Commands;

public sealed record UpdateUserStatusCommand(Guid UserId, UserStatus NewStatus) : IRequest<Result<bool>>;
```

### 4.7. Handlers của Module Users

```csharp
// File: src/Modules/Users/CodePath.Modules.Users.Application/Features/Users/Commands/CreateUserCommandHandler.cs
using CodePath.Modules.Users.Application.Abstractions;
using CodePath.Modules.Users.Application.Contracts.Users.Commands;
using CodePath.Modules.Users.Domain.Entities;
using CodePath.Shared.Kernel.Common;
using CodePath.Shared.Kernel.Enums;
using CodePath.Shared.Kernel.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CodePath.Modules.Users.Application.Features.Users.Commands;

internal sealed class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Result<Guid>>
{
    private readonly IUsersDbContext _dbContext;

    public CreateUserCommandHandler(IUsersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<Guid>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var existingUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
        if (existingUser != null)
        {
            if (existingUser.EmailVerifiedAt.HasValue)
            {
                throw new ConflictException("Email already exists in the system.");
            }

            // Chống Email Squatting: Tài khoản chưa verify OTP -> Cho phép cập nhật lại thông tin & ghi đè mật khẩu mới để gửi lại OTP
            existingUser.UpdateUnverifiedAccount(request.FullName, request.PasswordHash, request.Role, request.StudentId);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result<Guid>.Success(existingUser.Id);
        }

        User user = request.Role switch
        {
            UserRole.Student => User.CreateStudent(request.FullName, normalizedEmail, request.PasswordHash, request.StudentId!),
            UserRole.Teacher => User.CreateTeacher(request.FullName, normalizedEmail, request.PasswordHash),
            UserRole.Admin => User.CreateAdmin(request.FullName, normalizedEmail, request.PasswordHash),
            _ => throw new ArgumentOutOfRangeException(nameof(request.Role))
        };

        try
        {
            await _dbContext.Users.AddAsync(user, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result<Guid>.Success(user.Id);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new ConflictException("Email or student code already exists.");
        }
    }
}
```

```csharp
// File: src/Modules/Users/CodePath.Modules.Users.Application/Features/Users/Queries/GetUserQueriesHandlers.cs
using CodePath.Modules.Users.Application.Abstractions;
using CodePath.Modules.Users.Application.Contracts.Users.Dtos;
using CodePath.Modules.Users.Application.Contracts.Users.Queries;
using CodePath.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodePath.Modules.Users.Application.Features.Users.Queries;

internal sealed class GetUserByEmailQueryHandler : IRequestHandler<GetUserByEmailQuery, Result<UserAuthDto>>
{
    private readonly IUsersDbContext _dbContext;

    public GetUserByEmailQueryHandler(IUsersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<UserAuthDto>> Handle(GetUserByEmailQuery request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null) return Result<UserAuthDto>.Failure("User not found.");

        return Result<UserAuthDto>.Success(new UserAuthDto(
            user.Id, user.Email, user.FullName, user.PasswordHash,
            user.Role, user.Status, user.StudentId, user.EmailVerifiedAt));
    }
}

internal sealed class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, Result<UserAuthDto>>
{
    private readonly IUsersDbContext _dbContext;

    public GetUserByIdQueryHandler(IUsersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<UserAuthDto>> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user is null) return Result<UserAuthDto>.Failure("User not found.");

        return Result<UserAuthDto>.Success(new UserAuthDto(
            user.Id, user.Email, user.FullName, user.PasswordHash,
            user.Role, user.Status, user.StudentId, user.EmailVerifiedAt));
    }
}
```

```csharp
// File: src/Modules/Users/CodePath.Modules.Users.Application/Features/Users/Commands/VerifyAndLoginStatusHandlers.cs
using CodePath.Modules.Users.Application.Abstractions;
using CodePath.Modules.Users.Application.Contracts.Users.Commands;
using CodePath.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace CodePath.Modules.Users.Application.Features.Users.Commands;

internal sealed class VerifyUserEmailCommandHandler : IRequestHandler<VerifyUserEmailCommand, Result<bool>>
{
    private readonly IUsersDbContext _dbContext;

    public VerifyUserEmailCommandHandler(IUsersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<bool>> Handle(VerifyUserEmailCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user is null) return Result<bool>.Failure("User not found.");

        user.VerifyEmail();
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

internal sealed class RecordLoginSuccessCommandHandler : IRequestHandler<RecordLoginSuccessCommand, Result<bool>>
{
    private readonly IUsersDbContext _dbContext;

    public RecordLoginSuccessCommandHandler(IUsersDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<bool>> Handle(RecordLoginSuccessCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        if (user is null) return Result<bool>.Failure("User not found.");

        user.RecordLoginSuccess();
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

internal sealed class UpdateUserStatusCommandHandler : IRequestHandler<UpdateUserStatusCommand, Result<bool>>
{
    private readonly IUsersDbContext _dbContext;
    private readonly IDatabase _redis;

    public UpdateUserStatusCommandHandler(IUsersDbContext dbContext, IConnectionMultiplexer redis)
    {
        _dbContext = dbContext;
        _redis = redis.GetDatabase();
    }

    public async Task<Result<bool>> Handle(UpdateUserStatusCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        if (user is null) return Result<bool>.Failure("User not found.");

        user.UpdateStatus(request.NewStatus);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // M1 Fix: Invalidate cache Redis ngay lập tức để ActiveUser policy lập tức chặn truy cập nếu chuyển Disabled
        await _redis.KeyDeleteAsync($"user:status:{request.UserId}");

        return Result<bool>.Success(true);
    }
}
```

---

## 5. MODULE AUTH

### 5.1. Domain Entity: `RefreshToken.cs` (Đóng gói chuẩn DDD)

Entity `RefreshToken` kế thừa từ `BaseEntity`, tuân thủ nghiêm ngặt nguyên tắc encapsulation (private setters, private constructor, static factory method `Create`, và domain method `Revoke`) tương tự entity `User`:

```csharp
// File: src/Modules/Auth/CodePath.Modules.Auth.Domain/Entities/RefreshToken.cs
using CodePath.Domain.Common;

namespace CodePath.Modules.Auth.Domain.Entities;

public sealed class RefreshToken : BaseEntity
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = default!;
    public DateTime ExpiresAt { get; private set; }
    public string? CreatedByIp { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevokedByIp { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsActive => !IsRevoked && !IsExpired;

    private RefreshToken() { }

    public static RefreshToken Create(
        Guid userId,
        string tokenHash,
        DateTime expiresAt,
        string? createdByIp = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedByIp = createdByIp,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Revoke(string? revokedByIp = null, string? replacedByTokenHash = null)
    {
        RevokedAt = DateTime.UtcNow;
        RevokedByIp = revokedByIp;
        ReplacedByTokenHash = replacedByTokenHash;
        Touch(revokedByIp);
    }
}
```

### 5.2. Application Abstractions

```csharp
// File: src/Modules/Auth/CodePath.Modules.Auth.Application/Abstractions/IAuthDbContext.cs
using CodePath.Modules.Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodePath.Modules.Auth.Application.Abstractions;

public interface IAuthDbContext
{
    DbSet<RefreshToken> RefreshTokens { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

```csharp
// File: src/Modules/Auth/CodePath.Modules.Auth.Application/Abstractions/IPasswordHasher.cs
namespace CodePath.Modules.Auth.Application.Abstractions;

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
}
```

```csharp
// File: src/Modules/Auth/CodePath.Modules.Auth.Application/Abstractions/IJwtTokenService.cs
using CodePath.Shared.Kernel.Enums;

namespace CodePath.Modules.Auth.Application.Abstractions;

public record GeneratedTokens(string AccessToken, string RefreshToken, int ExpiresInSeconds);

public interface IJwtTokenService
{
    GeneratedTokens GenerateTokens(Guid userId, string email, UserRole role, UserStatus status);
    string HashRefreshToken(string refreshToken);
}
```

```csharp
// File: src/Modules/Auth/CodePath.Modules.Auth.Application/Abstractions/IOtpService.cs
namespace CodePath.Modules.Auth.Application.Abstractions;

public interface IOtpService
{
    Task<(bool Success, string? ErrorMessage)> CanRequestOtpAsync(string email);
    Task<string> GenerateAndStoreOtpAsync(string email);
    Task<(bool IsValid, string? ErrorMessage)> VerifyOtpAsync(string email, string otp);
}
```

```csharp
// File: src/Modules/Auth/CodePath.Modules.Auth.Application/Abstractions/ITokenBlacklistService.cs
namespace CodePath.Modules.Auth.Application.Abstractions;

public interface ITokenBlacklistService
{
    Task BlacklistTokenAsync(string jti, TimeSpan remainingLifetime);
    Task<bool> IsBlacklistedAsync(string jti);
}
```

```csharp
// File: src/Modules/Auth/CodePath.Modules.Auth.Application/Abstractions/IEmailSender.cs
namespace CodePath.Modules.Auth.Application.Abstractions;

public interface IEmailSender
{
    Task SendOtpEmailAsync(string toEmail, string otp, CancellationToken ct = default);
}
```

---

### 5.3. Infrastructure Persistence & Services

#### Auth DbContext (Triển khai IAuthDbContext)
```csharp
// File: src/Modules/Auth/CodePath.Modules.Auth.Infrastructure/Persistence/AuthDbContext.cs
using CodePath.Modules.Auth.Application.Abstractions;
using CodePath.Modules.Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodePath.Modules.Auth.Infrastructure.Persistence;

public class AuthDbContext : DbContext, IAuthDbContext
{
    public const string Schema = "auth";

    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.HasDefaultSchema(Schema);
        m.ApplyConfigurationsFromAssembly(typeof(AuthDbContext).Assembly);
        base.OnModelCreating(m);
    }
}
```

#### Refresh Token Configuration (EF Core)
```csharp
// File: src/Modules/Auth/CodePath.Modules.Auth.Infrastructure/Persistence/Configurations/RefreshTokenConfiguration.cs
using CodePath.Modules.Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodePath.Modules.Auth.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens", "auth");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.UserId).IsRequired();
        b.Property(x => x.TokenHash).IsRequired().HasMaxLength(128);
        b.Property(x => x.ExpiresAt).IsRequired().HasColumnType("timestamptz");

        b.Property(x => x.CreatedByIp).HasMaxLength(64);
        b.Property(x => x.RevokedByIp).HasMaxLength(64);
        b.Property(x => x.ReplacedByTokenHash).HasMaxLength(128);

        b.Property(x => x.RevokedAt).HasColumnType("timestamptz");
        b.Property(x => x.CreatedAt).IsRequired().HasColumnType("timestamptz");
        b.Property(x => x.UpdatedAt).HasColumnType("timestamptz");

        b.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("ux_auth_refresh_tokens_hash");
        b.HasIndex(x => x.UserId).HasDatabaseName("ix_auth_refresh_tokens_user");

        b.Ignore(x => x.DomainEvents);
    }
}
```

#### BCrypt Hasher
```csharp
// File: src/Modules/Auth/CodePath.Modules.Auth.Infrastructure/Services/BCryptPasswordHasher.cs
using CodePath.Modules.Auth.Application.Abstractions;

namespace CodePath.Modules.Auth.Infrastructure.Services;

public sealed class BCryptPasswordHasher : IPasswordHasher
{
    public string HashPassword(string password) =>
        BCrypt.Net.BCrypt.EnhancedHashPassword(password, workFactor: 12);

    public bool VerifyPassword(string password, string passwordHash) =>
        BCrypt.Net.BCrypt.EnhancedVerify(password, passwordHash);
}
```

#### JWT Token Service
```csharp
// File: src/Modules/Auth/CodePath.Modules.Auth.Infrastructure/Services/JwtTokenService.cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CodePath.Modules.Auth.Application.Abstractions;
using CodePath.Shared.Kernel.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CodePath.Modules.Auth.Infrastructure.Services;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration) => _configuration = configuration;

    public GeneratedTokens GenerateTokens(Guid userId, string email, UserRole role, UserStatus status)
    {
        var secret = _configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey is missing.");
        var issuer = _configuration["Jwt:Issuer"] ?? "CodePath.Api";
        var audience = _configuration["Jwt:Audience"] ?? "CodePath.Client";
        var expiryMinutes = int.TryParse(_configuration["Jwt:AccessTokenExpiryMinutes"], out var mins) ? mins : 15;

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(secret);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(ClaimTypes.Role, role.ToString()),
            new("status", status.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(expiryMinutes),
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var accessToken = tokenHandler.WriteToken(token);

        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        var refreshToken = Convert.ToBase64String(randomBytes);

        return new GeneratedTokens(accessToken, refreshToken, expiryMinutes * 60);
    }

    public string HashRefreshToken(string refreshToken)
    {
        var bytes = Encoding.UTF8.GetBytes(refreshToken);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
```

#### Redis OTP Service (Đảm bảo Atomic hoàn toàn bằng Lua Script & Transaction)
```csharp
// File: src/Modules/Auth/CodePath.Modules.Auth.Infrastructure/Services/RedisOtpService.cs
using System.Security.Cryptography;
using CodePath.Modules.Auth.Application.Abstractions;
using StackExchange.Redis;

namespace CodePath.Modules.Auth.Infrastructure.Services;

public sealed class RedisOtpService : IOtpService
{
    private readonly IDatabase _redis;
    private const int OtpTtlMinutes = 10;
    private const int MaxAttempts = 5;
    private const int CooldownSeconds = 60;

    // Lua Script đảm bảo kiểm tra OTP và trừ attempts diễn ra atomic, chống race condition khi có nhiều request đồng thời
    private static readonly LuaScript VerifyOtpScript = LuaScript.Prepare(@"
        local otpKey = @otpKey
        local attemptsKey = @attemptsKey
        local cooldownKey = @cooldownKey
        local inputOtp = @inputOtp

        local storedOtp = redis.call('GET', otpKey)
        if not storedOtp then
            return { -1, 'OTP đã hết hạn hoặc không tồn tại.' }
        end

        local attempts = tonumber(redis.call('GET', attemptsKey) or '0')
        if attempts <= 0 then
            redis.call('DEL', otpKey, attemptsKey)
            return { -2, 'Bạn đã nhập sai OTP quá số lần quy định. Vui lòng yêu cầu lại mã.' }
        end

        if storedOtp ~= inputOtp then
            attempts = redis.call('DECR', attemptsKey)
            if attempts <= 0 then
                redis.call('DEL', otpKey, attemptsKey)
                return { -2, 'Bạn đã nhập sai OTP quá số lần quy định. Vui lòng yêu cầu lại mã.' }
            end
            return { 0, tostring(attempts) }
        end

        -- OTP chính xác: dọn dẹp sạch key OTP và cooldown
        redis.call('DEL', otpKey, attemptsKey, cooldownKey)
        return { 1, 'SUCCESS' }
    ");

    public RedisOtpService(IConnectionMultiplexer redis) => _redis = redis.GetDatabase();

    public async Task<(bool Success, string? ErrorMessage)> CanRequestOtpAsync(string email)
    {
        var cooldownKey = $"auth:otp:cooldown:{email.ToLowerInvariant()}";
        if (await _redis.KeyExistsAsync(cooldownKey))
        {
            var ttl = await _redis.KeyTimeToLiveAsync(cooldownKey);
            return (false, $"Vui lòng đợi {ttl?.Seconds ?? 60}s trước khi yêu cầu lại OTP.");
        }
        return (true, null);
    }

    public async Task<string> GenerateAndStoreOtpAsync(string email)
    {
        var normEmail = email.ToLowerInvariant();
        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

        var otpKey = $"auth:otp:{normEmail}";
        var attemptsKey = $"auth:otp:attempts:{normEmail}";
        var cooldownKey = $"auth:otp:cooldown:{normEmail}";

        // Sử dụng Transaction và await đảm bảo ghi đồng thời 3 key thành công
        var tran = _redis.CreateTransaction();
        _ = tran.StringSetAsync(otpKey, otp, TimeSpan.FromMinutes(OtpTtlMinutes));
        _ = tran.StringSetAsync(attemptsKey, MaxAttempts, TimeSpan.FromMinutes(OtpTtlMinutes));
        _ = tran.StringSetAsync(cooldownKey, "1", TimeSpan.FromSeconds(CooldownSeconds));
        
        var committed = await tran.ExecuteAsync();
        if (!committed)
        {
            throw new InvalidOperationException("Không thể khởi tạo mã OTP trong Redis.");
        }

        return otp;
    }

    public async Task<(bool IsValid, string? ErrorMessage)> VerifyOtpAsync(string email, string otp)
    {
        var normEmail = email.ToLowerInvariant();
        var result = (RedisResult[]?)await _redis.ScriptEvaluateAsync(VerifyOtpScript, new
        {
            otpKey = (RedisKey)$"auth:otp:{normEmail}",
            attemptsKey = (RedisKey)$"auth:otp:attempts:{normEmail}",
            cooldownKey = (RedisKey)$"auth:otp:cooldown:{normEmail}",
            inputOtp = otp.Trim()
        });

        if (result == null || result.Length < 2)
            return (false, "Lỗi kiểm tra OTP từ máy chủ.");

        var status = (int)result[0];
        var message = (string?)result[1];

        return status switch
        {
            1 => (true, null),
            0 => (false, $"Mã OTP không chính xác. Bạn còn {message} lần thử."),
            _ => (false, message ?? "Mã OTP không hợp lệ.")
        };
    }
}
```

#### Redis Token Blacklist Service & Email Mock Sender
```csharp
// File: src/Modules/Auth/CodePath.Modules.Auth.Infrastructure/Services/RedisTokenBlacklistService.cs
using CodePath.Modules.Auth.Application.Abstractions;
using StackExchange.Redis;

namespace CodePath.Modules.Auth.Infrastructure.Services;

public sealed class RedisTokenBlacklistService : ITokenBlacklistService
{
    private readonly IDatabase _redis;
    public RedisTokenBlacklistService(IConnectionMultiplexer redis) => _redis = redis.GetDatabase();

    public async Task BlacklistTokenAsync(string jti, TimeSpan remainingLifetime)
    {
        if (remainingLifetime > TimeSpan.Zero)
            await _redis.StringSetAsync($"auth:blacklist:jti:{jti}", "revoked", remainingLifetime);
    }

    public async Task<bool> IsBlacklistedAsync(string jti) =>
        await _redis.KeyExistsAsync($"auth:blacklist:jti:{jti}");
}
```

```csharp
// File: src/Modules/Auth/CodePath.Modules.Auth.Infrastructure/Services/EmailSender.cs
using CodePath.Modules.Auth.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace CodePath.Modules.Auth.Infrastructure.Services;

public sealed class EmailSender : IEmailSender
{
    private readonly ILogger<EmailSender> _logger;
    public EmailSender(ILogger<EmailSender> logger) => _logger = logger;

    public Task SendOtpEmailAsync(string toEmail, string otp, CancellationToken ct = default)
    {
        _logger.LogInformation("[MOCK-EMAIL] Gửi mã OTP xác thực tới {ToEmail}: {Otp}", toEmail, otp);
        return Task.CompletedTask;
    }
}
```

#### Authorization Handler kiểm tra Status thực tế (Phase 2 ready)
```csharp
// File: src/Modules/Auth/CodePath.Modules.Auth.Infrastructure/Authorization/ActiveUserRequirement.cs
using System.Security.Claims;
using CodePath.Modules.Users.Application.Contracts.Users.Queries;
using CodePath.Shared.Kernel.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using StackExchange.Redis;

namespace CodePath.Modules.Auth.Infrastructure.Authorization;

public class ActiveUserRequirement : IAuthorizationRequirement
{
    public UserRole? RequiredRole { get; }
    public ActiveUserRequirement(UserRole? requiredRole = null) => RequiredRole = requiredRole;
}

public class ActiveUserAuthorizationHandler : AuthorizationHandler<ActiveUserRequirement>
{
    private readonly ISender _sender;
    private readonly IDatabase _redis;

    public ActiveUserAuthorizationHandler(ISender sender, IConnectionMultiplexer redis)
    {
        _sender = sender;
        _redis = redis.GetDatabase();
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ActiveUserRequirement requirement)
    {
        var sub = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
               ?? context.User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(sub, out var userId)) return;

        var cacheKey = $"user:status:{userId}";
        var cachedStatus = await _redis.StringGetAsync(cacheKey);

        UserStatus currentStatus;
        if (cachedStatus.HasValue && Enum.TryParse<UserStatus>(cachedStatus.ToString(), out var parsedStatus))
        {
            currentStatus = parsedStatus;
        }
        else
        {
            var userResult = await _sender.Send(new GetUserByIdQuery(userId));
            if (!userResult.IsSuccess || userResult.Value is null) return;

            currentStatus = userResult.Value.Status;
            await _redis.StringSetAsync(cacheKey, currentStatus.ToString(), TimeSpan.FromMinutes(5));
        }

        if (currentStatus != UserStatus.Active) return;

        if (requirement.RequiredRole.HasValue)
        {
            var roleClaim = context.User.FindFirst(ClaimTypes.Role)?.Value;
            if (roleClaim != requirement.RequiredRole.Value.ToString()) return;
        }

        context.Succeed(requirement);
    }
}
```

---

### 5.4. Application: Features (Commands, Handlers, Validators)

#### 1. Register Flow
```csharp
// File: src/Modules/Auth/CodePath.Modules.Auth.Application/Features/Auth/Commands/RegisterCommand.cs
using System.Text.RegularExpressions;
using CodePath.Modules.Auth.Application.Abstractions;
using CodePath.Modules.Users.Application.Contracts.Users.Commands;
using CodePath.Shared.Kernel.Common;
using CodePath.Shared.Kernel.Enums;
using FluentValidation;
using MediatR;

namespace CodePath.Modules.Auth.Application.Features.Auth.Commands;

public sealed record RegisterCommand(string FullName, string Email, string Password) : IRequest<Result<string>>;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    private static readonly Regex HanuEmailRegex = new(@"^[a-zA-Z0-9._%+-]+@hanu\.edu\.vn$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public RegisterCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email)
            .NotEmpty()
            .Must(e => HanuEmailRegex.IsMatch(e?.Trim() ?? ""))
            .WithMessage("Chỉ chấp nhận email có đuôi @hanu.edu.vn.");

        RuleFor(x => x.Password)
            .NotEmpty().MinimumLength(8).WithMessage("Mật khẩu tối thiểu 8 ký tự.")
            .Matches(@"[A-Z]").WithMessage("Mật khẩu phải chứa ít nhất 1 chữ hoa.")
            .Matches(@"[a-z]").WithMessage("Mật khẩu phải chứa ít nhất 1 chữ thường.")
            .Matches(@"[0-9]").WithMessage("Mật khẩu phải chứa ít nhất 1 chữ số.")
            .Matches(@"[\!\?\*\@\#\$\%\^\&\+\=]").WithMessage("Mật khẩu phải chứa ít nhất 1 ký tự đặc biệt.");
    }
}

internal sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<string>>
{
    private readonly ISender _sender;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOtpService _otpService;
    private readonly IEmailSender _emailSender;
    private static readonly Regex StudentRegex = new(@"^\d+@hanu\.edu\.vn$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public RegisterCommandHandler(
        ISender sender,
        IPasswordHasher passwordHasher,
        IOtpService otpService,
        IEmailSender emailSender)
    {
        _sender = sender;
        _passwordHasher = passwordHasher;
        _otpService = otpService;
        _emailSender = emailSender;
    }

    public async Task<Result<string>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        UserRole role;
        UserStatus status;
        string? studentId = null;

        // Phân loại local-part
        if (StudentRegex.IsMatch(normalizedEmail))
        {
            role = UserRole.Student;
            status = UserStatus.Active;
            studentId = normalizedEmail.Split('@')[0];
        }
        else
        {
            role = UserRole.Teacher;
            status = UserStatus.Pending;
        }

        var passwordHash = _passwordHasher.HashPassword(request.Password);

        var createResult = await _sender.Send(new CreateUserCommand(
            request.FullName,
            normalizedEmail,
            passwordHash,
            role,
            status,
            studentId), cancellationToken);

        if (!createResult.IsSuccess)
        {
            return Result<string>.Failure(createResult.Error ?? "Đăng ký không thành công.");
        }

        try
        {
            var otp = await _otpService.GenerateAndStoreOtpAsync(normalizedEmail);
            await _emailSender.SendOtpEmailAsync(normalizedEmail, otp, cancellationToken);
            return Result<string>.Success("Đăng ký tài khoản thành công. Vui lòng kiểm tra email để nhận mã OTP xác thực.");
        }
        catch (Exception)
        {
            // N6 Fix: Tránh lỗi 500 làm người dùng bối rối khi tài khoản đã tạo thành công trong DB nhưng gửi OTP gặp sự cố
            return Result<string>.Success("Tài khoản đã được tạo thành công nhưng hệ thống gặp sự cố khi gửi mã OTP. Vui lòng bấm 'Gửi lại OTP' để nhận mã xác thực.");
        }
    }
}
```

#### 2. Verify OTP & Resend OTP
```csharp
// File: src/Modules/Auth/CodePath.Modules.Auth.Application/Features/Auth/Commands/VerifyOtpCommand.cs
using CodePath.Modules.Auth.Application.Abstractions;
using CodePath.Modules.Users.Application.Contracts.Users.Commands;
using CodePath.Shared.Kernel.Common;
using FluentValidation;
using MediatR;

namespace CodePath.Modules.Auth.Application.Features.Auth.Commands;

public sealed record VerifyOtpCommand(string Email, string Otp) : IRequest<Result<string>>;

public sealed class VerifyOtpCommandValidator : AbstractValidator<VerifyOtpCommand>
{
    public VerifyOtpCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Otp).NotEmpty().Length(6).Matches(@"^\d{6}$").WithMessage("Mã OTP gồm 6 chữ số.");
    }
}

internal sealed class VerifyOtpCommandHandler : IRequestHandler<VerifyOtpCommand, Result<string>>
{
    private readonly IOtpService _otpService;
    private readonly ISender _sender;

    public VerifyOtpCommandHandler(IOtpService otpService, ISender sender)
    {
        _otpService = otpService;
        _sender = sender;
    }

    public async Task<Result<string>> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var (isValid, errorMessage) = await _otpService.VerifyOtpAsync(normalizedEmail, request.Otp);
        if (!isValid) return Result<string>.Failure(errorMessage ?? "Mã OTP không hợp lệ.");

        var verifyResult = await _sender.Send(new VerifyUserEmailCommand(normalizedEmail), cancellationToken);
        if (!verifyResult.IsSuccess) return Result<string>.Failure(verifyResult.Error ?? "Không thể xác minh email.");

        return Result<string>.Success("Xác minh email thành công. Bây giờ bạn có thể đăng nhập.");
    }
}
```

```csharp
// File: src/Modules/Auth/CodePath.Modules.Auth.Application/Features/Auth/Commands/ResendOtpCommand.cs
using CodePath.Modules.Auth.Application.Abstractions;
using CodePath.Modules.Users.Application.Contracts.Users.Queries;
using CodePath.Shared.Kernel.Common;
using FluentValidation;
using MediatR;

namespace CodePath.Modules.Auth.Application.Features.Auth.Commands;

public sealed record ResendOtpCommand(string Email) : IRequest<Result<string>>;

public sealed class ResendOtpCommandValidator : AbstractValidator<ResendOtpCommand>
{
    public ResendOtpCommandValidator() => RuleFor(x => x.Email).NotEmpty().EmailAddress();
}

internal sealed class ResendOtpCommandHandler : IRequestHandler<ResendOtpCommand, Result<string>>
{
    private readonly IOtpService _otpService;
    private readonly IEmailSender _emailSender;
    private readonly ISender _sender;

    public ResendOtpCommandHandler(IOtpService otpService, IEmailSender emailSender, ISender sender)
    {
        _otpService = otpService;
        _emailSender = emailSender;
        _sender = sender;
    }

    public async Task<Result<string>> Handle(ResendOtpCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var userResult = await _sender.Send(new GetUserByEmailQuery(normalizedEmail), cancellationToken);
        if (!userResult.IsSuccess || userResult.Value is null)
        {
            return Result<string>.Success("Nếu tài khoản tồn tại, mã OTP mới đã được gửi.");
        }

        if (userResult.Value.EmailVerifiedAt.HasValue)
        {
            return Result<string>.Failure("Email này đã được xác thực trước đó.");
        }

        var (canRequest, error) = await _otpService.CanRequestOtpAsync(normalizedEmail);
        if (!canRequest) return Result<string>.Failure(error!);

        var otp = await _otpService.GenerateAndStoreOtpAsync(normalizedEmail);
        await _emailSender.SendOtpEmailAsync(normalizedEmail, otp, cancellationToken);

        return Result<string>.Success("Mã OTP mới đã được gửi đến email của bạn.");
    }
}
```

#### 3. Login Flow
```csharp
// File: src/Modules/Auth/CodePath.Modules.Auth.Application/Features/Auth/Commands/LoginCommand.cs
using CodePath.Modules.Auth.Application.Abstractions;
using CodePath.Modules.Auth.Domain.Entities;
using CodePath.Modules.Users.Application.Contracts.Users.Commands;
using CodePath.Modules.Users.Application.Contracts.Users.Queries;
using CodePath.Shared.Kernel.Common;
using CodePath.Shared.Kernel.Enums;
using CodePath.Shared.Kernel.Exceptions;
using FluentValidation;
using MediatR;

namespace CodePath.Modules.Auth.Application.Features.Auth.Commands;

public sealed record LoginResponse(string AccessToken, string RefreshToken, int ExpiresIn, string TokenType = "Bearer");

public sealed record LoginCommand(string Email, string Password) : IRequest<Result<LoginResponse>>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

internal sealed class LoginCommandHandler : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly ISender _sender;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IAuthDbContext _authDbContext;

    private const string GenericAuthErrorMessage = "Email hoặc mật khẩu không chính xác.";

    public LoginCommandHandler(
        ISender sender,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IAuthDbContext authDbContext)
    {
        _sender = sender;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _authDbContext = authDbContext;
    }

    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // 1. Check Email
        var userResult = await _sender.Send(new GetUserByEmailQuery(normalizedEmail), cancellationToken);
        if (!userResult.IsSuccess || userResult.Value is null)
        {
            _passwordHasher.VerifyPassword("dummy", "$2a$12$e8vT9gXhM4kE4D.B1G8q2.j3l5oG7m9qP1rS3tU5vW7xY9zA1bC3e");
            return Result<LoginResponse>.Failure(GenericAuthErrorMessage, "UNAUTHORIZED");
        }

        var user = userResult.Value;

        // 2. Check Password
        var isPasswordValid = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            return Result<LoginResponse>.Failure(GenericAuthErrorMessage, "UNAUTHORIZED");
        }

        // 3. Check EmailVerifiedAt
        if (!user.EmailVerifiedAt.HasValue)
        {
            return Result<LoginResponse>.Failure("Email chưa được xác thực. Vui lòng xác thực OTP trước khi đăng nhập.", "UNAUTHORIZED");
        }

        // 4. Check Status (M5 Fix: Trả mã lỗi thống nhất qua Result pattern, không throw Exception)
        switch (user.Status)
        {
            case UserStatus.Pending:
                return Result<LoginResponse>.Failure("Tài khoản Giảng viên đang chờ Quản trị viên cấp quyền.", "ACCOUNT_PENDING");
            case UserStatus.Rejected:
                return Result<LoginResponse>.Failure("Tài khoản của bạn đã bị từ chối.", "ACCOUNT_REJECTED");
            case UserStatus.Disabled:
                return Result<LoginResponse>.Failure("Tài khoản của bạn đã bị vô hiệu hóa.", "ACCOUNT_DISABLED");
            case UserStatus.Active:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        // Ghi nhận đăng nhập thành công
        await _sender.Send(new RecordLoginSuccessCommand(user.Id), cancellationToken);

        // 5. Cấp Access Token + Refresh Token
        var tokens = _jwtTokenService.GenerateTokens(user.Id, user.Email, user.Role, user.Status);
        var tokenHash = _jwtTokenService.HashRefreshToken(tokens.RefreshToken);

        var refreshTokenEntity = RefreshToken.Create(
            user.Id,
            tokenHash,
            DateTime.UtcNow.AddDays(7));

        await _authDbContext.RefreshTokens.AddAsync(refreshTokenEntity, cancellationToken);
        await _authDbContext.SaveChangesAsync(cancellationToken);

        return Result<LoginResponse>.Success(new LoginResponse(
            tokens.AccessToken,
            tokens.RefreshToken,
            tokens.ExpiresInSeconds));
    }
}
```

#### 4. Refresh Token Flow (Rotation & Reuse Detection với Grace Window 30s)
```csharp
// File: src/Modules/Auth/CodePath.Modules.Auth.Application/Features/Auth/Commands/RefreshTokenCommand.cs
using CodePath.Modules.Auth.Application.Abstractions;
using CodePath.Modules.Auth.Domain.Entities;
using CodePath.Modules.Users.Application.Contracts.Users.Queries;
using CodePath.Shared.Kernel.Common;
using CodePath.Shared.Kernel.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodePath.Modules.Auth.Application.Features.Auth.Commands;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<Result<LoginResponse>>;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

internal sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<LoginResponse>>
{
    private readonly IAuthDbContext _authDbContext;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ISender _sender;

    public RefreshTokenCommandHandler(
        IAuthDbContext authDbContext,
        IJwtTokenService jwtTokenService,
        ISender sender)
    {
        _authDbContext = authDbContext;
        _jwtTokenService = jwtTokenService;
        _sender = sender;
    }

    public async Task<Result<LoginResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = _jwtTokenService.HashRefreshToken(request.RefreshToken);

        var existingToken = await _authDbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (existingToken is null)
        {
            return Result<LoginResponse>.Failure("Refresh token không hợp lệ.");
        }

        // REUSE DETECTION: Nếu token đã revoked bị dùng lại
        if (existingToken.IsRevoked)
        {
            var timeSinceRevoked = DateTime.UtcNow - existingToken.RevokedAt!.Value;

            // Grace Window (30 giây): Bảo vệ trường hợp 2 tab trình duyệt cùng gửi request refresh đồng thời
            if (timeSinceRevoked <= TimeSpan.FromSeconds(30) && !string.IsNullOrWhiteSpace(existingToken.ReplacedByTokenHash))
            {
                var activeReplacement = await _authDbContext.RefreshTokens
                    .FirstOrDefaultAsync(t => t.TokenHash == existingToken.ReplacedByTokenHash && t.RevokedAt == null, cancellationToken);

                if (activeReplacement != null && !activeReplacement.IsExpired)
                {
                    var userRes = await _sender.Send(new GetUserByIdQuery(existingToken.UserId), cancellationToken);
                    if (userRes.IsSuccess && userRes.Value != null && userRes.Value.Status == UserStatus.Active)
                    {
                        var u = userRes.Value;
                        // C2 Fix: Cấp cặp token MỚI và lưu vào DB cho tab thứ 2 bắt kịp phiên làm việc
                        // Tuyệt đối KHÔNG trả lại request.RefreshToken (đã bị revoke) vì sẽ gây lỗi reuse giả và sập toàn bộ session ở lần refresh kế tiếp
                        var freshTokens = _jwtTokenService.GenerateTokens(u.Id, u.Email, u.Role, u.Status);
                        var freshTokenHash = _jwtTokenService.HashRefreshToken(freshTokens.RefreshToken);

                        var branchToken = RefreshToken.Create(
                            u.Id,
                            freshTokenHash,
                            DateTime.UtcNow.AddDays(7));

                        await _authDbContext.RefreshTokens.AddAsync(branchToken, cancellationToken);
                        await _authDbContext.SaveChangesAsync(cancellationToken);

                        return Result<LoginResponse>.Success(new LoginResponse(
                            freshTokens.AccessToken,
                            freshTokens.RefreshToken,
                            freshTokens.ExpiresInSeconds));
                    }
                }
            }

            // Vượt quá Grace Window -> Tấn công Token Reuse -> Thu hồi toàn bộ phiên của User
            var userTokens = await _authDbContext.RefreshTokens
                .Where(t => t.UserId == existingToken.UserId && t.RevokedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var t in userTokens)
            {
                t.Revoke();
            }

            await _authDbContext.SaveChangesAsync(cancellationToken);
            return Result<LoginResponse>.Failure("Cảnh báo bảo mật: Token đã thu hồi bị sử dụng lại ngoài thời gian cho phép. Mọi phiên làm việc đã bị hủy.");
        }

        if (existingToken.IsExpired)
        {
            return Result<LoginResponse>.Failure("Refresh token đã hết hạn. Vui lòng đăng nhập lại.");
        }

        var userResult = await _sender.Send(new GetUserByIdQuery(existingToken.UserId), cancellationToken);
        if (!userResult.IsSuccess || userResult.Value is null || userResult.Value.Status != UserStatus.Active)
        {
            return Result<LoginResponse>.Failure("Người dùng không hợp lệ hoặc không ở trạng thái Active.");
        }

        var user = userResult.Value;

        // Cấp cặp token mới và xoay vòng
        var newTokens = _jwtTokenService.GenerateTokens(user.Id, user.Email, user.Role, user.Status);
        var newTokenHash = _jwtTokenService.HashRefreshToken(newTokens.RefreshToken);

        existingToken.Revoke(replacedByTokenHash: newTokenHash);

        var newRefreshToken = RefreshToken.Create(
            user.Id,
            newTokenHash,
            DateTime.UtcNow.AddDays(7));

        await _authDbContext.RefreshTokens.AddAsync(newRefreshToken, cancellationToken);
        await _authDbContext.SaveChangesAsync(cancellationToken);

        return Result<LoginResponse>.Success(new LoginResponse(
            newTokens.AccessToken,
            newTokens.RefreshToken,
            newTokens.ExpiresInSeconds));
    }
}
```

#### 5. Logout Flow & Get Current User (Me)
```csharp
// File: src/Modules/Auth/CodePath.Modules.Auth.Application/Features/Auth/Commands/LogoutCommand.cs
using CodePath.Modules.Auth.Application.Abstractions;
using CodePath.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodePath.Modules.Auth.Application.Features.Auth.Commands;

public sealed record LogoutCommand(string? RefreshToken, string Jti, DateTime TokenExpiresAtUtc, Guid CurrentUserId) : IRequest<Result<bool>>;

internal sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result<bool>>
{
    private readonly IAuthDbContext _authDbContext;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ITokenBlacklistService _blacklistService;

    public LogoutCommandHandler(
        IAuthDbContext authDbContext,
        IJwtTokenService jwtTokenService,
        ITokenBlacklistService blacklistService)
    {
        _authDbContext = authDbContext;
        _jwtTokenService = jwtTokenService;
        _blacklistService = blacklistService;
    }

    public async Task<Result<bool>> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var hash = _jwtTokenService.HashRefreshToken(request.RefreshToken);
            // N1 Fix: Kiểm tra chặt chẽ token thuộc về đúng user đang đăng xuất, chống việc revoke trộm token người khác
            var token = await _authDbContext.RefreshTokens
                .FirstOrDefaultAsync(t => t.TokenHash == hash && t.UserId == request.CurrentUserId, cancellationToken);

            if (token != null && !token.IsRevoked)
            {
                token.Revoke();
                await _authDbContext.SaveChangesAsync(cancellationToken);
            }
        }

        var remainingLifetime = request.TokenExpiresAtUtc - DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Jti) && remainingLifetime > TimeSpan.Zero)
        {
            await _blacklistService.BlacklistTokenAsync(request.Jti, remainingLifetime);
        }

        return Result<bool>.Success(true);
    }
}
```

```csharp
// File: src/Modules/Auth/CodePath.Modules.Auth.Application/Features/Auth/Queries/GetCurrentUserQuery.cs
using CodePath.Modules.Users.Application.Contracts.Users.Queries;
using CodePath.Shared.Kernel.Common;
using MediatR;

namespace CodePath.Modules.Auth.Application.Features.Auth.Queries;

public sealed record UserProfileResponse(Guid Id, string Email, string FullName, string Role, string Status);

public sealed record GetCurrentUserQuery(Guid UserId) : IRequest<Result<UserProfileResponse>>;

internal sealed class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, Result<UserProfileResponse>>
{
    private readonly ISender _sender;
    public GetCurrentUserQueryHandler(ISender sender) => _sender = sender;

    public async Task<Result<UserProfileResponse>> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var userResult = await _sender.Send(new GetUserByIdQuery(request.UserId), cancellationToken);
        if (!userResult.IsSuccess || userResult.Value is null)
        {
            return Result<UserProfileResponse>.Failure("Không tìm thấy thông tin người dùng.");
        }

        var user = userResult.Value;
        return Result<UserProfileResponse>.Success(new UserProfileResponse(
            user.Id, user.Email, user.FullName, user.Role.ToString(), user.Status.ToString()));
    }
}
```

---

### 5.5. Controller / Endpoints (`CodePath.Modules.Auth.Api`)

```csharp
// File: src/Modules/Auth/CodePath.Modules.Auth.Api/Contracts/AuthRequests.cs
namespace CodePath.Modules.Auth.Api.Contracts;

// Client KHÔNG được gửi Role/Status. DTO tuyệt đối không có 2 trường này!
public sealed record RegisterRequest(string FullName, string Email, string Password);
public sealed record VerifyOtpRequest(string Email, string Otp);
public sealed record ResendOtpRequest(string Email);
public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record LogoutRequest(string? RefreshToken);
```

```csharp
// File: src/Modules/Auth/CodePath.Modules.Auth.Api/Endpoints/AuthEndpoints.cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CodePath.Modules.Auth.Api.Contracts;
using CodePath.Modules.Auth.Application.Features.Auth.Commands;
using CodePath.Modules.Auth.Application.Features.Auth.Queries;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CodePath.Modules.Auth.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterRequest req, ISender sender) =>
        {
            var result = await sender.Send(new RegisterCommand(req.FullName, req.Email, req.Password));
            return result.IsSuccess
                ? Results.Ok(new { message = result.Value })
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("Register")
        .RequireRateLimiting("auth-rate-limit");

        group.MapPost("/verify-otp", async (VerifyOtpRequest req, ISender sender) =>
        {
            var result = await sender.Send(new VerifyOtpCommand(req.Email, req.Otp));
            return result.IsSuccess
                ? Results.Ok(new { message = result.Value })
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("VerifyOtp")
        .RequireRateLimiting("auth-rate-limit");

        group.MapPost("/resend-otp", async (ResendOtpRequest req, ISender sender) =>
        {
            var result = await sender.Send(new ResendOtpCommand(req.Email));
            return result.IsSuccess
                ? Results.Ok(new { message = result.Value })
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("ResendOtp")
        .RequireRateLimiting("auth-rate-limit");

        group.MapPost("/login", async (LoginRequest req, HttpContext httpContext, ISender sender, IConfiguration config) =>
        {
            var result = await sender.Send(new LoginCommand(req.Email, req.Password));
            if (result.IsSuccess)
            {
                // M3 Fix: Thiết lập Refresh Token vào Cookie HttpOnly; Secure; SameSite=Strict để chống XSS
                var isDev = string.Equals(config["ASPNETCORE_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase);
                httpContext.Response.Cookies.Append("refreshToken", result.Value!.RefreshToken, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = !isDev,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.AddDays(7)
                });

                return Results.Ok(result.Value);
            }

            // M5 Fix: Xử lý thống nhất qua ErrorCode từ Result pattern, trả đúng HTTP Status tương ứng
            return result.ErrorCode switch
            {
                "ACCOUNT_PENDING" or "ACCOUNT_REJECTED" or "ACCOUNT_DISABLED" =>
                    Results.Json(new { error = result.Error, code = result.ErrorCode }, statusCode: StatusCodes.Status403Forbidden),
                "UNAUTHORIZED" =>
                    Results.Json(new { error = result.Error, code = "UNAUTHORIZED" }, statusCode: StatusCodes.Status401Unauthorized),
                _ =>
                    Results.BadRequest(new { error = result.Error, code = result.ErrorCode ?? "BAD_REQUEST" })
            };
        })
        .WithName("Login")
        .RequireRateLimiting("login-rate-limit");

        group.MapPost("/refresh", async (RefreshTokenRequest? req, HttpContext httpContext, ISender sender, IConfiguration config) =>
        {
            // M3 Fix: Hỗ trợ đọc Refresh Token từ Cookie HttpOnly hoặc Body JSON (cho Swagger/Mobile)
            var tokenStr = req?.RefreshToken;
            if (string.IsNullOrWhiteSpace(tokenStr))
            {
                tokenStr = httpContext.Request.Cookies["refreshToken"];
            }

            if (string.IsNullOrWhiteSpace(tokenStr))
            {
                return Results.Json(new { error = "Refresh token không được để trống.", code = "UNAUTHORIZED" }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var result = await sender.Send(new RefreshTokenCommand(tokenStr));
            if (result.IsSuccess)
            {
                var isDev = string.Equals(config["ASPNETCORE_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase);
                httpContext.Response.Cookies.Append("refreshToken", result.Value!.RefreshToken, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = !isDev,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.AddDays(7)
                });

                return Results.Ok(result.Value);
            }

            return Results.Json(new { error = result.Error, code = result.ErrorCode ?? "UNAUTHORIZED" }, statusCode: StatusCodes.Status401Unauthorized);
        })
        .WithName("RefreshToken")
        .RequireRateLimiting("auth-rate-limit");

        group.MapPost("/logout", async (LogoutRequest? req, HttpContext httpContext, ISender sender, IConfiguration config) =>
        {
            var jti = httpContext.User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value ?? "";
            var expClaim = httpContext.User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;

            // N2 Fix: Đồng bộ thời gian hết hạn với cấu hình Jwt:AccessTokenExpiryMinutes thay vì hardcode 15 phút
            var expiryMinutes = int.TryParse(config["Jwt:AccessTokenExpiryMinutes"], out var mins) ? mins : 15;
            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(expiryMinutes);

            if (long.TryParse(expClaim, out var expSeconds))
            {
                expiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime;
            }

            var tokenStr = req?.RefreshToken ?? httpContext.Request.Cookies["refreshToken"];

            var sub = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                   ?? httpContext.User.FindFirst("sub")?.Value;
            Guid.TryParse(sub, out var currentUserId);

            // N1 Fix: Truyền UserId của người dùng hiện tại để kiểm tra quyền sở hữu token trước khi revoke
            await sender.Send(new LogoutCommand(tokenStr, jti, expiresAtUtc, currentUserId));

            // Xóa cookie refreshToken phía client
            httpContext.Response.Cookies.Delete("refreshToken");

            return Results.Ok(new { message = "Đăng xuất thành công." });
        })
        .WithName("Logout")
        .RequireAuthorization("ActiveUser");

        group.MapGet("/me", async (HttpContext httpContext, ISender sender) =>
        {
            var sub = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                   ?? httpContext.User.FindFirst("sub")?.Value;

            if (!Guid.TryParse(sub, out var userId))
                return Results.Unauthorized();

            var result = await sender.Send(new GetCurrentUserQuery(userId));
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.NotFound(new { error = result.Error });
        })
        .WithName("GetCurrentUser")
        .RequireAuthorization("ActiveUser");

        return app;
    }
}
```

---

## 6. BOOTSTRAPPER CONFIGURATION (`Program.cs`)

```csharp
// File: src/Bootstrapper/CodePath.Api/Program.cs
using System.Text;
using System.Threading.RateLimiting;
using CodePath.Modules.Auth.Api;
using CodePath.Modules.Auth.Application.Abstractions;
using CodePath.Modules.Auth.Infrastructure.Authorization;
using CodePath.Modules.Users.Api;
using CodePath.Shared.Kernel.Enums;
using CodePath.Shared.Web.Contracts;
using CodePath.Shared.Web.Extensions;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// 1. Hạ tầng dùng chung (Redis, Swagger)
builder.Services.AddSharedInfrastructure(builder.Configuration);

// 2. JWT Authentication & Redis Blacklist Check
var jwtSecret = builder.Configuration["Jwt:SigningKey"] 
    ?? throw new InvalidOperationException("Jwt:SigningKey must be provided.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "CodePath.Api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "CodePath.Client";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Chỉ tắt RequireHttpsMetadata ở môi trường Development
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromSeconds(5)
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var blacklistService = context.HttpContext.RequestServices.GetRequiredService<ITokenBlacklistService>();
                var jti = context.Principal?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
                if (!string.IsNullOrEmpty(jti) && await blacklistService.IsBlacklistedAsync(jti))
                {
                    context.Fail("Token has been revoked.");
                }
            }
        };
    });

// 3. Cấu hình CORS (N4 Fix: Hỗ trợ Frontend SPA gửi cookie HttpOnly an toàn)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
            ?? new[] { "http://localhost:3000", "http://localhost:5173" };

        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Bắt buộc true để trình duyệt đính kèm cookie HttpOnly
    });
});

// 4. Authorization Policies (Kiểm tra trạng thái Active của tài khoản qua cache/DB)
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ActiveUser", policy =>
        policy.Requirements.Add(new ActiveUserRequirement()));

    options.AddPolicy("ActiveStudent", policy =>
        policy.Requirements.Add(new ActiveUserRequirement(UserRole.Student)));

    options.AddPolicy("ActiveTeacher", policy =>
        policy.Requirements.Add(new ActiveUserRequirement(UserRole.Teacher)));

    options.AddPolicy("AdminOnly", policy =>
        policy.Requirements.Add(new ActiveUserRequirement(UserRole.Admin)));
});

// 5. Rate Limiting (M4 Fix: Phân tách policy riêng theo độ nhạy cảm của endpoint)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Policy chung cho Register, Verify OTP, Resend OTP, Refresh Token (20 req/phút/IP)
    options.AddPolicy("auth-rate-limit", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });

    // Policy riêng cho Login chống Brute-force mật khẩu (5 req/5 phút/IP)
    options.AddPolicy("login-rate-limit", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter($"login:{clientIp}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0
        });
    });
});

// 6. Đăng ký Modules
IModule[] modules = [
    new AuthModule(),
    new UsersModule()
];

foreach (var module in modules)
{
    module.RegisterModule(builder.Services, builder.Configuration);
}

var app = builder.Build();

app.UseAppExceptionHandling();

// Cấu hình ForwardedHeaders khi deploy sau Reverse Proxy (Nginx / Docker)
// Bắt buộc đặt TRƯỚC UseHttpsRedirection, UseCors, UseRateLimiter, UseAuthentication
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
// M2 Fix: Whitelist dải mạng Docker/K8s/Nginx nội bộ thay vì Clear() vô điều kiện để tránh client bypass rate limiter khi kết nối trực tiếp Kestrel
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();

var proxySubnet = builder.Configuration["ReverseProxy:KnownNetwork"] ?? "172.16.0.0/12";
if (System.Net.IPNetwork.TryParse(proxySubnet, out var network))
{
    forwardedHeadersOptions.KnownNetworks.Add(network);
}
else
{
    forwardedHeadersOptions.KnownNetworks.Add(new System.Net.IPNetwork(System.Net.IPAddress.Parse("172.16.0.0"), 12));
    forwardedHeadersOptions.KnownNetworks.Add(new System.Net.IPNetwork(System.Net.IPAddress.Parse("10.0.0.0"), 8));
}

app.UseForwardedHeaders(forwardedHeadersOptions);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "CodePath API v1"));
}

app.UseHttpsRedirection();

app.UseCors();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

foreach (var module in modules)
{
    module.MapEndpoints(app);
}

app.Run();

public partial class Program { }
```

---

## 7. Edge Cases & Chiến Lược Kiểm Thử (Testing Matrix)

| Kịch bản kiểm thử | Kỳ vọng |
|---|---|
| Email không phải `@hanu.edu.vn` (vd: `user@gmail.com`) | ValidationException: 400 Bad Request |
| Email sinh viên `20231234@hanu.edu.vn` | Phân loại `Role = Student`, `Status = Active`, `StudentId = 20231234` |
| Email giảng viên `sondo@hanu.edu.vn` | Phân loại `Role = Teacher`, `Status = Pending`, `StudentId = null` |
| Client cố tình gửi kèm `role` hoặc `status` trong body | DTO loại bỏ hoàn toàn, không map vào Command |
| 2 request đăng ký cùng lúc 1 email chưa từng có (race condition) | Bắt `DbUpdateException` (Postgres Unique Violation) -> trả 409 Conflict |
| Email squatting: Đăng ký email đã có người đăng ký nhưng chưa verify OTP | Cho phép cập nhật lại thông tin/mật khẩu mới, cấp lại mã OTP mới mà không báo lỗi 409 Conflict |
| Đăng ký email đã được xác thực trước đó | Báo lỗi 409 Conflict: `"Email already exists in the system."` |
| Đăng nhập tài khoản chưa verify OTP | Trả HTTP 401 kèm JSON `{ error: "Email chưa được xác thực...", code: "UNAUTHORIZED" }` |
| Đăng nhập sai email hoặc sai mật khẩu | Trả HTTP 401 kèm JSON `{ error: "Email hoặc mật khẩu không chính xác.", code: "UNAUTHORIZED" }` |
| Đăng nhập tài khoản `Pending` (Giảng viên) | Trả 403 Forbidden với mã lỗi `ACCOUNT_PENDING` |
| Đăng nhập tài khoản `Rejected` | Trả 403 Forbidden với mã lỗi `ACCOUNT_REJECTED` |
| Đăng nhập tài khoản `Disabled` | Trả 403 Forbidden với mã lỗi `ACCOUNT_DISABLED` |
| OTP nhập sai quá 5 lần (kể cả request đồng thời) | Lua Script Redis thực thi atomic: xóa key OTP, trả thông báo đã nhập sai quá số lần quy định |
| Resend OTP liên tiếp trong vòng 60 giây | Trả lỗi cooldown kèm thời gian chờ còn lại |
| 2 tab trình duyệt cùng refresh token trong vòng 30s (Grace Window) | Cả 2 tab đều nhận được cặp Access + Refresh Token MỚI hợp lệ, session của cả 2 tab tiếp tục hoạt động trơn tru, không bị sập ở lần refresh kế tiếp |
| Dùng lại Refresh Token đã revoked ngoài Grace Window (> 30s) | Phát hiện tấn công Token Replay Attack -> Thu hồi toàn bộ Refresh Tokens của User |
| Dùng Access Token sau khi đã gọi Logout | Token đã bị blacklist JTI trong Redis -> 401 Unauthorized |
| User bị Admin chuyển `Disabled` gọi `/me` hoặc `/logout` | `UpdateUserStatusCommand` xóa ngay cache Redis `user:status:{userId}` -> Chặn ngay lập tức (403 Forbidden) mà không cần chờ TTL 5 phút |
| Lỗi hệ thống máy chủ (DB connection, 500 Internal Error) | Ghi log `TraceId` và stack trace lên máy chủ; response JSON chỉ trả thông báo chung an toàn, tuyệt đối không leak câu lệnh SQL/cột Postgres |
| Đăng ký với StudentId > 50 ký tự hoặc FullName > 200 ký tự | Domain validation ném `ArgumentException`, middleware chuyển thành 400 Bad Request, không bị crash unhandled `DbUpdateException` |
| Người dùng gửi request logout với token của người khác | `LogoutCommandHandler` kiểm tra `t.UserId == currentUserId` -> Không thể revoke trộm token phiên của tài khoản khác |
| Rate limit Login (Brute-force) | Giới hạn 5 lần thử/5 phút theo IP (`login-rate-limit`) -> Chặn tấn công dò mật khẩu |
| Rate limit sau Reverse Proxy (Nginx/Docker) | `UseForwardedHeaders` kết hợp whitelist mạng nội bộ (`172.16.0.0/12`) nhận đúng IP client thực tế -> Không dồn vào 1 bucket IP proxy và chống client giả mạo header |

---

## 8. Lưu ý Kiến trúc & Triển khai Tiếp theo (Phase 1 Follow-ups)

1. **Email Service Asynchronous (N7)**:
   - Trong Phase 1, `EmailSender` tạm thời là mock logger để chạy thử nghiệm.
   - Khi tích hợp SMTP/SendGrid/SES thật ở Phase 2, bắt buộc chuyển sang mô hình **Background Job / Message Queue** (sử dụng Hangfire, RabbitMQ hoặc Outbox Pattern) để việc gửi email không chặn HTTP request pipeline của người dùng.
2. **Quản lý Refresh Token Cookie (M3)**:
   - Frontend web SPA khuyến nghị đọc Access Token từ JSON response (lưu trong memory) và để trình duyệt tự động quản lý Refresh Token qua cookie `HttpOnly; Secure; SameSite=Strict`.
   - Các client mobile/desktop có thể tiếp tục gửi Refresh Token qua JSON body nếu không hỗ trợ cookie jar.
