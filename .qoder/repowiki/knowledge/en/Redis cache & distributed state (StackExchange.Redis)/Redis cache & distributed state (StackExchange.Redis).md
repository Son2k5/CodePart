---
kind: external_dependency
name: Redis cache & distributed state (StackExchange.Redis)
slug: stackexchange-redis
category: external_dependency
category_hints:
    - client_constraint
scope:
    - '**'
---

StackExchange.Redis is the Redis client used for OTP storage, refresh-token blacklist, login attempt tracking, and optional code-execution job queue coordination. It is wired via `IConnectionMultiplexer` and health-checked by `RedisDependencyHealthCheck`. No SignalR Redis backplane is present yet — that would require adding `Microsoft.AspNetCore.SignalR.StackExchangeRedis`.