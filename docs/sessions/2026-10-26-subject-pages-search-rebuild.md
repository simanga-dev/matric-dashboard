# Subject pages, search index rebuild, and directory polish

## Changes

- Added `GET /api/v1/dashboard/subjects/{code}` and `GET /api/v1/dashboard/subjects/{code}/schools`. The detail endpoint returns KPIs, a yearly trend, and province/district breakdowns; the schools endpoint is paged, searchable, sortable, and filterable by scope and year. Both are additive and stay public like the rest of the dashboard read API.
- `/api/v1/dashboard/schools` accepts an optional `province` filter so the directory can scope a page without client-side trimming.
- Built `/subjects/[code]`: KPI row, yearly trend, region breakdown with drill-down, and a paged list of schools that wrote the subject. Scope and year live in the URL (`subject-url.ts`), so a copied link reproduces the view.
- Reworked `/subjects` with a name filter, sortable columns, a KPI strip, and rows/bars that link into the detail page.
- Reworked `/schools` with a province filter, URL-synced search/province/sort/page (`schools-url.ts`), and a KPI strip from region stats. The previous `?search=` deep links still work.
- Added `POST /api/v1/search-index/schools` behind authentication and the sensitive-operation rate limit. It replaces the Meilisearch `schools` index with every current school and returns indexed/removed counts and the duration; a failed Meilisearch task maps to `503`.
- Added `SchoolSearchIndexService` plus `SeedSearchButton` so an operator can rebuild the index from the UI. `upload_to_meilisearch.py` now matches the same document shape.
- Rebuilt the school detail page around `SchoolLocationMap`, which owns the single-school polygon view and is shared by the detail page and the map tests. Added a `DataSources` dialog that explains where the results, directory, boundaries, and basemap come from.
- Added the overview school-directory dialog. `?schools=1` opens it, closing the dialog clears the parameter, and the dialog is deliberately excluded from the remembered workspace so returning to the page does not reopen it.
- Sidebar and user navigation were restructured to make the directory, subjects, and overview reachable from one place. "Top Achievers" is hidden for now.
- Fixture and component tests cover the new endpoints, the search-index controller, the subject projector against PostGIS, both URL codecs, and the new tables and dialogs.

## Decisions

- Subject detail is served by dedicated endpoints instead of overloading `/schools/{id}/subjects`. The subject page needs aggregates (yearly trend, region rollup) that would otherwise force several client calls and merge logic.
- Paging, sorting, and filtering for the subject schools list stay server-owned. Client-side sorting only orders the rows that happen to be loaded, which makes the page numbers wrong.
- The URL codecs (`subject-url.ts`, `schools-url.ts`) are pure functions with tests, so the page components only bind state and never assemble query strings inline.
- The overview dialog uses a boolean `schools=1` rather than a full view state. The dialog is transient: a reload restores it, but the in-memory workspace fallback drops it.
- The search rebuild is an authenticated, rate-limited POST that returns counts. A background job was rejected because the rebuild is an operator action that should report success or failure immediately.

## Verification and follow-up

- `dotnet build src/backend/MatricDasbhoard.slnx` and the WebApi test suite, including the PostGIS subject tests.
- `pnpm test`, `pnpm check`, `prettier --check`, and `eslint` on the frontend.
- Follow-ups not addressed here: `v1.d.ts` was hand-edited in one commit and should be regenerated with `pnpm run api:generate`, and the search rebuild has no scheduled job, so the index only refreshes when an operator runs it.
