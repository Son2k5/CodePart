---
kind: configuration_system
name: 'Configuration System: Layered .NET Options + Environment Variables Across Three Services'
category: configuration_system
scope:
    - '**'
source_files:
    - api/src/Api/CodePath.Api/Program.cs
    - api/src/Api/CodePath.Api/appsettings.json
    - api/src/Api/CodePath.Api/appsettings.Development.json
    - api/src/Infrastructure/CodePath.Infrastructure/DependencyInjection.cs
    - api/src/Infrastructure/CodePath.Infrastructure/Auth/Options/JwtOptions.cs
    - api/src/Infrastructure/CodePath.Infrastructure/Auth/Options/AuthSessionOptions.cs
    - api/src/Infrastructure/CodePath.Infrastructure/Auth/Options/LoginSecurityOptions.cs
    - api/src/Infrastructure/CodePath.Infrastructure/Auth/Options/SmtpOptions.cs
    - api/src/Infrastructure/CodePath.Infrastructure/Auth/Options/AuthFrontendOptions.cs
    - api/src/Infrastructure/CodePath.Infrastructure/Auth/Options/LoginConcurrencyOptions.cs
    - api/src/Infrastructure/CodePath.Infrastructure/Auth/Options/PasswordHashingOptions.cs
    - ai-service/app/main.py
    - ai-service/.env.example
    - frontend/vite.config.ts
    - frontend/.env.example
---

## Overview

The monorepo configures three independent services, each with its own configuration strategy:

- **CodePath API (ASP.NET Core)** — Microsoft.Extensions.Configuration with typed `Options` classes, `appsettings.json`, environment-specific overrides, `.env` via `DotNetEnv`, Docker/Kubernetes file secrets under `/run/secrets`, and process environment variables.
- **AI Service (FastAPI/Python)** — pure `os.environ` reads; no implicit `.env` loading is performed (the `.env.example` explicitly says "no .env is loaded implicitly").
- **Frontend (Vite/React)** — Vite's `loadEnv` with the `VITE_` prefix for build-time constants; a dev proxy pointing at the backend.

There is no shared configuration library across the three services; each service owns its own configuration surface.

## ASP.NET API Configuration Sources (in load order)

1. `Program.cs` calls `WebApplication.CreateBuilder(args)` which loads the default ASP.NET sources (`appsettings.json`, `appsettings.{Environment}.json`, environment variables, command-line).
2. In development only (`builder.Environment.IsDevelopment()`), `DotNetEnv.Env.TraversePath().Load()` is called to load `.env` files from the current directory upward.
3. `builder.Configuration.AddEnvironmentVariables()` is then added in development (redundant with the default source, but explicit).
4. If `/run/secrets` exists (Docker/Kubernetes), every file in that directory is read as a key-value pair where the filename is normalized by replacing `__` with `:` (e.g. `Database__Password` → `Database:Password`) and trailing `\r\n` are trimmed.
5. `ASPNETCORE_URLS` / `urls` config value is honored to override Kestrel binding URLs.

Key files:
- `api/src/Api/CodePath.Api/appsettings.json` — base defaults for Judge, Logging, Jwt, ConnectionStrings, Infrastructure.MySql/Redis, Cors, Auth, Email, RateLimiting, CodeExecution.Judge0, ReverseProxy, AdminSeed.
- `api/src/Api/CodePath.Api/appsettings.Development.json` — Development-only overrides (Logging LogLevel, Cors AllowedOrigins, AuthFrontend.PublicBaseUrl, ReverseProxy.KnownProxies).
- `api/.env`, `api/.env.ai.example`, `api/.env.judge.example`, `api/.env.production.example` — per-environment secret/environment templates.

## Typed Options Pattern

Every configuration section is bound to a strongly-typed class in `Infrastructure/Auth/Options/` (and related folders) using `AddOptions<T>().Bind(...).ValidateDataAnnotations().ValidateOnStart()`. The options classes define a `SectionName` constant used for binding:

- `JwtOptions` — `Jwt` section
- `SmtpOptions` — `Email.Smtp`
- `AuthFrontendOptions` — `AuthFrontend`
- `LoginSecurityOptions` — `Auth.LoginProtection`
- `PasswordHashingOptions` — `Auth.PasswordHashing`
- `AuthSessionOptions` — `Auth.Session`
- `LoginConcurrencyOptions` — `Auth.LoginConcurrency`
- `Judge0Options` — `CodeExecution.Judge0`
- `ReverseProxyTrustOptions` — `ReverseProxy`
- `AuthRateLimitingOptions` — `Auth.RateLimiting`
- `DatabasePoolOptions` — `Infrastructure.MySql`
- `RedisConnectionOptions` — `Infrastructure.Redis`

