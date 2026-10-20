# Dokploy deployment

Use `docker-compose.production.yml` as the Compose path in Dokploy. This file runs `frontend`, `api`, GeoServer, and a one-time GeoServer configuration service. Do not combine it with `docker-compose.yml`, which also creates infrastructure services.

The frontend waits for GeoServer configuration to finish successfully. Configuration waits for both GeoServer and the API to be healthy, allowing API database migrations to complete first. If startup is blocked, inspect API, GeoServer, and `geoserver-init` logs.

## Connections

Copy `.env.production.example` into Dokploy's environment editor and replace the placeholders. Keep credentials out of Git. The database connection uses Npgsql `Host=...;Database=...` syntax, not a PostgreSQL URI.

The API connects to your separately managed PostgreSQL/PostGIS, Meilisearch, and MinIO services. GeoServer runs in this stack and the API reaches it at `http://geoserver:8080/geoserver`. Use actual internal service hostnames for external dependencies, not display names or `localhost`. Set `EXTERNAL_SERVICES_NETWORK` to that existing network, normally `dokploy-network`. The API and GeoServer attach to it. Verify network attachment and DNS resolution in Dokploy before deploying.

Set `GEOSERVER_DB_HOST`, `GEOSERVER_DB_PORT`, and `GEOSERVER_DB_NAME` to the same database used by `DATABASE_CONNECTION_STRING`. GeoServer uses JDBC rather than Npgsql, so it needs separate connection settings. Prefer a dedicated user with schema USAGE and SELECT on `geo` tables and views, including `geo.gt_pk_metadata`. Set its credentials in `GEOSERVER_DB_USER` and `GEOSERVER_DB_PASSWORD`. Database traffic requires TLS by default; set `GEOSERVER_DB_SSLMODE=disable` only if you accept unencrypted traffic.

Set a strong `GEOSERVER_ADMIN_PASSWORD`. GeoServer has no published host port. Its catalog persists in `geoserver-data`; back up this volume. The `geoserver-init` service creates or updates the `matric` workspace, `schools` PostGIS datastore, and six map layers. It does not copy, seed, or modify database data. When changing admin credentials on an existing volume, update GeoServer's stored admin account too.

Set `TRUSTED_PROXY_NETWORK` to the actual CIDR of the Compose default network carrying frontend-to-API requests. You can inspect it on the VPS with `docker network inspect <network-name>`. Create the network through Dokploy first if necessary. Do not trust all addresses. If Dokploy changes the network subnet on redeployment, update this setting.

Add your HTTPS domain in Dokploy, targeting service `frontend`, port `3000`. Set `FRONTEND_URL` to that exact origin without a trailing slash. The frontend proxies requests to `http://matric-backend:8080` using an API alias on the stack's default bridge network; the browser does not connect directly to infrastructure. Do not add public API or database ports. Dokploy handles TLS; HTTPS redirection is disabled inside the API to avoid redirects on internal HTTP requests.

Redeploy both containers after network changes. Remove any stale Dokploy override that sets the frontend's `API_URL` to `http://api:8080`. If proxy requests fail, run `node -e "require('dns').lookup('matric-backend', console.log)"` inside the frontend container and check the API container's startup logs. A stopped or restarting API still requires fixing its startup error.

## Before deploying

- [ ] Verify `SELECT PostGIS_Full_Version();` succeeds in the application database.
- [ ] Back up and restore the existing application and spatial data into that database. Keep the source untouched until verification finishes.
- [ ] Ensure the main database has the `geo` tables/views and `geo.gt_pk_metadata`. Configure the GeoServer database/admin variables; the initializer publishes layers automatically. See `docs/spatial-operations.md`. Keep the external GeoServer until the new map works.
- [ ] Migrate or rebuild the Meilisearch school index and verify its settings.
- [ ] Create the MinIO bucket and migrate existing objects. This Compose file does not create buckets or change their access policy. Use the S3 API endpoint, not the MinIO console.
- [ ] Set production Turnstile keys for your domain.
- [ ] Set stable JWT and external-provider encryption keys. Preserve existing keys when migrating encrypted provider configuration.
- [ ] Configure persistent volumes and backups on each external service.
- [ ] Rotate any credentials previously shared in chat.

## Verification

Validate Compose with your populated environment before deployment:

```bash
docker compose --env-file .env.production -f docker-compose.production.yml config --quiet
```

After deployment, check API startup logs, login, school search, map layers, and file upload/download. A running service indicator alone does not verify data migration or service configuration.
