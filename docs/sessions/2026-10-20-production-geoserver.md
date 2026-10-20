# Production GeoServer

## Changes

- `a338568` added production datastore/layer provisioning, regression tests, environment examples, and the previous session notes.
- Restored the missing production Compose services so those settings and scripts are actually used.
- `docker-compose.production.yml` now runs GeoServer with persistent catalog storage and a one-time initializer. The API uses its private service URL and the frontend waits for successful setup.
- `docs/dokploy-deployment.md` documents the shared external database, startup ordering, credentials, and migration prerequisites.
- `FILEMAP.md` records the related configuration and test files.
- The earlier API network-alias commit was already merged through PR #45.

## Decisions

Use the main PostGIS database rather than creating another database container. Keep GeoServer private behind the API proxy. Require TLS by default with an explicit opt-out for database connections. Keep placeholder credentials in the example environment.

## Verification and follow-up

Run `python -m unittest discover -s scripts/tests` before merging. Compose validation is included in these tests. Deployment still requires PostGIS, migrated spatial data, and production credentials. Verify the map before removing the external GeoServer. This change does not deploy services or migrate data.
