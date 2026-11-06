---
kind: build_system
name: Multi-Service Containerized Build & Docker Compose Orchestration
category: build_system
scope:
    - '**'
source_files:
    - api/Dockerfile
    - api/global.json
    - api/Directory.Build.props
    - api/CodePath.sln
    - api/docker-compose.yml
    - api/docker-compose.production.yml
    - ai-service/Dockerfile
    - ai-service/pyproject.toml
    - ai-service/uv.lock
    - frontend/Dockerfile
    - frontend/package.json
    - frontend/package-lock.json
---

## What system/approach is used

The CodePath monorepo builds and ships three independent services, each with its own language toolchain and container image:

- **API (ASP.NET Core)** — built with `dotnet` SDK 8.0 (`global.json` pins `8.0.100`, roll-forward to latest minor). The solution `api/CodePath.sln` contains six projects: `CodePath.Api`, `CodePath.Application`, `CodePath.Domain`, `CodePath.Infrastructure`, `CodePath.Shared.Kernel`, `CodePath.Shared.Web`, plus three test projects under `tests/`. A single multi-stage `api/Dockerfile` restores NuGet packages (with `/root/.nuget/packages` cache mount), publishes `CodePath.Api.csproj` in Release with `--no-restore --output /app/publish /p:UseAppHost=false`, and runs the resulting `CodePath.Api.dll` as a non-root `app` user on port 8080.
- **AI Service (FastAPI + Python)** — built with `python:3.11-slim`, dependency-managed via `pyproject.toml` + `uv.lock`; the Dockerfile installs `uv==0.13.1` pinned, runs `uv sync --locked --no-dev --no-cache`, creates a dedicated `assistant` user (uid 10001), and starts `uvicorn app.main:app` with `--limit-concurrency 32` on port 8000.
- **Frontend (React + Vite)** — built with `node:24-alpine`, dependencies installed via `npm ci` (cache mounted at `/root/.npm`), then `npm run build`; the runtime image is `nginx:1.28-alpine` serving the static `dist/` output from `nginx/default.conf` on ports 80/443.

Orchestration is done entirely through two Docker Compose files under `api/`:

- `docker-compose.yml` — development stack. Comment says "Development only. .NET, Vite and MySQL run on the host." Redis is the only service spun up by compose; AI/RAG are optional profiles (`ai`, `rag`).
- `docker-compose.production.yml` — standalone production stack. Comment explicitly states "Do NOT combine with docker-compose.yml" and "Production uses docker-compose.production.yml ALONE, never as an overlay." It brings up redis, api, web (frontend), ai-service, ai-indexer, and qdrant behind a private `backend` network (subnet `172.30.0.0/24`) and exposes only `web` on ports 80/443.

No Makefiles, no top-level CI pipeline, no GitHub Actions workflows were found in the repository (the `.github` directory does not exist).

## Key files and packages

- `api/Dockerfile` — multi-stage .NET 8 build and runtime image
- `api/global.json` — pins dotnet SDK 8.0.100
- `api/Directory.Build.props` — shared MSBuild props: `<TargetFramework>net8.0</TargetFramework>`, `<Nullable>enable</Nullable>`, `<WarningsAsErrors>Nullable</WarningsAsErrors>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<LangVersion>latest</LangVersion>`
- `api/Directory.Packages.props` — central package versions for NuGet
- `api/CodePath.sln` — solution manifest for all API projects and tests
- `api/docker-compose.yml` — development compose (Redis + optional AI/RAG profiles)
- `api/docker-compose.production.yml` — production compose (all services, secrets, networks)
- `ai-service/Dockerfile` — Python 3.11 slim image with uv lockfile install
- `ai-service/pyproject.toml` + `ai-service/uv.lock` — Python dependency manifests
- `frontend/Dockerfile` — Node 24 Alpine build → Nginx 1.28 runtime
- `frontend/package.json` + `frontend/package-lock.json` — frontend dependencies

## Architecture and conventions

### Multi-stage containers per service
Each service has its own `Dockerfile` that separates build and runtime stages. The .NET image copies only the six project `.csproj` files before `dotnet restore` so NuGet cache layers stay stable; source is copied afterward and `dotnet publish` is invoked with `--no-restore`. The Python image uses `uv sync --locked` against `uv.lock` rather than `pip freeze`. The frontend image uses `npm ci` (not `npm install`) for deterministic installs.

