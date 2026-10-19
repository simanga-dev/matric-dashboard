# Dokploy deployment

Use `docker-compose.production.yml` as the Compose path in Dokploy. This file runs only `frontend` and `api`. Do not combine it with `docker-compose.yml`, which also creates infrastructure services.

## Connections

Copy `.env.production.example` into Dokploy's environment editor and replace the placeholders. Keep credentials out of Git. The database connection uses Npgsql `Host=...;Database=...` syntax, not a PostgreSQL URI.

The API connects to your separately managed PostgreSQL/PostGIS, Meilisearch, GeoServer, and MinIO services. Use their actual internal service hostnames, not the display names or `localhost`. All services must share a reachable Docker network. Set `EXTERNAL_SERVICES_NETWORK` to that existing network, normally `dokploy-network`. Verify network attachment and DNS resolution in Dokploy before deploying.

Set `TRUSTED_PROXY_NETWORK` to the actual CIDR of the Compose default network carrying frontend-to-API requests. You can inspect it on the VPS with `docker network inspect <network-name>`. Create the network through Dokploy first if necessary. Do not trust all addresses. If Dokploy changes the network subnet on redeployment, update this setting.

Add your HTTPS domain in Dokploy, targeting service `frontend`, port `3000`. Set `FRONTEND_URL` to that exact origin without a trailing slash. The frontend proxies requests to `http://api:8080`; the browser does not connect directly to infrastructure. Do not add public API or database ports. Dokploy handles TLS; HTTPS redirection is disabled inside the API to avoid redirects on internal HTTP requests.

## Before deploying

- [ ] Verify `SELECT PostGIS_Full_Version();` succeeds in the application database.
- [ ] Back up and restore the existing application and spatial data into that database. Keep the source untouched until verification finishes.
- [ ] Configure GeoServer's PostGIS store and publish the required school layers. See `docs/spatial-operations.md` and `scripts/geoserver/bootstrap.sh`.
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
