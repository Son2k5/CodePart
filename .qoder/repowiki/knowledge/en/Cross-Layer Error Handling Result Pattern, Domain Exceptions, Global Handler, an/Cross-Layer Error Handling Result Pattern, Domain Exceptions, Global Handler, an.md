---
kind: error_handling
name: 'Cross-Layer Error Handling: Result Pattern, Domain Exceptions, Global Handler, and Frontend Normalization'
category: error_handling
scope:
    - '**'
source_files:
    - api/src/Shared/CodePath.Shared.Kernel/Common/ErrorCodes.cs
    - api/src/Shared/CodePath.Shared.Kernel/Common/ErrorCatalog.cs
    - api/src/Shared/CodePath.Shared.Kernel/Common/Result.cs
    - api/src/Domain/CodePath.Domain/Common/Exceptions/DomainException.cs
    - api/src/Domain/CodePath.Domain/Common/Exceptions/ConflictDomainException.cs
    - api/src/Domain/CodePath.Domain/Common/Exceptions/DomainRuleViolationException.cs
    - api/src/Domain/CodePath.Domain/Common/Exceptions/DomainValidationException.cs
    - api/src/Shared/CodePath.Shared.Web/Exceptions/GlobalExceptionHandler.cs
    - api/src/Api/CodePath.Api/Extensions/ResultExtensions.cs
    - api/src/Shared/CodePath.Shared.Kernel/Behaviors/ValidationBehavior.cs
    - api/src/Shared/CodePath.Shared.Kernel/Behaviors/LoggingBehavior.cs
    - frontend/src/lib/errorService.ts
    - ai-service/app/main.py
---

## Overview

