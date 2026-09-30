# CodePath tests

## Fast tests

Architecture and domain tests do not require external services:

```powershell
dotnet test tests/CodePath.Architecture.Tests/CodePath.Architecture.Tests.csproj
```

## Integration tests

Integration tests require Docker Desktop. Testcontainers starts isolated PostgreSQL
and Redis containers, applies the real migrations, runs the API through
`WebApplicationFactory`, and removes the containers after the test run:

```powershell
dotnet test tests/CodePath.Integration.Tests/CodePath.Integration.Tests.csproj
```

Tests tagged `SecurityRegression` document required behavior for later security
phases. A skipped test must only be enabled when the corresponding production fix
is implemented; it must not be deleted to make the suite green.
