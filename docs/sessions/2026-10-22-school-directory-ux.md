# School directory and map editing UX

## Changes

- Restored the public `/schools` directory page and sidebar entry, reusing `SchoolsTable` with a page size of 25 and a deep-link `?search=` parameter.
- Added a `mapUpdatedAt` column and server-side sorting for every directory column. `IDashboardService.GetSchoolsAsync` now accepts `sort` and `order`, maps them to a fixed set of columns, and keeps stable tie-breakers so pages do not overlap.
- The map-correction order joins `geo.school_spatial_profiles` through raw SQL because the PostGIS table is intentionally outside the EF model. Pages are then hydrated from the existing projection, and every page carries `MapUpdatedAt` for the directory.
- Added `scripts/create_admin.py` (`Dokploy` one-time Admin bootstrap) with identity V3 hashing, a guarded transaction, and tests. Documented the operator steps in `docs/dokploy-deployment.md`.
- The school map editor now finishes and saves a drawn polygon with Enter or S, and the selected school's editor opens with E. Both are discoverable through the shortcut dialog.
- The school browser now starts at a 380px default width on wide layouts, and the editor's coordinate form is collapsed behind `Locate by coordinates`.
- Removed the raw NSC report files from version control. The tests that parse them skip when the file is absent, and `docs/school-name-cleaning.md` explains where to place it.
- Review follow-ups: em dash removed from the map-updated formatter, directory searches debounced and aborted when superseded, and shortcut handling moved into the shared registry with target and modal guards.

## Decisions

- Sorting stays server-owned so every page of the directory is ordered, not only the loaded rows. Meilisearch relevance ranking is skipped when an explicit sort is requested, because it cannot preserve a global order across pages.
- The map timestamp is read with parameterised SQL instead of adding the PostGIS profile table to the EF model.
- Single-key shortcuts go through `$lib/state/shortcuts.svelte.ts` so they appear in the help dialog, respect input focus, and share one modal check. Components register their own handlers for actions the root layout cannot own.
- The admin bootstrap writes directly to the database only for a brand-new account. It refuses duplicates, never resets passwords, and does not grant Superuser or spatial editing permissions.

## Verification and follow-up

- `dotnet build src/backend/MatricDasbhoard.slnx`, `dotnet test` component suite (including the PostGIS map-update tests), `dotnet format --verify-no-changes`.
- `pnpm test` 595 tests, `pnpm check` 0 errors, `prettier --check` and `eslint` clean.
- `python3 -m unittest discover -s scripts/tests` passes with the report present and skips the two report tests without it.
- Follow-ups not addressed here: no CI job runs the Python tests, and the schools table headers and pagination text still bypass i18n.
