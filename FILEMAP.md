# File Map - Change Impact Reference

Quick-reference for "when you change X, also update Y" and "where does X live?"

> **Rule:** Before modifying any existing file listed here, trace its impact row. If a change affects downstream files, update them in the same commit (or same PR at minimum).

---

## Top 5 Most Common Changes

| Change | Must also update |
|---|---|
| **Add/change domain entity property** | EF config - migration - Application DTOs - WebApi DTOs - mapper |
| **Add backend endpoint** | Controller + DTOs + validator + mapper |
| **Change WebApi response DTO** | Mapper, `Api.Tests/Contracts/ResponseContracts.cs` |
| **Add permission** | `AppPermissions.cs` - `[RequirePermission]` - seed in `ApplicationBuilderExtensions` |
| **Add NuGet package** | `Directory.Packages.props` (version) + `.csproj` (reference) |

---

## Change Impact Tables

### Backend Changes

| When you change... | Also update... |
|---|---|
| **Domain entity** (add/rename property) | EF configuration, migration, Application DTOs, WebApi DTOs, mapper |
| **Domain entity** (add enum property) | EF config (`.HasComment()`), `EnumSchemaTransformer` handles the rest automatically |
| **`ErrorMessages.cs`** (Shared - add/rename constant) | Service that uses it |
| **`Result.cs`** (Shared - change pattern) | Every service + every controller that matches on `Result` |
| **Application interface** (change signature) | Infrastructure service implementation, controller calling the service |
| **Application DTO** (add/rename/remove field) | Infrastructure service, WebApi mapper, WebApi request/response DTO |
| **Infrastructure EF config** (change mapping) | Run new migration |
| **`MatricDasbhoardDbContext`** (add DbSet) | Run new migration |
| **Infrastructure service** (change behavior) | Verify controller still maps correctly, verify error messages still apply |
| **Infrastructure Options class** | `appsettings.json`, `appsettings.Development.json` (excluded from production publish - see `StripDevConfig`), DI registration |
| **DI extension** (new service registration) | `Program.cs` must call the extension |
| **WebApi controller** (change route/method) | API consumers |
| **WebApi request DTO** (add/rename/remove property) | Validator, mapper |
| **WebApi response DTO** (add/rename/remove property) | Mapper, `Api.Tests/Contracts/ResponseContracts.cs` |
| **WebApi validator** (change rules) | Consider matching client validation UX |
| **`Program.cs`** (change middleware order) | Test full request pipeline - order matters for auth, CORS, rate limiting; update `CustomWebApplicationFactory` if new services need mocking |
| **`Directory.Packages.props`** (change version) | `dotnet build` to verify compatibility |
| **`Directory.Build.props`** (change TFM/settings) | All projects in solution |
| **`BaseEntity.cs`** | `BaseEntityConfiguration`, `AuditingInterceptor`, all entities |
| **`BaseEntityConfiguration.cs`** | All entity configurations that extend it |
| **`CustomWebApplicationFactory.cs`** (change mock setup) | All API integration tests that depend on factory mocks |
| **`appsettings.Testing.json`** (change test config) | `CustomWebApplicationFactory` behavior; all API integration tests |
| **`FileStorageOptions`** (change S3/MinIO config) | `appsettings.json`, `MatricDasbhoard.AppHost/Program.cs` (`.WithEnvironment()`), `appsettings.Testing.json` |
| **`EmailOptions`** (change config shape) | `appsettings.json`, `appsettings.Development.json`, `appsettings.Testing.json`, `ServiceCollectionExtensions` (email DI), `EmailOptionsValidationTests` |
| **`IEmailService`** (change sending contract) | `NoOpEmailService`, `SmtpEmailService`, `CustomWebApplicationFactory` |
| **`IEmailTemplateRenderer`** (change rendering contract) | `FluidEmailTemplateRenderer`, `TemplatedEmailSender`, `FluidEmailTemplateRendererTests` |
| **`ITemplatedEmailSender`** (change send-safe contract) | `TemplatedEmailSender`, all services calling `SendSafeAsync()`, `TemplatedEmailSenderTests` |
| **`EmailTemplateModels.cs`** (add/rename model record) | Matching `.liquid` templates, `FluidEmailTemplateRenderer.CreateOptions()`, services that construct the model, `FluidEmailTemplateRendererTests` |
| **`.liquid` email template** (change variable/layout) | Matching model record in `EmailTemplateModels.cs`, `_base.liquid` if layout change, `FluidEmailTemplateRendererTests` |
| **`_base.liquid`** (shared email layout) | All rendered HTML emails, `FluidEmailTemplateRendererTests` layout assertions |
| **`FluidEmailTemplateRenderer`** (change rendering/caching logic) | `FluidEmailTemplateRendererTests`, `TemplatedEmailSender` |
| **`TemplatedEmailSender`** (change render+send wrapping) | `TemplatedEmailSenderTests`, services calling it |
| **`IFileStorageService`** (change upload/download contract) | `S3FileStorageService`, any service using file storage |
| **`IImageProcessingService`** (change avatar processing) | `ImageProcessingService`, `UserService.UploadAvatarAsync` |
| **`ApplicationUser.HasAvatar`** (change avatar flag) | `UserOutput`, `AdminUserOutput`, `UserResponse`, `AdminUserResponse`, `UserMapper`, `AdminMapper` |
| **Avatar endpoints** (`PUT/DELETE/GET`) | `UploadAvatarRequest`, `UploadAvatarRequestValidator`, `UserMapper` |
| **`AuditActions.cs`** (add action constant) | Service that logs it |
| **`AuditEvent` entity** (change fields) | `AuditEventConfiguration`, `AuditService`, Application DTOs (`AuditEventOutput`), WebApi DTOs, `AuditMapper` |
| **`HybridCache`** (change caching usage) | `NoOpHybridCache`, `UserCacheInvalidationInterceptor`, all services using `HybridCache`, `CustomWebApplicationFactory` mock |
| **`CacheKeys.cs`** (Application - rename/remove key) | All services referencing the changed key, `UserCacheInvalidationInterceptor` |
| **`CachingOptions`** (Infrastructure - change config shape) | `appsettings.json`, `appsettings.Development.json` |
| **`ICookieService`** (Application - change cookie contract) | `CookieService`, `AuthenticationService`, `UserService` |
| **`CookieNames`** (Application - rename/remove cookie name) | `AuthController`, `AuthenticationService`, `UserService` |
| **`IUserService`** (Application/Identity - change user service contract) | `UserService`, `UsersController`, `CustomWebApplicationFactory` mock |
| **`IUserContext`** (Application/Identity - change context contract) | `UserContext`, `AuthenticationService`, `UserService`, `AuditingInterceptor`, `UsersController`, `AdminController` |
| **`EmailTemplateNames.cs`** (Application - add/rename template name) | Services constructing `SendSafeAsync()` calls, matching `.liquid` template files |
| **Test fixture** (change shared helper) | All tests using that fixture |
| **`AppRoles.cs`** (add role) | Role seeding picks up automatically; consider what permissions to seed for the new role |
| **`AppPermissions.cs`** (add permission) | Seed in `ApplicationBuilderExtensions.SeedRolePermissionsAsync()`, add `[RequirePermission]` to endpoints |
| **`PiiMasker.cs`** (change masking rules) | `AdminMapper.WithMaskedPii` extensions, `PiiMaskerTests`, `AdminMapperPiiTests` |
| **`RequirePermission` attribute** (add to endpoint) | Remove any class-level `[Authorize(Roles)]`; ensure permission is defined in `AppPermissions.cs` |
| **`RoleManagementService`** (change role behavior) | Verify system role protection rules, check security stamp rotation |
| **`IRecurringJobDefinition`** (add new job) | Register in `ServiceCollectionExtensions.AddJobScheduling()`, job auto-discovered at startup |
| **Job scheduling config** (`ServiceCollectionExtensions.AddJobScheduling`) | `Program.cs` must call `AddJobScheduling()` and `UseJobScheduling()` |
| **`RateLimitPolicies.cs`** (add/rename constant) | `RateLimiterExtensions.cs` policy registration, `RateLimitingOptions.cs` config class, `appsettings.json` section, `[EnableRateLimiting]` attribute on controllers |
| **`RateLimitingOptions.cs`** (add/rename option class) | `RateLimiterExtensions.cs`, `appsettings.json`, `appsettings.Development.json` |
| **`RateLimiterExtensions.cs`** (add policy) | Requires matching constant in `RateLimitPolicies.cs` and config in `RateLimitingOptions.cs` |
| **`HostingOptions.cs`** (change hosting config shape) | `HostingExtensions.cs`, `appsettings.json`, `appsettings.Development.json` |
| **`HostingExtensions.cs`** (change middleware behavior) | `Program.cs` |
| **`Dockerfile`** (backend - change build/publish steps) | `.dockerignore`, verify published files don't include dev/test config |
| **`MatricDasbhoard.WebApi.csproj`** (add appsettings file) | If non-production: add `CopyToPublishDirectory="Never"` and matching `rm -f` in `Dockerfile` |
| **Route constraint** (add/modify in `Routing/`) | `Program.cs` constraint registration, route templates using that constraint |
| **`HealthCheckExtensions.cs`** (change endpoints/checks) | Frontend health proxy `+server.ts`, Dockerfile healthcheck command |
| **New infrastructure dependency** (DB, cache, storage, etc.) | `MatricDasbhoard.AppHost/Program.cs` (add resource + `.WithReference()`/`.WithEnvironment()`) |
| **Connection string config** (change format/name) | Verify `MatricDasbhoard.AppHost/Program.cs` environment variable mapping still works |
| **`MatricDasbhoard.ServiceDefaults/Extensions.cs`** | All projects referencing ServiceDefaults, `Program.cs` `AddServiceDefaults()` call |
| **`MatricDasbhoard.AppHost/Program.cs`** | Verify resource names match `ConnectionStrings:*` and `WithEnvironment` keys match `appsettings.json` option paths |
| **`ProblemDetailsAuthorizationHandler`** | `ProblemDetails` shape, `ErrorMessages.Auth` constants, `Program.cs` registration |
| **OpenAPI transformers** | Regenerate types to verify; check Scalar UI |
| **`CaptchaOptions`** (Infrastructure - Captcha config) | `appsettings.json`, `appsettings.Development.json`, `appsettings.Testing.json`, `TurnstileCaptchaService`, `ServiceCollectionExtensions` |
| **`TurnstileCaptchaService`** (Infrastructure - Captcha service) | `ICaptchaService` interface, `CaptchaOptions`, `AuthController` captcha gate |

