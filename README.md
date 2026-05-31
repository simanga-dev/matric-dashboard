# MatricDasbhoard

Generated with [netrock](https://netrock.dev) - a .NET API project generator.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker](https://docs.docker.com/get-docker/) (required for Aspire orchestration)

## Quick start

Run the setup script to configure ports, initialize git, and verify your environment:

```bash
# macOS / Linux
chmod +x setup.sh && ./setup.sh

# Windows (PowerShell)
.\setup.ps1
```

The setup script will:
- Check prerequisites (.NET SDK, Docker)
- Let you choose a base port for the service stack
- Optionally initialize a git repository with an initial commit
- Build the solution and run tests

### Manual start

If you prefer to skip the setup script:

```bash
dotnet run --project src/backend/MatricDasbhoard.AppHost
```

The Aspire dashboard opens automatically. From there you can access the API, pgAdmin, Mailpit, and MinIO console.

## Run tests

```bash
dotnet test src/backend/MatricDasbhoard.slnx
```

## What's included

- **Authentication** - JWT + refresh tokens, registration, login, email verification, password reset
- **Two-factor auth** - TOTP-based 2FA with recovery codes
- **OAuth** - External login with Google, GitHub, Microsoft, and more
- **Captcha** - Cloudflare Turnstile on registration and password reset
- **Background jobs** - Hangfire scheduling with PostgreSQL storage
- **File storage** - S3/MinIO abstraction
- **Avatars** - User avatar uploads with image processing
- **Audit trail** - Security event logging
- **Admin panel** - User and role management
- **Aspire** - .NET Aspire for local dev orchestration with OpenTelemetry

## Project structure

```
src/backend/
  MatricDasbhoard.Domain/           Domain entities
  MatricDasbhoard.Shared/           Cross-cutting: Result, errors, helpers
  MatricDasbhoard.Application/      Use cases, interfaces, DTOs
  MatricDasbhoard.Infrastructure/   EF Core, services, external integrations
  MatricDasbhoard.WebApi/           Controllers, middleware, configuration
  MatricDasbhoard.ServiceDefaults/  OpenTelemetry, health checks, resilience
  MatricDasbhoard.AppHost/          Aspire orchestration (local dev)
  tests/                      Architecture, unit, and integration tests
```

## Configuration

Key settings are in `src/backend/MatricDasbhoard.WebApi/appsettings.json`. Development overrides are in `appsettings.Development.json`.

### Port allocation

Ports are configured in `src/backend/MatricDasbhoard.AppHost/appsettings.json`. The setup script can change these for you. All infrastructure ports (pgAdmin, PostgreSQL, MinIO, Mailpit) are derived from the base port automatically.

### Seed users

Development seed users are configured in `appsettings.Development.json`:

```json
{
  "Seed": {
    "Users": [
      { "Email": "admin@example.com", "Password": "YourPassword123!", "Role": "Superuser" }
    ]
  }
}
```

## Adding migrations

The project ships without EF Core migrations. On first run in development, the database schema is created automatically. When you're ready to manage schema changes:

```bash
cd src/backend
dotnet ef migrations add Initial --project MatricDasbhoard.Infrastructure --startup-project MatricDasbhoard.WebApi
```

## Learn more

- [netrock](https://github.com/fpindej/netrock) - The original template this was generated from
- [netrock-cli](https://github.com/fpindej/netrock-cli) - The generator source code
- [Discord](https://discord.gg/5rHquRptSh) - Community and support
