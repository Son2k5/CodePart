# CodePath - Kien truc Modular Monolith

## 1. Brut cuc

```text
src/
  Bootstrapper/CodePath.Api      # Composition Root, chi biet IModule
  Shared/
    CodePath.Shared.Kernel       # BaseEntity, Result, Events, Exceptions (thuan domain)
    CodePath.Shared.Web          # IModule, ValidationBehavior, Exception/Swagger
  Modules/{Module}/
    {Module}.Domain              # Entity, Enum, Exception, Event
    {Module}.Application         # Abstractions, Contracts (public), Features (internal), DI
    {Module}.Infrastructure      # DbContext (schema rieng), Configurations, Migrations, DI
    {Module}.Api                 # Module.cs, Endpoints, Contracts (Request/Response)
tests/
  CodePath.Architecture.Tests    # Quy uoc module boundary
```

## 2. Quy uoc phu thuoc (Clean Architecture)

- `Api -> Application + Infrastructure + Shared.Web`
- `Infrastructure -> Application`
- `Application -> Domain + Shared.Kernel`
- `Domain -> Shared.Kernel`
- `Bootstrapper -> *.Api + Shared.Web` (khong ref thang Application/Infrastructure/Domain)
- Cam `ProjectReference` cheo `Domain/Infrastructure` giua cac module.

## 3. Handler-cross (CQRS, cam Service)

- Cam `Services/*Service.cs` trong `*.Application`.
- Moi usecase la `Command/Query + Handler + Validator` (MediatR).
- `Contracts/` chua `public record : IRequest<T>` + `DTO`. Module khac goi qua `ISender.Send()`.
- `Features/` chua `internal Handler + Validator`.
- Khong doc `DbContext` / `Domain.Entity` cua module khac.

## 4. Du lieu

- Moi module 1 `DbContext` + 1 schema Postgres rieng (`users`, `problems`, ...).
- Chung 1 physical database, `MigrationsHistoryTable` rieng tung schema.
- FK lien module chi luu `Guid` thuan, khong navigation cross-module.

## 5. Them module moi

1. Tao 4 project theo mau `Modules/Users`.
2. Tao `*Module : IModule`, `*DbContext`, `DependencyInjection`.
3. Them `ProjectReference` vao `CodePath.Api.csproj` + `new XModule()` vao `Program.cs` + `.sln`.
