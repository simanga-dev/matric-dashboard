using Aspire.Hosting.ApplicationModel;

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
var geoserverPort = frontendPort + 10;

// ── Infrastructure ──────────────────────────────────────────────────────────
// Container resources use session lifetime (default) - containers stop on
// Ctrl+C and restart on next run. Named data volumes persist across restarts,
// so database and file data survive. Explicit passwords ensure new containers
// can mount existing volumes without credential mismatch.

var pgPassword = builder.AddParameter("postgres-password", secret: true);
var storageUser = builder.AddParameter("storage-user");
var storagePassword = builder.AddParameter("storage-password", secret: true);
var jwtSecret = builder.AddParameter("jwt-secret", secret: true);
var meilisearchMasterKey = builder.AddParameter("meilisearch-master-key", secret: true);
var geoserverPassword = builder.AddParameter("geoserver-password", secret: true);

var postgres = builder.AddPostgres("db", password: pgPassword)
    // PostGIS build of PostgreSQL - spatial types/functions for the school map.
    // Must match the major version of the persisted dev volume (PostgreSQL 18).
    .WithImage("postgis/postgis")
    .WithImageTag("18-3.6-alpine")
    .WithEndpoint("tcp", e => e.Port = postgresPort)
    .WithDataVolume("matric-dasbhoard-db-data");

// pgAdmin only for local development
if (builder.ExecutionContext.IsRunMode)
{
    postgres.WithPgAdmin(pgAdmin => pgAdmin.WithEndpoint("http", e => e.Port = pgAdminPort));
}

var db = postgres.AddDatabase("Database");

IResourceBuilder<ContainerResource> storage;
if (builder.ExecutionContext.IsRunMode)
{
    // S3Mock accepts any credentials. Keep it local and separate from real MinIO data.
    storage = builder.AddContainer("storage", "adobe/s3mock")
        .WithImageSHA256("ab01a6946750f451ca215a47e91030695b260e4003b8a5a6201d25029b8fca92")
        .WithHttpEndpoint(port: minioPort, targetPort: 9090)
        .WithEndpoint("http", e => e.TargetHost = "127.0.0.1")
        .WithVolume("matric-dasbhoard-s3mock-data", "/s3mockroot")
        .WithEnvironment("COM_ADOBE_TESTING_S3MOCK_STORE_ROOT", "/s3mockroot")
        .WithEnvironment("COM_ADOBE_TESTING_S3MOCK_STORE_RETAIN_FILES_ON_EXIT", "true")
        .WithHttpHealthCheck("/favicon.ico");
}
else
{
    storage = builder.AddMinioContainer("storage", rootUser: storageUser, rootPassword: storagePassword)
        .WithImage("quay.io/minio/minio")
        .WithEndpoint("http", e => e.Port = minioPort)
        .WithEndpoint("console", e => e.Port = minioConsolePort)
        .WithDataVolume("matric-dasbhoard-storage-data");
}

// Meilisearch backs the school search box. Browse and map data come from
// Postgres/GeoServer; free-text search goes to the index built by
// scripts/upload_to_meilisearch.py.
var meilisearch = builder.AddMeilisearch("meilisearch", masterKey: meilisearchMasterKey)
    .WithEndpoint("http", e => e.Port = meilisearchPort)
    .WithDataVolume("matric-dasbhoard-meilisearch-data");

// GeoServer publishes the PostGIS school layer over OGC WMS/WFS. The API proxies
// client requests to it, so it is never exposed directly to browsers.
// Bootstrap the workspace/store/layer with: ./scripts/geoserver/bootstrap.sh
var geoserver = builder.AddContainer("geoserver", "docker.osgeo.org/geoserver", "2.26.2")
    .WithHttpEndpoint(port: geoserverPort, targetPort: 8080, name: "http")
    .WithVolume("matric-dasbhoard-geoserver-data", "/opt/geoserver_data")
    .WithEnvironment("SKIP_DEMO_DATA", "true")
    .WithEnvironment("GEOSERVER_ADMIN_USER", "admin")
    .WithEnvironment("GEOSERVER_ADMIN_PASSWORD", geoserverPassword)
    .WithEnvironment("CORS_ENABLED", "false")
    .WithEnvironment("EXTRA_JAVA_OPTS", "-Xms512m -Xmx1g")
    // The PostGIS datastore connection is configured by the bootstrap script.
    .WaitFor(db);

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
    .WaitFor(geoserver)
    .WithEnvironment("Authentication__Jwt__Key", jwtSecret)
    .WithEnvironment("FileStorage__Endpoint", storage.GetEndpoint("http"))
    .WithEnvironment("FileStorage__AccessKey", storageUser)
    .WithEnvironment("FileStorage__SecretKey", storagePassword)
    .WithEnvironment("FileStorage__BucketName", "matric-dasbhoard-files")
    .WithEnvironment("FileStorage__UseSSL", "false")
    .WithEnvironment("MEILI_URL", meilisearch.GetEndpoint("http"))
    .WithEnvironment("MEILI_MASTER_KEY", meilisearchMasterKey)
    .WithEnvironment("GeoServer__Enabled", "true")
    .WithEnvironment("GeoServer__BaseUrl", ReferenceExpression.Create($"{geoserver.GetEndpoint("http")}/geoserver"))
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
