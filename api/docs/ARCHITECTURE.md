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
  Tools/
    CodePath.Migrator           # Tool chạy Database Migrations độc lập
tests/
  CodePath.Architecture.Tests   # Kiểm tra quy ước kiến trúc
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

- Các DbContext tách schema rõ ràng (`auth`, `users`).
- Entity configurations được phân lập theo namespace để tránh cross-schema configuration.
- `IDbContext` abstractions (`IAuthDbContext`, `IUsersDbContext`) đặt tại `Application/{Feature}/Abstractions`.
- `DbContext` implementations đặt tại `Infrastructure/{Feature}/Persistence`.
