var builder = DistributedApplication.CreateBuilder(args);

var frontendPort = int.TryParse(builder.Configuration["Ports:Frontend"], out var fp) ? fp : 13000;
var apiPort = int.TryParse(builder.Configuration["Ports:Api"], out var ap) ? ap : 13002;

// Derive infrastructure ports from the base (frontend) port so the entire
// stack lives in one predictable range - no collisions between projects.
var pgAdminPort = frontendPort + 3;
var postgresPort = frontendPort + 4;
var minioPort = frontendPort + 5;
var minioConsolePort = frontendPort + 6;
var mailpitSmtpPort = frontendPort + 7;
var mailpitHttpPort = frontendPort + 8;
var meilisearchPort = frontendPort + 9;

// ── Infrastructure ──────────────────────────────────────────────────────────
// Container resources use session lifetime (default) - containers stop on
// Ctrl+C and restart on next run. Named data volumes persist across restarts,
// so database and file data survive. Explicit passwords ensure new containers
// can mount existing volumes without credential mismatch.

var pgPassword = builder.AddParameter("postgres-password", secret: true);
var storageUser = builder.AddParameter("storage-user");
var storagePassword = builder.AddParameter("storage-password", secret: true);
var jwtSecret = builder.AddParameter("jwt-secret", secret: true);
var meilisearchMasterKey = builder.AddParameter("meilisearch-master-key");

var postgres = builder.AddPostgres("db", password: pgPassword)
    .WithEndpoint("tcp", e => e.Port = postgresPort)
    .WithDataVolume("matric-dasbhoard-db-data");

// pgAdmin only for local development
if (builder.ExecutionContext.IsRunMode)
{
    postgres.WithPgAdmin(pgAdmin => pgAdmin.WithEndpoint("http", e => e.Port = pgAdminPort));
}

var db = postgres.AddDatabase("Database");

var storage = builder.AddMinioContainer("storage", rootUser: storageUser, rootPassword: storagePassword)
    .WithEndpoint("http", e => e.Port = minioPort)
    .WithEndpoint("console", e => e.Port = minioConsolePort)
    .WithDataVolume("matric-dasbhoard-storage-data");

var meilisearch = builder.AddMeilisearch("meilisearch", masterKey: meilisearchMasterKey)
    .WithEndpoint("http", e => e.Port = meilisearchPort)
    .WithDataVolume("matric-dasbhoard-meilisearch-data");

// ── API ─────────────────────────────────────────────────────────────────────
// Migrations and seeding are handled by the API on startup (development only).
// See: ApplicationBuilderExtensions.InitializeDatabaseAsync

var api = builder.AddProject<Projects.MatricDasbhoard_WebApi>("api")
    .WithEndpoint("http", e =>
    {
        e.Port = apiPort;
        e.IsProxied = false;
    })
    .WithReference(db)
    .WaitFor(db)
    .WaitFor(storage)
    .WaitFor(meilisearch)
    .WithEnvironment("Authentication__Jwt__Key", jwtSecret)
    .WithEnvironment("FileStorage__Endpoint", storage.GetEndpoint("http"))
    .WithEnvironment("FileStorage__AccessKey", storage.Resource.RootUser)
    .WithEnvironment("FileStorage__SecretKey", storage.Resource.PasswordParameter)
    .WithEnvironment("FileStorage__BucketName", "matric-dasbhoard-files")
    .WithEnvironment("FileStorage__UseSSL", "false")
    .WithEnvironment("MEILI_URL", meilisearch.GetEndpoint("http"))
    .WithEnvironment("MEILI_MASTER_KEY", meilisearchMasterKey)
    ;

// Mailpit only for local development - production uses real SMTP (configured via environment variables)
if (builder.ExecutionContext.IsRunMode)
{
    var mailpit = builder.AddMailPit("mailpit", httpPort: mailpitHttpPort, smtpPort: mailpitSmtpPort);
    api.WaitFor(mailpit)
        .WithEnvironment("Email__Smtp__Host", mailpit.Resource.Host)
        .WithEnvironment("Email__Smtp__Port", () => mailpitSmtpPort.ToString())
        .WithEnvironment("Email__Smtp__UseSsl", "false");
}

// ── Frontend (SvelteKit) ────────────────────────────────────────────────────

builder.AddViteApp("frontend", "../../../src/frontend")
    .WithPnpm()
    .WithEndpoint("http", e =>
    {
        e.Port = frontendPort;
        e.IsProxied = false;
    })
    .WithEnvironment("API_URL", "http://127.0.0.1:" + apiPort)
    .WithEnvironment("TURNSTILE_SITE_KEY", "1x00000000000000000000AA")
    .WaitFor(api);

builder.Build().Run();
