# CLAUDE.md

MatricDasbhoard - .NET 10 API (Clean Architecture), fully dockerized.

**The backend API is the core of the project.** It is a public-facing API designed to serve any client - multiple frontends, mobile apps, other backends, third-party integrations. Treat every API change as if unknown consumers depend on it.

```
Backend layers: WebApi -> Application <- Infrastructure -> Domain + Shared
```

## Hard Rules

### Backend

- `Result`/`Result<T>` for all fallible operations - never throw for business logic
- `TimeProvider` (injected) - never `DateTime.UtcNow` or `DateTimeOffset.UtcNow`
- C# 13 `extension(T)` syntax for new extension methods
- Never `null!` - fix the design instead
- `ProblemDetails` (RFC 9457) for all error responses - never anonymous objects or raw strings
- `internal` on all Infrastructure service implementations
- `/// <summary>` XML docs on all public and internal API surface
- `System.Text.Json` only - never `Newtonsoft.Json`
- NuGet versions in `Directory.Packages.props` only - never in `.csproj`

### Cross-Cutting

- Security first - when convenience and security conflict, choose security. Deny by default, open selectively.
- Atomic commits: `type(scope): imperative description` (Conventional Commits). No `Co-Authored-By` lines.
- No dead code - remove unused imports, variables, functions, files, and stale references in the same commit
- No em dashes - never use `---` anywhere (code, comments, docs, UI). Use `-` or rewrite the sentence.

## Delegation Rule

The top-level agent is an orchestrator. It does not write application code in `src/` - that goes to specialized agents.

**Default (application code in `src/`):**

- Delegate implementation to `backend-engineer`
- Run relevant reviewers in parallel after implementation completes
- Run `filemap-checker` after modifying files with known consumers

**Orchestrator handles directly (no delegation needed):**

- Documentation, configuration, and tooling files (`.claude/`, `CLAUDE.md`, `FILEMAP.md`, `.gitignore`, `docs/`, CI/CD)
- Quick answers, planning, research, and code review
- Commits, PRs, and git operations

**User override:** If the user explicitly asks to skip delegation ("do it yourself", "directly", "don't delegate", "just fix it"), the orchestrator implements directly regardless of scope.

## Breaking Changes

The backend API is public-facing. Treat every contract change with the same care as a published library.

| Layer                         | Breaking change                                                              |
| ----------------------------- | ---------------------------------------------------------------------------- |
| **Domain entity**             | Renaming/removing a property, changing a type                                |
| **Application interface/DTO** | Changing a method signature, renaming/removing a field, changing nullability |
| **WebApi endpoint**           | Changing route, method, request/response shape, status codes                 |

**Pre-modification checklist:** (1) Check FILEMAP.md for impact, (2) Search all usages, (3) Document in commit body. Prefer additive changes. If breaking, update all consumers in the same PR.

## Verification

Run before every commit. Fix all errors before committing. **Loop until green - never commit with failures.**

```bash
# Backend (run when src/backend/ changed)
dotnet build src/backend/MatricDasbhoard.slnx && dotnet test src/backend/MatricDasbhoard.slnx -c Release

# Aspire (run to verify local orchestration - requires Docker)
dotnet run --project src/backend/MatricDasbhoard.AppHost
```

> **Note:** The setup scripts launch Aspire with `DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true` so the dashboard opens without a login token. When running `dotnet run` manually, you'll need the token from the console output (or set the same variable).

## Autonomous Behaviors

Do these automatically - never wait to be asked:

| Trigger                             | Action                                                                                                                                           |
| ----------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Any code change**                 | Run relevant verification. Fix failures. Loop until green.                                                                                       |
| **Modifying existing files**        | Check FILEMAP.md for downstream impact before editing. Update all affected files in the same commit.                                             |
| **Logically complete unit of work** | Commit immediately with Conventional Commit message. Don't batch, don't ask.                                                                     |
| **Creating a PR** (`/create-pr`)    | Auto-generate session doc in `docs/sessions/` for non-trivial PRs (3+ commits or 5+ files).                                                      |
| **Adding any feature**              | Write tests alongside the implementation - component, API integration, validator as applicable.                                                  |
| **Build/test failure**              | Read the error, fix it, re-run. Repeat until green. Don't stop and report the error unless stuck after 3 attempts.                               |
| **Unclear requirement**             | Infer from context and existing patterns first. Ask the user only when genuinely ambiguous (multiple valid approaches with different tradeoffs). |
| **Backend-only task**               | Delegate to `backend-engineer` (unless user overrides). Run `backend-reviewer` in parallel after.                                                |
| **After any implementation**        | Run relevant reviewers in parallel.                                                                                                              |
| **After modifying shared files**    | Run `filemap-checker` to verify all downstream consumers are updated.                                                                            |

## Agent Team

All application code in `src/` goes to specialized agents. User override is the only exception (see Delegation Rule). Run reviewers in parallel after every implementation.

| Agent              | Role                       | When to use                      |
| ------------------ | -------------------------- | -------------------------------- |
| `backend-engineer` | Implements .NET features   | Task stays within `src/backend/` |
| `backend-reviewer` | Audits C# code (read-only) | Reviewing backend changes        |


| `security-reviewer` | Audits for vulnerabilities (read-only) | Auth, permissions, PII, tokens, middleware changes |


| `devops-engineer` | Implements infra changes | Dockerfiles, Aspire, CI/CD, health checks, env vars |
| `devops-reviewer` | Audits infra/deployment (read-only) | Dockerfiles, compose, Aspire, CI/CD changes |
| `test-writer` | Writes tests | Tests needed alongside implementation |
| `filemap-checker` | Verifies downstream updates (read-only) | After modifying files with known consumers |
| `tech-writer` | Writes documentation | READMEs, session docs, guides |
| `product-owner` | Proposes prioritized work items (read-only) | Deciding what to work on next, backlog review |

**Delegation patterns:**

- **New backend feature**: `backend-engineer` implements, then `backend-reviewer` audits


- **Security-sensitive change**: `backend-engineer` implements, then `backend-reviewer` + `security-reviewer` audit in parallel


- **Infra task**: `devops-engineer` implements, then `devops-reviewer` audits
- **Pre-release check**: `devops-reviewer` validates deployment readiness
- **What to work on next**: `product-owner` analyzes codebase, issues, and TODOs
- **After modifying shared files**: `filemap-checker` verifies all consumers updated

## File Roles

| File              | Contains                                                                                   |
| ----------------- | ------------------------------------------------------------------------------------------ |
| `.claude/agents/` | Specialized agents for delegation (engineers, reviewers, writers)                          |
| `.claude/skills/` | Step-by-step procedures and convention references (type `/` to list user-invocable skills) |
| `.claude/hooks/`  | Lifecycle hooks: safety gates, auto-format, quality checks                                 |
| `FILEMAP.md`      | "When you change X, also update Y" - change impact tables                                  |
