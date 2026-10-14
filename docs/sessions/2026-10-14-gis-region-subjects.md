# GIS Seeding, Region Stats and Subject Results

**Date:** 2026-10-14
**Branch:** `seed-gis-data`

## Summary

Seeds the dashboard with real DBE data and adds two new result layers. The branch
contains three commits:

1. `32a4e8f` - import real school coordinates and add a performance timeline.
2. `0ac68ec` - add region stats and province/district boundary layers.
3. `2b14ce6` - add school subject results (2012-2024) and region trends.

All changes are additive: no existing API contract fields were removed or renamed.

## Changes

### 1. School coordinates + performance timeline (`32a4e8f`)

- `scripts/import_school_locations.py` / `.sh` - load real coordinates from the DBE
  EMIS masterlists into `geo.school_locations` (PostGIS), replacing the sample points.
- `scripts/geoserver/bootstrap.sh` - refresh the GeoServer layer after seeding;
  `justfile` `geo-locations` target; `docs/spatial-operations.md` updated.
- `SchoolGeoMap.svelte` now renders real points; new `SchoolPerformanceTimeline.svelte`
  and updated `SchoolDetailPanel.svelte` show multi-year NSC history.

### 2. Region stats + boundary layers (`0ac68ec`)

- Backend: `DashboardService.GetRegionStatsAsync`, `RegionStatsOutput` /
  `RegionStatsResponse`, `DashboardController` `GET /dashboard/regions/stats`,
  `DashboardMapper`, api/component tests.
- Data: `scripts/import_boundaries.py`, `scripts/postgis/data/za-provinces.geojson`,
  `scripts/postgis/init.sql` (`geo.provinces`, `geo.districts` with PostGIS indexes).
- Frontend: `overview-url.ts` deep-linking, `SchoolTree.svelte`, `SchoolGeoMap.svelte`
  boundary layers, `types/dashboard.ts`, API contract types.

### 3. Subject results + region trends (`2b14ce6`)

- Staging pipeline (SQLite, gitignored at `data/nsc/matric.db`):
  - `scripts/download_dbe_reports.py` - fetch every document from the DBE Reports page.
  - `scripts/extract_nsc_results.py` - per-school overall results (2018-2025).
  - `scripts/extract_subject_results.py` - per-school subject results for 2012-2024
    across all report layouts (2/3 metrics, split 2014 province files, stacked years).
    752,671 rows, 10,534 centres, 11 gateway subjects.
  - Both extractors refresh only their own tables in the shared SQLite database.
- Postgres (serving store):
  - New `marks.Subjects` and `marks.SchoolSubjectPerformances` entities, EF configs,
    and migration `20261014083355_AddSchoolSubjects` (unique `(SchoolId, SubjectId, Year)`).
  - `scripts/import_subject_results.py` / `.sh` - idempotent import resolving
    centre -> EMIS (with a unique-centre fallback); 596,452 rows imported for 6,847
    schools in an end-to-end scratch-database test.
- API:
  - `GET /api/dashboard/schools/{id}/subjects?year=` (year optional, defaults to latest).
  - School detail now includes `subjectsOfferedYear` and `subjectsOffered` - the union
    of every subject the school has offered (most recent stats per subject), since
    12,064 school-subject pairs were only offered before the school's latest year.
- Frontend: the `/subjects` page (school search, year pills, subject table) and a
  "Subjects offered" section in `SchoolDetailPanel.svelte`.

## Key Decisions

- **SQLite as staging, Postgres as serving store.** Extraction is reproducible from the
  own PDFs; Postgres stays the source of truth for the API.
- **"Offered" means `totalWrote > 0`.** The DBE reports only cover the 11 gateway
  subjects, so this is candidate participation, not the full curriculum.
- **Union across years for the detail panel.** Showing only the latest year hid
  subjects offered earlier; each chip carries its own most recent year when older.
- **Historic centres are skipped.** ~156k subject rows use centre numbers that no
  longer map to a current school (2012-2014 numbering); these cannot be attributed.

## Verification

- Backend: `dotnet build` clean; `dotnet test -c Release` - 1,176 passed / 0 failed
  (Unit 97, Architecture 10, Component 556 with 15 pre-existing skips, Api 513).
- Frontend: `pnpm check` 0 errors; `pnpm test` 377 passed.
- Import: applied the migration plus both importers against a throwaway PostGIS
  container; re-running left row counts unchanged (idempotent).

## Follow-ups

- Apply the migration and run `./scripts/import_subject_results.sh` against the
  real Postgres database, then verify the overview panel.
- `.env` is missing `MEILI_MASTER_KEY`, so `docker compose up` currently fails on this
  machine.
- Possible next steps: subject filter on the school list, Meilisearch facet for offered
  subjects, and subject-level region stats.