### GeoServer / Spatial Changes

| When you change... | Also update... |
|---|---|
| **`docker-compose.yml`** postgres image/volume | `.env.example`, `docs/spatial-operations.md`; preserve Compose PG17 layout by default, PG18 needs explicit image/volume/mount overrides. Inspect Aspire's separate persisted volume before changing its major version |
| **`scripts/postgis/init.sql`** (schema/columns/layer name) | `scripts/geoserver/bootstrap.sh` layer name, frontend WFS `typeName` in `SchoolGeoMap.svelte` |
| **`scripts/geoserver/bootstrap.sh`** (workspace/store/layer names) | `SchoolGeoMap.svelte` WFS URL, `.env` / `.env.example` GeoServer vars |
| **GeoServer host port binding** | Bootstrap local maintenance URL, `.env.example`, `docs/spatial-operations.md`; Compose API keeps internal service DNS |
| **Sample location seeds** (`scripts/postgis/init.sql`, `scripts/geoserver/seed_from_api.py`) | Guard real rows with `is_sample`, bootstrap bounds refresh after seeding, `scripts/tests/test_spatial_operations.py`, `docs/spatial-operations.md` |
| **`GeoServerOptions`** (config shape) | `appsettings.json`, `appsettings.Development.json`, `AppHost/Program.cs` env, `docker-compose.yml` api env |
| **`GeoServerProxyMiddleware`** (route/behavior) | `GeoServerOptions.RoutePrefix`, frontend `api/geo/[...path]` proxy route |
| **`api/geo/[...path]`** (frontend proxy route) | Backend `GeoServerOptions.RoutePrefix`, `SchoolGeoMap.svelte` WFS URL |
| **`scripts/import_school_locations.py`** (coordinate source/normalization) | `scripts/import_school_locations.sh`, `scripts/geoserver/bootstrap.sh` (optional step), `justfile` `geo-locations`, `geo.school_locations` columns, `scripts/tests/test_spatial_operations.py`, `docs/spatial-operations.md`; masterlist headers `GIS_Long`/`GIS_Lat` vs `GIS_Longitude`/`GIS_Latitude` |
| **`scripts/import_school_locations.sh`** (DBE env vars/psql call) | `scripts/import_school_locations.py`, `docs/spatial-operations.md` |
| **DBE masterlist columns** (`NatEmis`, `Official_Institution_Name`, `GIS_*`) | `scripts/import_school_locations.py` header lookup, `scripts/tests/test_spatial_operations.py` workbook fixture |