Validation is centralized in `Infrastructure/DependencyInjection.cs` via custom `.Validate(...)` lambdas plus `[Required]`/range attributes on the option classes. Examples of enforced rules:
- `Jwt:SigningKey` must be present and ≥ 64 characters (checked in `Program.cs` before DI setup).
- `Auth:PasswordHashing:WorkFactor` must be between 10 and 16.
- `Auth:Session:RefreshTokenAbsoluteLifetimeDays` must equal exactly 7 days.
- `Auth:Session:CookieSameSite` must be one of `Strict`, `Lax`, `None`.
- `Auth:RateLimiting:BurstCapacity` / `RefillPerMinute` must be between 1 and 100000; `QueueLimit` must be zero.
- `ReverseProxy:ForwardLimit` must be between 1 and 10; `KnownNetworks` must contain valid CIDR addresses.
- SMTP and `AuthFrontend:PublicBaseUrl` must be fully configured in production (validation skips when `IsDevelopment()` or `IsEnvironment("Testing")`).
- MySQL pool: `MinPoolSize ≤ MaxPoolSize`, both within 0–10000; `ConnectionIdleLifetimeSeconds` within 0–86400.

## Database Connection Resolution

In `Infrastructure/DependencyInjection.cs`, the connection string is resolved in this precedence:
1. `ConnectionStrings:Database` (standard ASP.NET convention)
2. `DATABASE_URL` environment variable
3. If neither is set, it builds a connection string from `Database:Host`, `Database:Port`, `Database:Name`, `Database:User`, `Database:Password` (with defaults `localhost`, `3306`, `codepath`, `codepath`). At least `Database:Password` is required if the above two paths are empty.

The resulting string is passed through `MySqlConnectionStringBuilder` to add `GuidFormat=Binary16`, `DateTimeKind=Utc`, `CharacterSet=utf8mb4`, and pool settings from `DatabasePoolOptions`.

## AI Service Configuration

The FastAPI service reads everything directly from `os.environ` inside `app/main.py`'s lifespan:
- `SERVICE_API_KEY` — validated to be ≥ 32 characters at startup; used as an HMAC-compare against the `X-Service-Key` header for inter-service auth.
- `REDIS_URL` — optional; if absent, Redis cache is disabled.
- `LLM_PROVIDER`, `LLM_API_KEY`, `LLM_MODEL`, `LLM_ADVANCED_MODEL`, `LLM_BASE_URL` — LLM client configuration.
- `MODEL_PATH`, `MODEL_MANIFEST` — optional classifier model path; manifest is parsed as JSON to resolve the active model path.
- `QDRANT_URL`, `QDRANT_API_KEY`, `EMBEDDING_BASE_URL`, `EMBEDDING_MODEL`, `EMBEDDING_DIMENSIONS`, `EMBEDDING_API_KEY`, `RAG_MIN_SCORE`, `RAG_ASSET_HOSTS` — RAG/vector store configuration.
- `ENVIRONMENT` — controls whether Swagger docs (`/docs`, `/redoc`, `/openapi.json`) are enabled.

No typed configuration objects exist; validation is minimal (key length check for `SERVICE_API_KEY`).

## Frontend Configuration

`vite.config.ts` uses Vite's `loadEnv(mode, process.cwd(), 'VITE_')` to load env vars prefixed with `VITE_`. Build-time constants include:
- `VITE_API_BASE_URL` — backend base URL.
- `VITE_ENABLE_MOCKS` / `VITE_USE_MOCK` — enables MSW-based mock mode.

During dev, the Vite server proxies `/api`, `/health`, `/swagger` to `https://localhost:5001` using the local TLS cert/key from `api/secrets/dev/localhost.pem` and `api/secrets/dev/localhost.key`.

## Secrets Handling

- **ASP.NET**: Docker/Kubernetes secrets mounted under `/run/secrets` are auto-loaded into `IConfiguration` with filename-to-section-name normalization (`__` → `:`). This allows secrets like `Jwt__SigningKey` to be injected without modifying code.
- **.env files**: Only loaded in development via `DotNetEnv`; not loaded in production.
- **AI service**: No `.env` loading; expects all values in the process environment.
- **Frontend**: `VITE_*` env vars are baked into the build output at compile time.

## Key Files

- `api/src/Api/CodePath.Api/Program.cs` — application bootstrap, config source wiring, feature flags (`Judge:Enabled`, `Ai:Enabled`), rate limiter policies, reverse proxy trust.
- `api/src/Api/CodePath.Api/appsettings.json` — base configuration defaults.
- `api/src/Api/CodePath.Api/appsettings.Development.json` — development overrides.
- `api/src/Infrastructure/CodePath.Infrastructure/DependencyInjection.cs` — typed options registration, validation, database connection resolution.
- `api/src/Infrastructure/CodePath.Infrastructure/Auth/Options/*.cs` — typed configuration classes for auth subsystem.
- `ai-service/app/main.py` — FastAPI app startup, env-driven initialization, health endpoints.
- `ai-service/.env.example` — documented environment variable reference for the AI service.
- `frontend/vite.config.ts` — Vite env loading, dev proxy, mock mode toggle.
- `frontend/.env.example` — frontend env var reference.