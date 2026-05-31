# Conventions Quick Reference (for PR Review)

## Backend Hard Rules

- `Result`/`Result<T>` for all fallible operations - never throw for business logic
- `TimeProvider` injected - never `DateTime.UtcNow`
- C# 13 `extension(T)` syntax for new extension methods
- Never `null!` - fix the design instead
- `ProblemDetails` (RFC 9457) for all error responses
- `internal` on all Infrastructure service implementations
- `/// <summary>` XML docs on all public and internal API surface
- `System.Text.Json` only - never Newtonsoft
- NuGet versions in `Directory.Packages.props` only

## Cross-Cutting Rules

- No dead code
- No em dashes (U+2014) anywhere
- No emojis anywhere
- Atomic commits: `type(scope): imperative description`
- Security first - deny by default
- PII compliance: `users.view_pii` permission, server-side masking

## DTO Naming

| Layer | Pattern |
|---|---|
| WebApi Request | `{Operation}Request` |
| WebApi Response | `{Entity}Response` |
| Application Input | `{Operation}Input` |
| Application Output | `{Entity}Output` |

## Error Flow

Backend `ErrorMessages.*` -> `Result.Failure()` -> `ProblemFactory.Create()` -> `ProblemDetails.detail`

## Dockerfile

When a new `.csproj` project is added that WebApi references, it needs a COPY line in the Dockerfile restore layer.
