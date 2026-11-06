---
kind: logging_system
name: 'Logging System: Microsoft.Extensions.Logging + Python stdlib logging'
category: logging_system
scope:
    - '**'
source_files:
    - api/src/Api/CodePath.Api/Middleware/CorrelationIdMiddleware.cs
    - api/src/Api/CodePath.Api/Program.cs
    - api/src/Shared/CodePath.Shared.Kernel/Behaviors/LoggingBehavior.cs
    - api/src/Application/CodePath.Application/Auth/Commands/ForgotPasswordCommand.cs
    - api/src/Application/CodePath.Application/Auth/Commands/RegisterCommand.cs
    - api/src/Application/CodePath.Application/Auth/Commands/ResendOtpCommand.cs
    - api/src/Application/CodePath.Application/Auth/Commands/ResetPasswordCommand.cs
    - api/src/Infrastructure/CodePath.Infrastructure/Auth/Services/Email/EmailSender.cs
    - api/src/Infrastructure/CodePath.Infrastructure/Auth/Services/LoginProtection/RedisLoginAttemptService.cs
    - api/src/Infrastructure/CodePath.Infrastructure/Auth/Services/Tokens/RefreshTokenCleanupService.cs
    - api/src/Infrastructure/CodePath.Infrastructure/CodeExecution/Judge0CodeExecutionService.cs
    - api/src/Infrastructure/CodePath.Infrastructure/Learning/RagIndexWorker.cs
    - api/src/Infrastructure/CodePath.Infrastructure/Learning/RagService.cs
    - api/src/Api/CodePath.Api/Extensions/DatabaseMigrationExtensions.cs
    - ai-service/app/api/pipeline.py
---

## Overview

The monorepo uses two independent logging stacks, one per service:

- **ASP.NET API (C#)** — `Microsoft.Extensions.Logging` with structured log messages via `ILogger<T>` injected through DI.
- **AI/RAG Service (Python/FastAPI)** — Python's built-in `logging` module (`logging.getLogger('hint_guard')`).

There is no shared logger abstraction across services; each language/runtime configures its own sinks.

## ASP.NET API (CodePath.Api)

### Framework and registration
- All loggers are obtained via constructor injection of `ILogger<T>` from `Microsoft.Extensions.Logging` (e.g. `ILogger<CorrelationIdMiddleware>`, `ILogger<ForgotPasswordCommandHandler>`, `ILogger<EmailSender>`).
- No third-party logging provider (Serilog, NLog, etc.) is referenced — the default console sink provided by ASP.NET Core is used.
- There is no explicit `builder.Logging.AddConsole(...)` call in `Program.cs`; the default host builder supplies it.

### Correlation / tracing
- `CorrelationIdMiddleware` (src/Api/CodePath.Api/Middleware/CorrelationIdMiddleware.cs) is the central correlation point:
  - Reads an incoming `X-Correlation-ID` header (validated to ≤128 alphanumeric/dash/underscore/dot characters); otherwise falls back to `Activity.Current?.TraceId` or a new GUID.
  - Sets `HttpContext.TraceIdentifier` to that value so ASP.NET diagnostic traces carry it.
  - Wraps request processing in `_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId })`, attaching `CorrelationId` as a structured property to every subsequent log line in the scope.
  - Echoes the same ID back on the response header `X-Correlation-ID`.
- Error responses produced by JWT bearer events, rate-limiting, and health checks append `traceId` (from `HttpContext.TraceIdentifier`) into `ProblemDetails.Extensions["traceId"]`.

### MediatR behavior
- `LoggingBehavior<TRequest,TResponse>` (src/Shared/CodePath.Shared.Kernel/Behaviors/LoggingBehavior.cs) is a MediatR pipeline behavior that logs:
  - `LogInformation` at start with `{RequestName}`.
  - `LogWarning` when the returned `IResult` is unsuccessful, including `{ErrorCode}`, `{Error}`, and elapsed ms.
  - `LogInformation` on success with elapsed ms.
  - `LogError(ex, ...)` on unhandled exceptions, rethrowing them.

### Log levels used
| Level | Typical use | Example locations |
|---|---|---|
| `LogInformation` | Lifecycle / progress events (DB migration, email sent, seeder, cleanup counts) | `DatabaseMigrationExtensions`, `EmailSender`, `RefreshTokenCleanupService`, `ExerciseSeeder` |
| `LogWarning` | Recoverable / expected anomalies (Judge0 HTTP status, RAG worker retry, failed RAG hint retrieval) | `Judge0CodeExecutionService`, `RagIndexWorker`, `RagService` |
| `LogError` | Failures (OTP delivery failures, DB migration/seeding failure, Judge0 connection failure, refresh-token cleanup failure) | `ForgotPasswordCommand`, `RegisterCommand`, `ResendOtpCommand`, `ResetPasswordCommand`, `DatabaseMigrationExtensions`, `Judge0CodeExecutionService`, `RefreshTokenCleanupService` |
| `LogDebug` | Suppressed email sends in testing | `EmailSender` |
| `LogCritical` | Fatal DB migration/seeding errors | `DatabaseMigrationExtensions` |

### Structured fields
Structured properties are passed as named placeholders in the message template (e.g. `{Recipient}`, `{StatusCode}`, `{Count}`, `{JobId}`, `{ErrorCode}`, `{ElapsedMilliseconds}`, `{RequestName}`). The `CorrelationId` middleware additionally injects a top-level `CorrelationId` property via `BeginScope`.

### Configuration
No `appsettings.json` overrides for logging; the project does not reference Serilog/NLog. Sinks are controlled by the default ASP.NET Core host (console output). Environment-specific `.env` files are loaded only in development via `DotNetEnv`.

## AI/RAG Service (Python FastAPI)

- Uses the standard library `logging` module.
- A single logger instance is created at module level: `log = logging.getLogger('hint_guard')` in `ai-service/app/api/pipeline.py`.
- Only `log.warning(...)` calls are present in the production code path (cache read/write failures, guard-blocked hints).
- No custom formatter, handler, or log rotation is configured in the scanned files; the root logger defaults apply.
- Logs are plain text strings rather than structured JSON.

## Frontend

The React/Vite frontend does not include a dedicated logging framework. It relies on browser console APIs (not captured here) and has no centralized logger abstraction.

## Conventions observed

- C# code never instantiates loggers directly; they are always injected via constructor parameters typed as `ILogger<T>`.
- Correlation IDs flow end-to-end through `CorrelationIdMiddleware`'s `BeginScope`, making `CorrelationId` available to all downstream `ILogger` calls within a request.
- Error paths consistently pass the exception as the first argument to `LogError` (e.g. `logger.LogError(exception, "...", args...)`) so stack traces are captured.
- Success/lifecycle events use `LogInformation`; recoverable warnings use `LogWarning`; unrecoverable failures use `LogError`.
- Python-side logging is minimal and uses the generic `'hint_guard'` logger name with simple string templates.