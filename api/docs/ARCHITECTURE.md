# CodePath - Kiến trúc Layered Clean Architecture

## 1. Bố cục thư mục (Layered Clean Architecture)

```text
src/
  Api/CodePath.Api              # Presentation / Web API (Endpoints, Contracts, Program.cs)
  Application/CodePath.Application  # Business logic (Commands, Queries, Handlers, Validators, DTOs, Abstractions)
  Domain/CodePath.Domain        # Entities, Enums, Domain Exceptions (thuần domain, không dependency ra ngoài)
  Infrastructure/CodePath.Infrastructure # EF Core DbContexts, Configurations, Migrations, External Services
  Shared/
    CodePath.Shared.Kernel      # BaseEntity, Result, Events, Exceptions
    CodePath.Shared.Web         # Web helpers: Swagger, ExceptionHandling, Redis
tests/
  CodePath.Architecture.Tests   # Kiểm tra quy ước kiến trúc
  CodePath.Unit.Tests           # Unit tests
  CodePath.Integration.Tests    # Integration tests với MySQL 8.4 và Redis
```

## 2. Quy ước phụ thuộc (Clean Architecture)

- `Api -> Application + Infrastructure + Shared.Web`
- `Infrastructure -> Application`
- `Application -> Domain + Shared.Kernel`
- `Domain -> Shared.Kernel`
- Chiều phụ thuộc luôn đi một chiều vào trong: `Domain` và `Shared.Kernel` không phụ thuộc bất kỳ layer nào bên ngoài.

## 3. CQRS & Mapping

- Sử dụng CQRS (Command/Query separation) với MediatR.
- `Application/{Feature}/Commands/`: Chứa Command, Handler, Validator.
- `Application/{Feature}/Queries/`: Chứa Query, Handler.
- `Application/{Feature}/Dtos/`: Chứa DTOs (`sealed record`) và manual mapping extension methods (`user.ToUserAuthDto()`).
- Bỏ hoàn toàn AutoMapper, sử dụng manual mapping thuần túy.

## 4. Dữ liệu & Persistence

- Một `AppDbContext` dùng chung cho các feature trong kiến trúc layer thuần; đây không phải Modular Monolith.
- MySQL dùng một database `codepath`; không dùng EF schema kiểu PostgreSQL.
- `IDbContext` abstractions (`IAuthDbContext`, `IUsersDbContext`) đặt tại `Application/{Feature}/Abstractions`.
- `AppDbContext` và các entity configuration đặt tại `Infrastructure/Persistence`.
