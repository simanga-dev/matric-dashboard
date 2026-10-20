# School names, map layers, and deployment

## Commits

- `c61d541` adds a cleaned school-name export using the DBE EMIS masterlist.
- `659510f` applies cleaned names to SQLite and Postgres and uses them in search uploads.
- `4d7fb11` adds a toggleable map layers panel alongside school editing controls.
- `919b1c8` adds production Compose configuration and Dokploy deployment guidance.

## Files and decisions

- `scripts/clean_school_names.py` matches names by EMIS, expands school-type abbreviations, and flags unmatched records without modifying the source report.
- `scripts/apply_clean_school_names.py` backs up SQLite, writes an audit of changed names, and emits transactional SQL for both Postgres name tables.
- `scripts/apply_clean_school_names.sh` connects the update script to a running Postgres container.
- `scripts/upload_to_meilisearch.py` substitutes cleaned names when an export is available.
- `scripts/tests/test_school_names.py` covers normalization, masterlist matching, database updates, and search documents.
- `justfile` exposes export and application commands.
- `docs/school-name-cleaning.md` documents the workflow, backups, and search reindexing.
- `FILEMAP.md` lists the new scripts and documentation.
- `src/frontend/src/lib/components/dashboard/SchoolGeoMap.svelte` combines background selection and editing in a toggleable panel.
- `docker-compose.production.yml` runs the frontend and API against separately managed infrastructure.
- `.env.production.example` lists deployment settings without real credentials.
- `docs/dokploy-deployment.md` documents network configuration, prerequisites, and deployment verification.

## Verification

- Python unittest discovery ran 47 tests, with 46 passing and one skipped because `requests` is not installed. Python also reported unclosed SQLite connection warnings.
- Frontend `pnpm check` passed with no errors and 11 warnings in unchanged components.
- Frontend `pnpm test` passed all 562 tests across 39 files.
- Backend build, backend tests, frontend lint, and production deployment were not run during PR preparation.

## Follow-up

- Run the skipped search-document test with `requests` installed.
- Populate production environment settings and complete the Dokploy deployment checklist before deploying.