### Non-root runtime users
Both server images drop privileges: .NET runs as user `app`, Python as user `assistant` (uid 10001). The Python service also sets `read_only: true` with a `tmpfs:/tmp` mount and drops all capabilities (`cap_drop: ALL`, `security_opt: no-new-privileges:true`).

### Profiled optional services
AI and RAG features are opt-in via Docker Compose profiles. In both compose files, `ai-service` is profile `[ai, rag]` and `ai-indexer` + `qdrant` are profile `[rag]`. Running without those profiles yields a minimal Redis-only stack.

### Secrets management
Production compose uses Docker secrets for database passwords, Redis password, JWT signing key, SMTP password, admin passwords, and TLS certificates. Environment variables are required via `${VAR:?message}` syntax for critical settings (`SERVER_NAME`, `DATABASE_HOST`, `DATABASE_USER`, `SMTP_HOST`, `SMTP_USERNAME`, `SMTP_FROM_ADDRESS`, `JUDGE0_BASE_URL`, `QDRANT_API_KEY`, `EMBEDDING_BASE_URL`, `EMBEDDING_MODEL`, `LLM_MODEL`, `LLM_ADVANCED_MODEL`, `SERVICE_API_KEY`).

### Network isolation
Production compose defines a `backend` bridge network with a fixed subnet `172.30.0.0/24`. Only the `web` service publishes ports 80/443; Redis, API, AI, indexer, and Qdrant remain internal. Development compose binds Redis/AI/Qdrant to `127.0.0.1:<port>` for local access.

### Health checks
Every service declares a healthcheck: Redis via `redis-cli ping`, AI via HTTP GET to `/health`, Qdrant via its entrypoint. Services declare `depends_on` with `condition: service_healthy` where applicable.

### Version pinning strategy
- .NET SDK pinned in `global.json` (`8.0.100`, `rollForward: latestMinor`)
- Python `uv` version pinned in the Dockerfile (`uv==0.13.1`)
- Python deps locked via `uv.lock`
- Frontend deps locked via `package-lock.json` and installed with `npm ci`
- Base images pinned by tag: `mcr.microsoft.com/dotnet/sdk:8.0`, `python:3.11-slim`, `node:24-alpine`, `nginx:1.28-alpine`, `redis:7-alpine`, `qdrant/qdrant:v1.17.1`

## Conventions and constraints

- **Rule:** Production deployment must use `docker-compose.production.yml` alone, never layered over `docker-compose.yml`. Enforced by the comment header in `api/docker-compose.production.yml`: "Standalone production stack. Do NOT combine with docker-compose.yml."
- **Rule:** All .NET projects target `net8.0` with nullable reference types enabled and treated as errors, enforced centrally by `api/Directory.Build.props` (`<TargetFramework>net8.0</TargetFramework>`, `<Nullable>enable</Nullable>`, `<WarningsAsErrors>Nullable</WarningsAsErrors>`).
- **Rule:** The .NET SDK version is pinned to `8.0.100` with `rollForward: latestMinor`, enforced by `api/global.json`.
- **Rule:** Python dependencies must be installed with `uv sync --locked` against `uv.lock`; the Dockerfile hard-codes `uv==0.13.1` and passes `--locked`.
- **Rule:** Frontend dependencies must be installed deterministically via `npm ci` (not `npm install`), enforced by the frontend Dockerfile.
- **Rule:** Runtime containers run as non-root users (.NET `app`, Python `assistant` uid 10001).
- **Rule:** AI/RAG services are optional and gated behind Docker Compose profiles `ai` and `rag`; the production compose validates required env vars at startup via shell `:` expansion (`: "$${VAR:?Set VAR}"`).
- **Rule:** Secrets are passed through Docker secrets files, not inline environment variables, in the production compose definition.
- **Constraint:** No top-level CI/CD pipeline or Makefile exists in the repository; builds are driven by `dotnet publish`, `uv sync`, and `npm run build` inside their respective Dockerfiles.