### School Data (Postgres `marks` schema)

School lists and lookups are read from PostgreSQL via EF (`DashboardService`). The CSV is loaded with the import script.

| When you change... | Also update... |
|---|---|
| **`2024_nsc_school_performance_report.csv`** (columns) | `scripts/import_schools_to_db.py` (column names), `scripts/import_school_locations.py` (roster columns), `School`/`SchoolPerformance` entities, `DashboardService` projection |
| **`School`/`SchoolPerformance` entity** (fields) | EF configuration, migration, `scripts/import_schools_to_db.py`, `DashboardService` mapping, `SchoolOutput` / `SchoolDetailOutput` DTOs |
| **`DashboardService.GetSchoolsAsync`** | `IDashboardService`, `DashboardController`, `SchoolOutput` DTO, `DashboardMapper` |
| **`DashboardService.GetSchoolByIdAsync`** | `IDashboardService`, `DashboardController`, `SchoolDetailOutput` DTO, `DashboardMapper`, `MatricDasbhoard.Api.Tests/Features/Dashboard/DashboardMapperTests.cs` |
| **`DashboardService.GetSchoolSubjectsAsync`** (per-school subjects) | `IDashboardService`, `DashboardController` (`GET schools/{id}/subjects?year=`), `SchoolSubjectsOutput` / `SchoolSubjectOutput` / `OfferedSubjectOutput` DTOs, `DashboardMapper`, frontend `api/dashboard.ts` (`fetchSchoolSubjects`), `routes/(app)/subjects/+page.svelte`, `SchoolDetailPanel.svelte` |
| **`Subject` / `SchoolSubjectPerformance` entities** (fields) | EF configurations (`SubjectConfiguration`, `SchoolSubjectPerformanceConfiguration`), migration `AddSchoolSubjects`, `scripts/import_subject_results.py` column list, `DashboardService` subject projections, `ResponseContracts.cs` |
| **`scripts/import_subject_results.py`** (SQL shape/school matching) | `scripts/import_subject_results.sh`, `marks."SchoolSubjectPerformances"` columns + unique (SchoolId, SubjectId, Year), `NSC_DB_PATH`, `scripts/import_subject_results.py` centre->EMIS fallback |
| **`DashboardService.GetRegionStatsAsync`** | `IDashboardService`, `DashboardController`, `RegionStatsOutput` / `RegionYearOutput` DTOs, `DashboardMapper`, frontend `api/dashboard.ts` (`fetchRegionStats`), `SchoolDetailPanel.svelte` |
| **`RegionStatsOutput` / `RegionStatsResponse`** (fields) | `DashboardMapper`, `MatricDasbhoard.Api.Tests/Contracts/ResponseContracts.cs`, frontend `types/dashboard.ts` (`RegionStats` / `RegionYear`) |
| **`scripts/import_schools_to_db.py`** (import shape/identity validation) | `scripts/import_schools_to_db.sh`, entity columns, `scripts/tests/test_spatial_operations.py`, `docs/spatial-operations.md`; quarantine placeholders without synthesizing EMIS identifiers |

