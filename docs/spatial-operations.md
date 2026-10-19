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

## Real school coordinates

`scripts/import_school_locations.py` matches the NSC performance report to the DBE
EMIS masterlists by EMIS number and upserts measured coordinates into
`geo.school_locations` with `is_sample=false`. Sources default to
`~/Documents/dbe-emis-downloads`; override with `DBE_DIR`, `DBE_PRIMARY`, and
`DBE_FALLBACK`. `scripts/geoserver/bootstrap.sh` runs it automatically when the
primary masterlist exists (set `IMPORT_LOCATIONS=0` to skip), and `just
geo-locations` runs the standalone wrapper:

```bash
./scripts/import_school_locations.sh
```

It normalizes two masterlist quirks before emitting SQL: provinces that store the
latitude in the longitude column, and provinces that store coordinates with the
decimal point dropped (`-2907075` means `-29.07075`). Coordinates outside the
South African envelope are dropped rather than written. Schools without usable
coordinates keep their sample point; set `UNRESOLVED_PATH` to a new CSV file to
review them. Real rows carry `is_sample=false`, so later sample seeds leave their
geometry untouched. Against the 2025 masterlists the bundled report resolves 6814
of 6871 valid schools, with the 2018 masterlist fallback filling 20 of the
remainder.

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
bash -n scripts/import_schools_to_db.sh scripts/import_school_locations.sh scripts/geoserver/bootstrap.sh
python3 -B -m unittest discover -s scripts/tests -v
docker compose config --quiet
```

Compose validation requires configured secrets. Tests use temporary CSV files,
in-memory SQL databases, and fake Docker/HTTP commands. They do not import data
into a running database or bootstrap the user's GeoServer.

## School grounds and editor location corrections

School map editing uses the `schools.spatial.edit` permission. Grant it to a
custom editor role in the existing administration screens, then assign that role
to approved users. The default User role must not receive it. Superusers retain
their existing permission bypass. Users may need a refreshed sign-in token after
a role change.

Editors select a school, open **Edit school map**, draw or modify its grounds,
and independently add or move its marker. They must provide a reason and source
description before saving. Save publishes the whole draft; Cancel discards it.
These are editor-traced school grounds, not surveyed cadastral boundaries.
Drawing and vertex editing currently require a mouse, pen, or touch pointer.
The form and toolbar support keyboard focus, but this version does not include a
keyboard-only coordinate editor.

### Deployment order

1. Back up the application database, including the `marks`, `geo`, and migration
   history schemas. Verify the backup is restorable.
2. Deploy the API and apply its EF migrations against the intended database. For
   a manually managed deployment, use the established connection configuration
   with the following command:

   ```bash
   dotnet ef database update \
     --project src/backend/MatricDasbhoard.Infrastructure \
     --startup-project src/backend/MatricDasbhoard.WebApi
   ```

   PostGIS must be available on the database server. Install the extension with
   a database administrator first if the migration role cannot create it. On a
   fresh installation the migration provisions the imported location table if
   absent, without seeding it. Existing source rows are never replaced.

3. Run `scripts/geoserver/bootstrap.sh` against that same stack. It now publishes
   `school_footprints` and `school_boundaries` in addition to the
   existing layers. It refuses publication if the spatial migration is missing.
   To publish only the legacy layers for an older API, explicitly set
   `GEOSERVER_LAYERS="school_locations provinces districts"`.
4. Deploy the frontend and grant the editor permission to the pilot group.
5. Save a correction on a known school and confirm that reloading the page shows
   the same grounds and marker. Check the editor-only history.

The migration owns the correction tables and public views. Bootstrap does not
create another definition of them. Bootstrap owns `geo.gt_pk_metadata` and
configures the GeoServer store to use the school UUID as the view feature key.
If the store already uses a different primary-key metadata table, bootstrap
stops instead of replacing it. Preserve the existing assignments in
`geo.gt_pk_metadata` and explicitly update the store setting before continuing,
or use a separate GeoServer store for these layers.
Empty school grounds are valid before the first correction; bootstrap publishes
empty spatial views with explicit world bounds instead of trying to infer bounds
from no features. Subsequent bootstrap runs recalculate bounds for nonempty
layers. Bootstrap retains its existing sample/import steps; set `SEED_FROM_API=0`,
`IMPORT_LOCATIONS=0`, and `IMPORT_BOUNDARIES=0` for publication-only maintenance
after the initial schema/sample setup.

### Correction precedence and recovery

`geo.school_locations` remains the imported source. Editor grounds, point
overrides, and editable location footprints live separately in
`geo.school_spatial_profiles`. The `school_footprints` view chooses the stored footprint,
or generates a small 25 m circular polygon around the editor override or imported
point. If that view is not published or returns no features, the frontend converts
legacy `school_locations` points into the same small polygon shape so school areas
remain visible until publication. A later DBE import may refresh the source
coordinates but cannot undo a manual override or footprint.
Sample seeds also leave editor profiles alone. A school with no imported point
can still receive an editor marker.

Each successful save creates a revision and a complete before/after history
entry in the same transaction. Actor and time come from the server. The protected
history API returns the editor's name and timestamp, resolved from the identity
store. History is editor-only; the public GeoServer views omit actor identities,
reasons, source notes, and revision history. GeoServer proxy requests remain
read-only.
The history API returns the latest 100 revisions; the database retains all
revisions. Initial request limits are 1 MiB, 10,000 boundary positions, 100 polygon
components, and 100 total rings. Reasons are limited to 2,000 characters and
source descriptions to 1,000. Review these limits during the pilot rather than
raising them to accept an unexpectedly detailed parcel import.

If another editor saves first, the API returns HTTP 409 and the frontend retains
the local draft. Compare with the current saved state before making another
change. Do not force an overwrite by editing the revision in a request.

Boundary deletion, clearing an existing point override, and one-click historical
restore are not part of this version. To correct an incorrect edit, make another
edit with a reason. For database recovery, restore a verified backup into a
separate database first, reconcile revision history, and promote it through the
normal deployment process. Do not drop correction tables or reset volumes to
repair a map or GeoServer publication problem.

### Optional imagery

No imagery provider is selected by default. Configure both runtime variables
only after checking the provider's South African coverage, tracing rights,
attribution requirements, quotas, and deployment license:

```dotenv
PUBLIC_MAP_IMAGERY_URL=https://your-licensed-provider.example/tiles/{z}/{x}/{y}.jpg
PUBLIC_MAP_IMAGERY_ATTRIBUTION=Your licensed imagery provider
```

The `.example` address above is a placeholder, not a working tile service. Use
HTTPS XYZ tiles in the map's Web Mercator projection. Leave both values empty to
use street maps only. For local frontend development, set them in
`src/frontend/.env`; for Compose, set them in the root `.env` and recreate the
frontend container. These values are public and read at runtime. Never put a
server-only credential in the tile URL. Restrict browser-visible keys by origin
and quota where supported. The map keeps attribution visible and returns to
street maps if imagery fails.

### Bulk data cleanup and validation

Do not scrape Google Maps or bulk-copy coordinates from its pages. Google Maps
Platform terms restrict scraping, caching, and redistributing results, including
displaying Google-derived coordinates on a non-Google map. Use the DBE EMIS
master data as the source of truth. Use Google Maps manually for spot checks, or
use a licensed geocoding/imagery API after checking quota, storage, and display
rights.

A practical cleanup order:

1. Reject or quarantine missing, zero, duplicate, and malformed EMIS identifiers
   before importing locations.
2. Flag coordinates outside South Africa, outside the school's province, still
   marked `is_sample`, or missing entirely.
3. Flag duplicate and near-duplicate coordinates, large differences from the DBE
   masterlist, and schools whose point is far from their district.
4. Sort the review queue by risk, not alphabetically. Review sample points,
   missing points, and confirmed import mismatches first.
5. Validate one school at a time with approved imagery. Paste the full Google
   Maps URL or `latitude, longitude` into the editor, locate, draw the grounds,
   and save a reason and evidence source.
6. Keep manual profiles separate from imports and preserve revision history. Never
   overwrite a manual correction with a later bulk import.

The editor accepts decimal coordinates (`-30.86536, 29.85241`) and full Google
Maps URLs containing `@latitude,longitude` or `!3d...!4d...`. Short
`maps.app.goo.gl` links contain no coordinates and must be opened before copying
the full URL.

### Isolated publication smoke test

```bash
python3 scripts/geoserver/smoke_test.py
```

This opt-in test creates disposable PostGIS and GeoServer containers on a new
Docker network. It checks empty-view publication, exposed UUID keys, stable
feature IDs, a newly added grounds polygon, and repeat bootstrap with the
existing store credentials. It uses fixture views to isolate publication from
the application migration, which has separate backend integration tests. It
removes only its own test containers, volumes, and network. It never bootstraps
the running application stack. Docker may download the test images if absent.
