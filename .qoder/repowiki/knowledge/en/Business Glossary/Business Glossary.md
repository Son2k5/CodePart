---
kind: business_term
name: Business Glossary
category: business_term
scope:
    - '**'
---

### OutboxMessages
- Definition：Durable outbox table used to guarantee at-least-once delivery of cross-module events. Rows are written in the same transaction as the business operation and later drained by a background worker (RagIndexWorker/LearningJudgeRelay pattern), replacing direct cross-module calls with an async, retry-safe pipeline.
- Aliases：outbox table、outbox rows

### Result pattern
- Definition：Domain error-handling convention where operations return `Result<T>.Success/Failure/ValidationFailure` with typed `ErrorCodes`/`ErrorCatalog`, converted to HTTP responses via `.ToHttpResult()` in API endpoints. All new services must follow this instead of throwing exceptions for expected failures.
- Aliases：Result<T>、Result.Success、Result.Failure

### ActiveUser
- Definition：Authorization policy name applied to API groups requiring a user whose account is active (as opposed to Admin-only or unauthenticated routes). New endpoints should attach it via `RequireAuthorization("ActiveUser")`.
- Aliases：policy ActiveUser

### AdminOnly
- Definition：Authorization policy name restricting endpoints to admin users only, used on admin-scoped Minimal API groups.
- Aliases：policy AdminOnly

### Cursor-based pagination
- Definition：Pagination style used by the API (e.g. `AdminCursor`) where results are returned with a cursor token rather than page numbers, enabling stable large-list traversal without offset drift.
- Aliases：cursor pagination、cursor-based paging

### CorrelationIdMiddleware
- Definition：Request-scoped correlation ID middleware already present in the project, used to propagate a trace identifier across logs and downstream calls. New services should read/write it rather than generating ad-hoc request IDs.
- Aliases：correlation id、correlation ID