### NSC Results (SQLite `data/nsc/matric.db`)

Single SQLite file: per-school overall results (`schools`, `results`) and per-school subject results (`subjects`, `subject_aliases`, `report_sources`, `subject_results`). Each extractor refreshes only its own tables. `.env` `DATABASE_URL` / `DB_UI_NSC` point here for vim-dadbod.

| When you change... | Also update... |
|---|---|
| **`scripts/extract_nsc_results.py`** (overall-report layout/columns) | `schools`/`results` schema, `scripts/import_nsc_history.py` (`DEFAULT_DB`, `NSC_DB_PATH`), `scripts/import_nsc_history.sh` |
| **`scripts/extract_subject_results.py`** (layout detection/anchors) | `METRICS_PER_SUBJECT` per report year (2 vs 3 metrics), `SUBJECT_ALIASES`, `data/nsc/matric.db` tables (`subjects`, `subject_aliases`, `report_sources`, `subject_results`) |
| **`subject_results` schema** (`subject_id` primary key) | `scripts/extract_subject_results.py` upsert statement; downstream joins to `schools.centre_number` (centres, not EMIS numbers) |
| **Report PDFs in `data/school_subject_reports/`** | `scripts/extract_subject_results.py` file discovery (report year from filename, one file per province for 2014) and `--pdf-dir` defaults (`data/school_subject_reports`, `data/nsc/subject_pdfs`) |
| **`scripts/download_dbe_reports.py`** (source page/parsing) | `data/reports/` folder layout; re-run is safe and skips existing files |

