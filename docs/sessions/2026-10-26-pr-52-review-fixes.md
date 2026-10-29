# PR 52 review fixes

## Changes

- Fixed the backend formatting violations in `DashboardService` that failed the `dotnet format` CI step.
- The search index rebuild now publishes with the `id` primary key. The uploader script writes the EMIS number to both `id` and `school_id`, but the API's `school_id` field is not the index primary key, and the existing index was created with `id`. The uploader now sends `primaryKey=id` explicitly so Meilisearch's inference cannot pick the wrong field.
- `POST /api/v1/search-index/schools` requires the new `search_index.manage` permission, uses the `admin-mutations` rate limit, and writes a `SearchIndexRebuild` audit event with the indexed, removed, and duration counts. The Admin role is seeded with the permission.
- The rebuild no longer deletes documents when the database projection is empty. An empty result is treated as a misconfigured connection and leaves the live index untouched.
- A `MeilisearchTimeoutError` from a task wait now maps to `503 Service Unavailable` instead of escaping as an unhandled 500.
- Added `SchoolSearchIndexServiceTests`, which drive the service against a fake Meilisearch HTTP handler. Coverage includes the empty-database guard, the upload primary key, stale document removal, a failed task, and a task timeout.
- The index seed button is only shown to users with `search_index.manage`.
- The school detail page keys the location map by school id, so history navigation between two schools replaces the map instead of keeping the first school's view.
- A schools URL that carries a metric sort but no explicit order now defaults to descending, matching the API.
- CI now runs the backend tests, frontend tests, and ESLint alongside the existing build and format checks. A shared test `afterEach` waits for the bits-ui body-scroll cleanup timer, which previously made jsdom teardown fail intermittently.

## Decisions

- `id` stays the Meilisearch primary key because the legacy index and both scripts use it. `school_id` remains in every document for the API to read back, but treating it as the primary key would require deleting and recreating the production index.
- The new permission is seeded for Admin rather than being Superuser-only, because index maintenance is a routine operator action. Custom roles can still be granted or denied explicitly.
- Rebuilds remain synchronous. Moving them to a Hangfire job is still a reasonable follow-up, but the endpoint now reports failures correctly and returns counts on success.
- The subject pages still use hardcoded English strings; translating them is a separate pass because it needs new keys in both locale directories.

## Verification

- `dotnet build src/backend/MatricDasbhoard.slnx`, `dotnet test` (123 unit, 10 architecture, 537 API, 600 component), `dotnet format --verify-no-changes`.
- `pnpm test` (660 tests), `pnpm check` (0 errors), `prettier --check .`, `eslint .`, `pnpm build`.