The CodePath monorepo implements a consistent error-handling strategy across three layers — ASP.NET Core API (C#), FastAPI AI service (Python), and React frontend (TypeScript) — centered on typed error codes, a `Result<T>` pattern in the backend, a global exception handler that maps exceptions to RFC 7807 `ProblemDetails`, and a frontend error normalizer that translates server error codes into user-facing Vietnamese messages.

## Backend (ASP.NET Core)

### Exception hierarchy

- `AppException` (`CodePath.Shared.Kernel.Exceptions`) is the base class for all application-level errors. It carries an `ErrorCode` string property and defaults to `"BAD_REQUEST"`.
- `DomainException` (`CodePath.Domain.Common.Exceptions`) extends `AppException` with a default code of `ErrorCodes.BadRequest`.
- Concrete domain exceptions are sealed subclasses of `DomainException`: `ConflictDomainException`, `DomainRuleViolationException`, `DomainValidationException` (which additionally exposes `PropertyName`).
- Kernel-level HTTP-oriented exceptions live under `CodePath.Shared.Kernel.Exceptions`: `AppException`, `ConflictException`, `ForbiddenException`, `NotFoundException`, `UnauthorizedAppException`.

All domain/business errors are thrown as exceptions from handlers/services; they are never returned as values.

### Centralized error-code registry

`ErrorCodes` (`CodePath.Shared.Kernel.Common`) is a static class enumerating every stable error identifier used across the system:

- Generic: `BadRequest`, `Unauthorized`, `Forbidden`, `NotFound`, `Conflict`, `ValidationError`, `InternalServerError`, `ServiceUnavailable`, `TooManyRequests`, `RateLimitExceeded`, `InfraError`.
- Auth: `AuthInvalidCredentials`, `AuthSessionExpired`, `AuthOtpInvalid`, `SessionIdleTimeout`, `SessionExpired`.
- Classes/Learning: `TopicLocked`, `NotEnrolled`, `ClassNotFound`, `EnrollmentNotFound`, `ClassFull`, `AlreadyEnrolled`, `ClassHasStudents`, `ClassConcurrencyConflict`, `InvalidJoinCode`, `ContentInvalid`, `FileTooLarge`, `AssetNotReady`, `FeatureNotEnabled`.
- Judge: `SubmissionInProgress`, `CompileError`, `RuntimeError`, `TimeLimitExceeded`, `MemoryLimitExceeded`.
- AI/RAG: `AiUnavailable`, `RagUnavailable`, `RagDisabled`, `ContextChanged`.

### User-message catalog and sanitization

`ErrorCatalog` (`CodePath.Shared.Kernel.Common`) maps each `ErrorCodes` value to a `(StatusCode, Title, SafeMessage)` triple in Vietnamese. It also provides:

- `GetDetails(errorCode, rawMessage)` — returns catalog entry or falls back to sanitized message.
- `GetSafeMessage(errorCode, rawMessage)` — returns only the safe message.
- `SanitizeMessage(message)` — strips technical details using a compiled regex matching patterns like `exception`, `stacktrace`, `npgsql`, `mysql`, `sqlite`, `sqlclient`, `foreign key`, `unique constraint`, `duplicate entry`, `connectionstring`, `password=`, `secret`, `jwt`, `bearer`, `/app/`, drive-letter paths. If matched, it returns a generic admin-facing fallback.

This ensures raw stack traces and database internals never leak to clients.

### `Result<T>` pattern

`Result<T>` (`CodePath.Shared.Kernel.Common`) is a sealed value type implementing `IResult` and `IValidationResult`. It exposes `IsSuccess`, `Value`, `Error`, `ErrorCode`, `RetryAfter`, and `ValidationErrors`. Factory methods:

- `Result<T>.Success(value)`
- `Result<T>.Failure(error, errorCode = ErrorCodes.BadRequest, retryAfter?)`
- `Result<T>.ValidationFailure(IEnumerable<ValidationError>)` — aggregates FluentValidation failures into a single validation result.

Handlers return `Result<T>` instead of throwing for expected business failures (e.g., rate limiting, session expiry). The extension `ResultExtensions.ToHttpResult<T>()` converts `Result<T>` into an ASP.NET `IResult`:

- Success → `Results.Ok()` (or `{ message }` wrapper for strings).
- Session-expiry codes (`SESSION_IDLE_TIMEOUT`, `SESSION_EXPIRED`) → 401 with localized title/detail.
- Validation failures → `HttpValidationProblemDetails` grouped by field name.
- Other failures → `ProblemDetails` resolved via `ErrorCatalog.GetDetails`, with `code` and `traceId` extensions.

### Global exception handling

`GlobalExceptionHandler` (`CodePath.Shared.Web.Exceptions`) implements `Microsoft.AspNetCore.Diagnostics.IExceptionHandler` and is the single catch-all for unhandled exceptions:

| Exception type | Status | Behavior |
|---|---:|---|
| `FluentValidation.ValidationException` | 400 | Groups errors by `PropertyName` into `HttpValidationProblemDetails`; logs warning. |
| `BadHttpRequestException` / `JsonException` | 400 | Returns `ProblemDetails` with `ErrorCodes.BadRequest`. |
| `DbUpdateConcurrencyException` | 409 | Returns `ProblemDetails` with `ErrorCodes.Conflict`. |
| `DbUpdateException` | 409 if duplicate-key detected (checks `Duplicate entry`, `unique constraint`, `23505`); otherwise 500 | Maps inner DB error to either conflict or internal error. |
| `AppException` (and subclasses) | 400–503 based on subtype | Uses `ErrorCatalog.GetSafeMessage` for the detail; logs warning with `ErrorCode`. |
| Fallback | 500 | Returns `ProblemDetails` with `ErrorCodes.InternalServerError`. |

Every response sets `Content-Type: application/problem+json`, includes `extensions["code"]` and `extensions["traceId"]` (from `HttpContext.TraceIdentifier`), and uses `application/problem+json` per RFC 7807.

### MediatR pipeline behaviors

- `ValidationBehavior<TRequest,TResponse>` runs all registered `IValidator<TRequest>` validators before the handler. If validation fails, it tries to construct a `Result<T>.ValidationFailure(...)` when the response type is `Result<>`; otherwise it throws `FluentValidation.ValidationException`, which is caught by `GlobalExceptionHandler`.
- `LoggingBehavior<TRequest,TResponse>` wraps request processing with timing and logs both successful and failed outcomes (including `ErrorCode` and `Error` from `IResult` responses) and re-throws unexpected exceptions so the global handler can process them.

## Frontend (React + TypeScript)

`frontend/src/lib/errorService.ts` defines `ERROR_MESSAGES`, a dictionary mirroring every `ErrorCodes` constant with Vietnamese user-facing text. `extractErrorDetails(error)` normalizes Axios errors:

1. Reads `response.data.code`, `response.data.traceId`, and `response.status`.
2. Detects validation payloads shaped as `{ errors: { field: ["msg"] } }` and extracts the first non-technical message.
3. Looks up `ERROR_MESSAGES[code]` for a localized message.
4. Falls back to `data.title` if it is not technical (same regex as server-side `ErrorCatalog.SanitizeMessage`).
5. Special-cases status 429 → `RATE_LIMIT_EXCEEDED`, 502/503/504 → `SERVICE_UNAVAILABLE`.
6. Final fallback includes the `traceId` in the message for supportability.

`isTechnicalMessage` uses the same regex pattern as the server's `ErrorCatalog` to avoid exposing stack traces, SQL fragments, connection strings, etc.

## AI Service (FastAPI, Python)

`ai-service/app/main.py` takes a simpler approach:

- `RequestValidationError` is handled explicitly, returning `{ "code": "INVALID_CONTEXT" }` with status 422.
- A blanket `Exception` handler returns `{ "code": "AI_UNAVAILABLE" }` with status 503 for any unhandled exception.
- The `/v1/assist` endpoint catches `ValueError` from prompt building and returns `INVALID_CONTEXT` (422), and wraps the async assist call in `asyncio.timeout(DEADLINE_SECONDS)`; any exception inside returns `AI_UNAVAILABLE` (503).
- Authentication failures raise `HTTPException(401, detail={"code": "UNAUTHORIZED"})`.

There is no shared error catalog or structured exception hierarchy in the Python service — error codes are literal strings.

## Cross-cutting conventions observed

1. **Stable error codes**: Every user-visible failure is identified by a string constant defined in `ErrorCodes`; these constants are mirrored in the frontend `ERROR_MESSAGES` dictionary.
2. **Localized messages on the server**: `ErrorCatalog` holds Vietnamese titles and details; the frontend has its own parallel dictionary rather than rendering server-provided text directly.
3. **No raw stack traces to clients**: Both server (`ErrorCatalog.SanitizeMessage`) and client (`isTechnicalMessage`) apply the same regex to detect and redact technical content.
4. **Trace correlation**: Every error response includes a `traceId` extension (server: `HttpContext.TraceIdentifier`; client: forwarded from response header `x-correlation-id` or body `traceId`).
5. **RFC 7807 ProblemDetails**: The ASP.NET global handler always returns `application/problem+json` with `status`, `title`, `detail`, `type`, `instance`, plus custom `code` and `traceId` extensions.
6. **Two error channels in the backend**: Expected business failures use `Result<T>` (returned values); unexpected/unauthorized/domain violations throw typed exceptions caught centrally.
7. **Validation flows through FluentValidation**: Validators run in a MediatR pipeline behavior, producing either a `Result<T>.ValidationFailure` or a `ValidationException` mapped to 400 by the global handler.
8. **Frontend normalization layer**: All Axios calls go through `errorService.ts`, which maps server error codes to user-friendly messages and surfaces a `NormalizedApiError` shape with `code`, `traceId`, `status`, `message`.

## Constraints enforced by the codebase

- Unknown `ErrorCode` values fall back to 400 with a sanitized message (enforced by `ErrorCatalog.GetDetails`).
- Database duplicate-key errors are automatically classified as 409 Conflict regardless of the underlying provider (PostgreSQL `23505`, MySQL `Duplicate entry`, EF Core `unique constraint`) — enforced in `GlobalExceptionHandler`.
- Session-expiry codes are forced to 401 even if the originating code does not map to 401 in the catalog — enforced in `ResultExtensions.ToHttpResult`.
- Any exception not recognized by `GlobalExceptionHandler` becomes a 500 with `INTERNAL_SERVER_ERROR` — enforced as the final `else` branch.
- The AI service treats any unhandled exception as `AI_UNAVAILABLE` (503) — enforced by the top-level `@app.exception_handler(Exception)`.