---

## Key Files Quick Reference

Files that are frequently referenced in impact tables above. For anything not listed here, use Glob/Grep - the codebase follows predictable naming patterns documented in the convention skills.

### Backend Naming Patterns

```
src/backend/MatricDasbhoard.{Layer}/
  Shared:          Result.cs, ErrorType.cs, ErrorMessages.cs, PhoneNumberHelper.cs
  Domain:          Entities/{Entity}.cs
  Application:     Features/{Feature}/I{Feature}Service.cs
                   Features/{Feature}/Dtos/{Operation}Input.cs, {Entity}Output.cs
                   Features/{Feature}/Persistence/I{Feature}Repository.cs
                   Features/Email/EmailTemplateNames.cs
                   Identity/IUserService.cs, IUserContext.cs
                   Identity/Constants/AppRoles.cs, AppPermissions.cs
                   Caching/Constants/CacheKeys.cs
                   Cookies/ICookieService.cs, Constants/CookieNames.cs
                   Persistence/IBaseEntityRepository.cs
  Infrastructure:  Features/{Feature}/Services/{Feature}Service.cs
                   Features/{Feature}/Configurations/{Entity}Configuration.cs
                   Features/{Feature}/Extensions/ServiceCollectionExtensions.cs
                   Persistence/MatricDasbhoardDbContext.cs
  WebApi:          Features/{Feature}/{Feature}Controller.cs
                   Features/{Feature}/{Feature}Mapper.cs
                   Features/{Feature}/Dtos/{Operation}/{Operation}Request.cs
                   Features/{Feature}/Dtos/{Operation}/{Operation}RequestValidator.cs
                   Authorization/RequirePermissionAttribute.cs (+ handler, provider, requirement)
                   Authorization/ProblemDetailsAuthorizationHandler.cs
                   Routing/{Name}RouteConstraint.cs
                   Shared/RateLimitPolicies.cs
                   Program.cs
```

### Email Template Patterns

```
src/backend/MatricDasbhoard.Application/Features/Email/
  IEmailTemplateRenderer.cs                         Rendering interface (Render<TModel>)
  ITemplatedEmailSender.cs                          Safe render+send interface (SendSafeAsync)
  Models/EmailTemplateModels.cs                     Model records (one per template)
  EmailTemplateNames.cs                             Template name constants (kebab-case)
  IEmailService.cs                                  Sending interface
  EmailMessage.cs                                   Message envelope DTO
src/backend/MatricDasbhoard.Infrastructure/Features/Email/
  Services/FluidEmailTemplateRenderer.cs            Fluid-based renderer (singleton, cached)
  Services/TemplatedEmailSender.cs                  Render+send wrapper (swallows failures)
  Services/SmtpEmailService.cs                      MailKit SMTP sender (when Enabled)
  Services/NoOpEmailService.cs                      Dev/test no-op sender (when disabled)
  Templates/_base.liquid                            Shared HTML email layout (header, card, footer)
  Templates/{name}.liquid                           HTML body fragment
  Templates/{name}.text.liquid                      Plain text variant (optional)
  Templates/{name}.subject.liquid                   Subject line
  Options/EmailOptions.cs                           FromName, FrontendBaseUrl config
  Extensions/ServiceCollectionExtensions.cs         DI registration (AddEmailServices)
```

