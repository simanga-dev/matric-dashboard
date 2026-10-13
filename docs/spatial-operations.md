# Spatial data operations

## PostgreSQL versions and persisted volumes

Compose defaults to `postgis/postgis:17-3.5-alpine` and mounts `postgres-data`
at `/var/lib/postgresql/data`. This retains the PostgreSQL 17 layout used by
existing Compose installations. Adding PostGIS does not upgrade PostgreSQL's
data files. Back up the database before enabling extensions or changing images.

Aspire uses PostgreSQL 18 with the separate `matric-dasbhoard-db-data` volume.
Its existing local volume was inspected during this fix: `PG_VERSION` is `18`.
Keep AppHost on 18. Compose and Aspire do not need the same major version when
they use separate volumes. Never mount either stack's volume with a different
PostgreSQL major version.

For a fresh PostgreSQL 18 Compose installation, set all three values in `.env`:

```dotenv
POSTGIS_IMAGE=postgis/postgis:18-3.6-alpine
POSTGRES_VOLUME=postgres-data-pg18
POSTGRES_VOLUME_TARGET=/var/lib/postgresql
```

PostgreSQL 18 uses `/var/lib/postgresql/18/docker` below that mount. The separate
named volume prevents a fresh cluster from hiding existing PostgreSQL 17 files.
If you already initialized a Compose PostgreSQL 18 volume with the earlier
configuration, retain its actual volume name and parent mount using these
overrides. Do not point the PostgreSQL 17 default at PostgreSQL 18 files.

### Migrating Compose from 17 to 18

1. Record the current container image, volume name, mount target, and `PG_VERSION`.
   Confirm the backup can be restored before changing the stack.
2. Pause application writes. Use the running PostgreSQL 17 instance to create
   logical backups of every application database with `pg_dump -Fc`, plus roles
   and other globals with `pg_dumpall --globals-only`. Store backups outside
   Docker volumes. Record row counts and PostGIS versions.
3. Stop Compose without deleting volumes. Set the three PostgreSQL 18 overrides
   above and start only `postgres` with the new `postgres-data-pg18` volume.
4. Restore required roles and databases into PostgreSQL 18 with `psql` and
   `pg_restore`. The new volume's init script creates a sample `geo` schema;
   resolve those initialization objects in the new database before restoring
   existing spatial objects. Do not remove objects from the old database.
5. Check extensions, school/performance counts, real geometries, and application
   reads. Bootstrap GeoServer to refresh layer bounds, then resume application
   writes. Retain the old volume and backups until migration is accepted.

Changing the image and mount path alone is not a migration. Do not use
`docker compose down -v` to resolve a version mismatch. Rollback before resuming
writes uses the old PostgreSQL 17 settings and retained volume; after new writes,
rollback also requires a data reconciliation plan.

## Import identities and quarantine

`scripts/import_schools_to_db.py` preserves real EMIS strings, including leading
zeroes. Centre numbers are not substituted for EMIS identifiers. Missing EMIS,
zero placeholders, `NA`, `N/A`, and `-` are rejected with the CSV row number,
centre number, and name on stderr. By default any such row refuses the whole
import before SQL is emitted. The source CSV remains unchanged.

The bundled report contains 45 centres with EMIS `0` and 5 with blank EMIS.
The earlier importer collapsed the 45 zero rows into one school, losing 44
records, and skipped the blanks. To import the valid schools while retaining
all 50 unresolved records, explicitly choose a new quarantine file:

```bash
QUARANTINE_PATH=/tmp/nsc-unresolved-emis.csv ./scripts/import_schools_to_db.sh
```

The quarantine CSV contains every source column and every rejected row. The
script refuses to overwrite an existing quarantine file and reports the count
and path on stderr. Review each centre against authoritative DBE identifiers,
correct a copy of the source, and reimport it. Do not fabricate EMIS identifiers
or combine the zero-EMIS centres into one school. Quarantined rows contribute no
school or performance records until corrected.

Conflicting rows sharing a real EMIS also refuse the whole import before SQL.
Exact duplicate source rows are reported and imported once. Empty inputs emit
no SQL; schools without performance data do not produce an empty `VALUES` list.
If a previous import already created EMIS `0`, inspect and reconcile that school
and its linked performance records separately. This fix does not delete it or
attempt to reconstruct records that the earlier import overwrote.

For multiple running database stacks, explicitly set `PG_CONTAINER`, `PG_DB`,
and `PG_USER` when running import/bootstrap rather than relying on autodetection.

## Sample coordinates and GeoServer maintenance

Both the static init SQL and API sample seed update only conflicting rows whose
`is_sample` flag is true. Rows marked `is_sample=false`, including their real
geometry and metadata, are retained. Real coordinate imports must set that flag
to false. The API seed replaces only sample rows in one transaction; empty API
results retain existing rows. Sample coordinates are province-centroid jitter,
not measured school positions.

Compose publishes GeoServer only on `127.0.0.1:${GEOSERVER_PORT:-8080}` for local
administration and `scripts/geoserver/bootstrap.sh`. The API still reaches
`http://geoserver:8080/geoserver` on the Compose network. Browser map requests
use the API proxy. Remote administration needs an SSH tunnel to the loopback
port rather than an all-interface publication.

Bootstrap recalculates both native and latitude/longitude bounds after the final
seed on every run, including existing layers and `SEED_FROM_API=0` runs. Failed
recalculation exits nonzero. When running `seed_from_api.py` independently,
refresh bounds through GeoServer REST after applying its SQL, or run bootstrap.

## Non-destructive regression checks

```bash
bash -n scripts/import_schools_to_db.sh scripts/geoserver/bootstrap.sh
python3 -B -m unittest discover -s scripts/tests -v
docker compose config --quiet
```

Compose validation requires configured secrets. Tests use temporary CSV files,
in-memory SQL databases, and fake Docker/HTTP commands. They do not import data
into a running database or bootstrap the user's GeoServer.
