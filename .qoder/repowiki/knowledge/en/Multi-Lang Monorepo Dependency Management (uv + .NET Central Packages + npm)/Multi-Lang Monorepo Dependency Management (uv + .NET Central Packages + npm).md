---
kind: dependency_management
name: Multi-Lang Monorepo Dependency Management (uv + .NET Central Packages + npm)
category: dependency_management
scope:
    - '**'
source_files:
    - ai-service/pyproject.toml
    - ai-service/uv.lock
    - api/Directory.Packages.props
    - api/global.json
    - frontend/package.json
    - frontend/package-lock.json
    - package.json
    - package-lock.json
---

## Overview

This monorepo contains three independently managed language stacks, each with its own package manager and lockfile. There is no cross-language dependency resolution — the top-level `package.json` only declares a small set of shared frontend utilities used by the root workspace.

## Python (ai-service)

- **Manager:** [uv](https://github.com/astral-sh/uv) (detected from `ai-service/uv.lock`).
- **Manifest:** `ai-service/pyproject.toml` declares runtime dependencies under `[project]`, plus optional groups `test`, `training`, and `transformer` under `[project.optional-dependencies]`.
- **Lockfile:** `ai-service/uv.lock` pins every transitive dependency to an exact version and SHA256 hash, sourced from `https://pypi.org/simple`. It records `requires-python = ">=3.11"` and includes resolution markers for `python_full_version >= '3.12'` vs `< '3.12'`, so different Python minor versions can resolve different transitive wheels (e.g., `numpy` 2.4.6 vs 2.5.4).
- **Versioning style:** Broad semver ranges with upper bounds (`>=0.115,<1` for FastAPI, `>=2.9,<3` for Pydantic, etc.), allowing patch/minor updates while blocking major bumps.
- **Private registry / vendoring:** No custom index or private registry is configured; all packages resolve from the public PyPI. No vendored third-party code exists in `ai-service/`.

## .NET / C# (api)

- **SDK pinning:** `api/global.json` pins the SDK to `8.0.100` with `rollForward: latestMinor`, so CI and local builds use exactly that SDK release line.
- **Central package management:** `api/Directory.Packages.props` sets `ManagePackageVersionsCentrally=true` and `CentralPackageTransitivePinningEnabled=true`. All NuGet packages are declared as `<PackageVersion Include="..." Version="..." />` entries in this single file rather than inside individual `.csproj` files.
- **Shared packages:** The central file documents cross-cutting abstractions reused across projects with a Vietnamese comment: `<!-- Cross-cutting abstractions dùng ở nhiều project (Shared.Kernel, các Application/Infrastructure layer) -->`. Shared packages include EF Core 8.x, Pomelo.MySql, MediatR, FluentValidation, JWT bearer, MailKit, StackExchange.Redis, xUnit, Testcontainers, Polly, etc.
- **Lockfile:** No `.csproj` files contain inline `<PackageReference>` versions; versions flow exclusively through `Directory.Packages.props`. No `packages.lock.json` was found at the repo root or under `api/`, so deterministic restore relies on the central props file alone.
- **Private registry / vendoring:** No `<PackageSource>` or private NuGet feed is configured in any visible MSBuild file. No vendored NuGet packages exist.

## Frontend (frontend/)

- **Manager:** npm (detected from `frontend/package-lock.json` and `frontend/package.json`).
- **Manifest:** `frontend/package.json` separates `dependencies` (runtime: React 19, Vite, Tailwind, Monaco, TanStack Query, Zod, Zustand, etc.) from `devDependencies` (Playwright, Vitest, MSW, oxlint, TypeScript ~6.0.2).
- **Lockfile:** `frontend/package-lock.json` (lockfileVersion 3) pins every transitive dependency to an exact version and integrity hash from `https://registry.npmjs.org`.
- **Top-level `package.json`:** The repo root also has a minimal `package.json` declaring only four shared dependencies (`@monaco-editor/react`, `monaco-editor`, `react-markdown`, `rehype-sanitize`) with a corresponding `package-lock.json`; this appears to be a legacy/shared workspace manifest separate from `frontend/package.json`.
- **Versioning style:** Mostly caret ranges (`^x.y.z`) allowing compatible updates; TypeScript uses a tilde range (`~6.0.2`) for tighter pinning.
- **Private registry / vendoring:** No `.npmrc` or custom registry is present. No `vendor/` directory; `node_modules` is gitignored.

## Conventions Observed

1. **Per-language toolchain:** Each subproject owns its own manifest and lockfile; there is no unified dependency graph across Python/.NET/JS.
2. **Broad ranges with upper bounds (Python):** Dependencies in `pyproject.toml` consistently use `>=X.Y,<Z` ranges to allow safe upgrades while preventing breaking major-version changes.
3. **Centralized .NET versions:** All NuGet package versions live in `api/Directory.Packages.props`; individual `.csproj` files do not pin versions inline.
4. **SDK pinning (.NET):** `api/global.json` locks the .NET SDK to `8.0.100` with `latestMinor` roll-forward.
5. **Deterministic installs via lockfiles:** Both `ai-service/uv.lock` and `frontend/package-lock.json` commit exact resolved versions and hashes to source control.
6. **Public registries only:** No private PyPI index, private NuGet feed, or npm registry override is configured anywhere in the repository.
7. **No vendoring:** Third-party code is never committed into the tree; only generated build artifacts (`bin/`, `obj/`, `dist/`, `node_modules/`, `.venv/`) are ignored.