### Job Scheduling Patterns

```
src/backend/MatricDasbhoard.Infrastructure/Features/Jobs/
  IRecurringJobDefinition.cs                          Interface for recurring jobs
  RecurringJobs/{JobName}Job.cs                       Recurring job implementations
  Models/PausedJob.cs                                 Persisted pause state entity
  Configurations/PausedJobConfiguration.cs            EF config - hangfire.pausedjobs
  Services/JobManagementService.cs                    Admin API service (DB-backed pause)
  Options/JobSchedulingOptions.cs                     Configuration (Enabled, WorkerCount)
  Extensions/ServiceCollectionExtensions.cs           DI registration
  Extensions/ApplicationBuilderExtensions.cs          Middleware + job registration + pause restore
src/backend/MatricDasbhoard.Application/Features/Jobs/
  IJobManagementService.cs                            Admin API interface
  Dtos/RecurringJobOutput.cs, ...                     Job DTOs
src/backend/MatricDasbhoard.WebApi/Features/Admin/
  JobsController.cs                                   Admin job endpoints
  JobsMapper.cs                                       DTO mapping
  Dtos/Jobs/                                          Response DTOs
```

### Test Naming Patterns

```
src/backend/tests/
  MatricDasbhoard.Unit.Tests/
    {Layer}/{ClassUnderTest}Tests.cs             Unit tests (pure logic)
  MatricDasbhoard.Component.Tests/
    Fixtures/TestDbContextFactory.cs             InMemory DbContext factory
    Fixtures/IdentityMockHelpers.cs              UserManager/RoleManager mock setup
    Services/{Service}Tests.cs                   Service tests (mocked deps)
  MatricDasbhoard.Api.Tests/
    Fixtures/CustomWebApplicationFactory.cs      WebApplicationFactory config
    Fixtures/TestAuthHandler.cs                  Fake auth handler
    Contracts/ResponseContracts.cs               Frozen response shapes for contract testing
    Controllers/{Controller}Tests.cs             HTTP integration tests
    Validators/{Validator}Tests.cs               FluentValidation tests
  MatricDasbhoard.Architecture.Tests/
    DependencyTests.cs                           Layer dependency rules
    NamingConventionTests.cs                     Class naming enforcement
    AccessModifierTests.cs                       Visibility rules
```

### Singleton Files (no pattern - memorize these)

| File | Why it matters |
|---|---|
| `src/backend/MatricDasbhoard.WebApi/Program.cs` | DI wiring, middleware pipeline |
| `src/backend/MatricDasbhoard.Infrastructure/Persistence/MatricDasbhoardDbContext.cs` | DbSets, migrations |
| `src/backend/MatricDasbhoard.Shared/ErrorMessages.cs` | All static error strings |
| `src/backend/MatricDasbhoard.Application/Identity/Constants/AppRoles.cs` | Role definitions |
| `src/backend/MatricDasbhoard.Application/Identity/Constants/AppPermissions.cs` | Permission definitions (reflection-discovered) |
| `src/backend/MatricDasbhoard.Application/Caching/Constants/CacheKeys.cs` | Cache key constants (used across services) |
| `src/backend/MatricDasbhoard.Application/Features/Email/EmailTemplateNames.cs` | Email template name constants |
| `src/backend/MatricDasbhoard.WebApi/Shared/RateLimitPolicies.cs` | Rate limit policy name constants |
| `src/backend/Directory.Packages.props` | NuGet versions (never in .csproj) |
| `src/backend/MatricDasbhoard.ServiceDefaults/Extensions.cs` | Aspire shared: OTEL, service discovery, HTTP resilience defaults |
| `src/backend/MatricDasbhoard.AppHost/Program.cs` | Aspire orchestrator: local dev (PostGIS + GeoServer, loopback-only S3Mock with separate data volume, MailPit, API, Frontend); publish mode retains MinIO |
| `src/backend/MatricDasbhoard.WebApi/appsettings.Testing.json` | Test environment config |
| `src/backend/tests/MatricDasbhoard.Api.Tests/Fixtures/CustomWebApplicationFactory.cs` | Test host configuration for API tests |
