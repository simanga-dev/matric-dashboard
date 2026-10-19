# School grounds and location editing plan

Status: implemented. Deployment and configuration instructions are in
[Spatial data operations](../spatial-operations.md#school-grounds-and-editor-location-corrections).
Verification results are recorded in the
[implementation session](../sessions/2026-10-19-school-map-editing.md).

## Implementation decisions

- API routes use `/api/v1/schools/{emis}/spatial` and its `/history` subroute.
- The backend uses parameterized PostGIS SQL rather than mapped NetTopologySuite
  entities. EF migrations own the tables, constraints, views, and immutable
  history trigger. No extra geometry serializer or spatial NuGet package is needed.
- A parent-school row lock protects concurrent first saves as well as later
  revisions. Clients still send an expected revision and receive HTTP 409 for a
  stale draft.
- The migration provisions the imported point table only if absent. It never
  seeds or replaces imported rows, and rollback preserves that source table.
- Imagery is opt-in through runtime `PUBLIC_MAP_IMAGERY_URL` and
  `PUBLIC_MAP_IMAGERY_ATTRIBUTION`. No provider or credentials ship by default.
- Publication smoke tests use disposable containers, not the running application
  database or GeoServer. Applying migrations and publishing production layers are
  separate deployment steps.

## Confirmed scope

- Approved editors can draw and update the physical grounds of an individual school.
- Editors can independently correct the school's point marker.
- Include an aerial or satellite background to help trace school grounds.
- Use the existing sign-in and role-permission system. Signing in alone does not grant edit access.
- This is not district, circuit, ward, or catchment editing.

Recommendation: keep GeoServer as the read/publishing service. Save changes through the .NET API into PostGIS. Do not enable browser WFS transactions or expose GeoServer administration credentials.

## What exists today

Paths below are relative to this repository unless an absolute path is shown.

| Area            | Finding                                                                                                                                                              | Relevant files                                                                                                                                             |
| --------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Map             | Svelte 5 with OpenLayers 10.11. School selection uses the EMIS string. The map reads WFS GeoJSON and uses OpenStreetMap tiles.                                       | `src/frontend/src/lib/components/dashboard/SchoolGeoMap.svelte`, `src/frontend/package.json`                                                               |
| Coordinates     | School points live in `geo.school_locations`, with EPSG:4326 geometry and a unique EMIS index. Province and district polygons are separate layers.                   | `scripts/postgis/init.sql`                                                                                                                                 |
| School identity | Application schools live in `marks."Schools"`, with a UUID primary key and unique string EMIS. They have no grounds geometry.                                        | `src/backend/MatricDasbhoard.Domain/Entities/School.cs`, `src/backend/MatricDasbhoard.Infrastructure/Features/Marks/Configurations/SchoolConfiguration.cs` |
| Publication     | Bootstrap publishes `matric:school_locations`, provinces, and districts. GeoServer and the API use the same PostgreSQL stack.                                        | `scripts/geoserver/bootstrap.sh`, `src/backend/MatricDasbhoard.AppHost/Program.cs`                                                                         |
| Proxy           | Both proxy layers deliberately restrict OGC requests to read operations. Tests explicitly reject WFS Transaction and locking operations.                             | `src/backend/MatricDasbhoard.WebApi/Middlewares/GeoServerProxyMiddleware.cs`, `src/frontend/src/routes/api/geo/[...path]/+server.ts`                       |
| Permissions     | Atomic role permissions already exist on the backend and frontend.                                                                                                   | `src/backend/MatricDasbhoard.Application/Identity/Constants/AppPermissions.cs`, `src/frontend/src/lib/utils/permissions.ts`                                |
| Cache           | WFS results stay in a session-scoped promise cache. It currently has no explicit invalidation operation.                                                             | `src/frontend/src/lib/state/geo-data.ts`                                                                                                                   |
| Imports         | The real-coordinate importer unconditionally updates conflicting coordinates. `is_sample=false` protects against sample seeding, not a later real-coordinate import. | `scripts/import_school_locations.py`, `docs/spatial-operations.md`                                                                                         |

These findings come from source review, not a running database or browser inspection.

## What to take from GLF

Reference root: `/home/simanga/Work/glf-core/src/WebGLF.Next/ClientApp/src`.

- `components/MapView/PowerGLFMap/MapToolbar.js` uses OpenLayers `Draw`, `Modify`, and `Snap`. Its `modifyend` handler validates geometry, checks the editing session, saves through `/geom/EditFeatures`, and refreshes map data. See approximately lines 1516 to 1682.
- `data/api/GeoServerAPI/GeoServerAPI.js` reads WFS through an application proxy and creates geometry through `/geom/CreateNodeGeom`. It does not use a browser WFS transaction for that creation flow.
- `components/LoadZoneImport/TurfHelper.js` contains polygon checks, including minimum vertices and self-intersections. It also contains parent, neighbour, overlap, and gap rules specific to GLF load zones.

Reuse the interaction patterns, selected-feature isolation, validation feedback, and refresh-after-save behavior. Adapt them to TypeScript and Svelte rather than copying the React toolbar.

Do not bring over GLF's branch model, node hierarchy, split/merge tools, shared-edge topology, or live locks for the first version. School grounds do not form a continuous coverage layer, so gaps between polygons are expected. Shared campuses may also legitimately overlap.

Unlike GLF's save-on-modify behavior, use explicit Save and Cancel. An editor should be able to move several vertices before publishing the correction.

## Editor workflow

1. Select a school from the existing map or school tree.
2. An authorized user opens an "Edit school map" panel showing the school name and EMIS prominently.
3. Switch between the existing street background and the configured imagery background. Keep attribution visible.
4. Load the selected school's current geometry and revision from the API. Fit the existing grounds if present; otherwise use its point. Allow map search or navigation if that point is missing or incorrect.
5. Choose "Draw grounds", "Edit grounds", or "Move marker". Provide a way to add a missing marker too.
6. Draw or adjust geometry in a separate draft layer. Other schools and administrative boundaries remain read-only. Freeze school selection while editing, or require Save/Discard before switching.
7. Show validation errors, approximate area, and a source/reason field. Identify the result as an editor-traced boundary, not a surveyed cadastral boundary.
8. Save the whole draft as one operation. Cancel restores the saved state. Warn before navigation with unsaved work.
9. Display the server-returned geometry immediately. Invalidate affected WFS cache entries and reload published layers.

Moving a marker must not move the polygon, and drawing a polygon must not silently replace the marker with its centroid. Warn if the marker is outside the grounds, but allow an explicit explanation because existing source coordinates may be wrong.

## Storage and import protection

Keep imported source coordinates in `geo.school_locations`. Add an application-owned spatial profile rather than writing manual corrections into the importer's table.

Proposed `geo.school_spatial_profiles`:

- `school_id`: UUID primary key and foreign key to `marks."Schools"`.
- `boundary`: nullable `geometry(MultiPolygon,4326)`. Accept a Polygon request and normalize it to MultiPolygon. Initially draw one polygon, but allow the data model to support detached school parcels later.
- `location_override`: nullable `geometry(Point,4326)`.
- `revision`: concurrency token, incremented on every saved change.
- Change reason and source description, plus server-owned editor and UTC timestamps.
- Spatial indexes and geometry checks. Select the school deletion policy explicitly; do not cascade away correction history by default.

Keep school linkage by UUID internally and expose EMIS as a string for the existing selection flow. Never parse EMIS into a number or invent an EMIS for unresolved import records.

Add an append-only history table with complete before/after profile snapshots, revisions, actor, server timestamp, and reason. Commit the profile and history entry in one database transaction. Record first creation as well as later changes. The generic security audit log alone is not a geometry revision store.

Publish two read-only GeoServer views:

- `matric:school_boundaries`: current, nonempty school grounds with school identifiers and safe display metadata.
- `matric:school_locations_effective`: use the location override when present, otherwise the imported location. Start from application schools and left-join imported coordinates and profiles, so an editor can add a marker even when no imported point exists.

Retain the existing `matric:school_locations` contract for other consumers. Switch this frontend to the effective layer. Do not publish editor identities, history, or private correction notes in public WFS properties. Add layer schemas and feature IDs to the publication tests.

Imports continue refreshing source data without changing editor profiles. Test this explicitly. Reverting to imported coordinates should be an explicit, audited action in a later version, not an incidental effect of an import.

Use EF migrations for application-owned tables and views. Update bootstrap to publish the new layers after migrations. Do not introduce a second competing table definition in `init.sql`. Add Npgsql/EF spatial support with NetTopologySuite if using mapped geometry properties; the current DbContext registration does not enable it.

## API and authorization

Add a dedicated SchoolSpatial feature following the existing Application, Infrastructure, and WebApi separation. Proposed routes, with the final prefix chosen to match existing controllers:

- `GET /api/schools/{emis}/spatial`: current boundary, effective point, point provenance, whether an override exists, and current revision. Use this as the authoritative edit read, not cached WFS.
- `PUT /api/schools/{emis}/spatial`: atomically save boundary and point override with the expected revision, reason, and source description.
- `GET /api/schools/{emis}/spatial/history`: editor-only revision history.

Define full-replacement request semantics explicitly. Distinguish "retain imported point" from "set an override". Do not use an ambiguous null to mean both keep and remove. For the first version, do not permit deleting an existing boundary or clearing an override through an ordinary save.

Use a proposed `schools.spatial.edit` permission for writes and history. Administrators grant it through an editor role using the existing role tools. Do not grant it to the default User role. Read access should follow the dashboard's existing visibility policy.

The API must enforce permissions even if someone bypasses the UI. Take actor and time from the server, never the request. Preserve the existing read-only proxy restrictions, enforce request-size and vertex limits, and use the established authenticated frontend-to-backend request path and CSRF protections appropriate to that path.

Return the canonical saved state and new revision. Use the project's Result and ProblemDetails conventions. Return 409 on a stale revision, including simultaneous first-time creation, and keep the local draft so the user can compare or reload. Optimistic concurrency is enough initially; real-time editing locks can wait.

## Geometry validation

- Use GeoJSON in EPSG:4326 at the API boundary, with longitude first. Explicitly transform to/from the OpenLayers EPSG:3857 view.
- Reject unsupported geometry types, nonfinite coordinates, out-of-range coordinates, empty geometry, unclosed rings, too few distinct vertices, self-intersections, and zero-area polygons. Support valid holes and MultiPolygon geometry server-side.
- Enforce server-side PostGIS/geometry-library validity checks. Client checks give quicker feedback but cannot authorize a save.
- Bound request bytes, total vertices, polygon components, and rings. Select numeric limits using real school examples before implementation.
- Warn about implausible area, large marker displacement, province mismatch, and overlap with another school. Do not automatically reject shared-campus overlaps or require grounds to match the existing district polygons, which are derived from points.
- Calculate area using geography or an appropriate projected CRS, never square degrees or uncorrected Web Mercator area.
- Reject invalid geometry with useful feedback rather than silently applying a repair that may change the editor's intended boundary.

## Imagery

Add a configurable background switcher with the current OSM layer as fallback. Reuse OpenLayers tile sources, not another mapping framework.

Provider selection remains open. Compare South African coverage, usable resolution, imagery age where available, tracing/derived-data rights, attribution, public deployment terms, quota, and cost. Obtain approval for a provider before integrating it. Do not use undocumented Google tile URLs.

Restrict browser-visible keys by origin and quota if the provider supports that model. Keep server-only credentials out of the frontend. Do not proxy or cache imagery unless the license permits it. Leave imagery out of dark-mode tile filters so editors can see the original image. Show a clear fallback when tiles fail.

## Implementation order

1. Finalize imagery provider, geometry limits, and whether an editor must supply a source/reason on every save. Recommended default: require a reason and identify imagery or field evidence used.
2. Add spatial persistence, migrations, atomic history, and import-protection tests. Verify migrations against existing spatial installations without changing current imported data.
3. Add the permission and authenticated API with geometry validation and optimistic concurrency. Regenerate frontend OpenAPI types.
4. Publish the effective point and school boundary layers. Keep old layers and proxy behavior working.
5. Extract map editing into a small Svelte component and TypeScript interaction helpers. Add Draw/Modify/Snap draft interactions, marker editing, Save/Cancel, error states, and imagery switching. Avoid growing `SchoolGeoMap.svelte` into GLF's large toolbar.
6. Add targeted cache invalidation, history display, and end-to-end tests. Update `docs/spatial-operations.md` with editor correction precedence, publication, backups, and recovery steps.
7. Pilot with a small editor group and a few known schools before enabling the permission broadly.

## Acceptance tests

- Anonymous users cannot write. A signed-in ordinary user receives 403. A granted editor can save only valid changes.
- Drawing a boundary, moving a marker, or adding a missing marker survives page refresh and application restart.
- Save and Cancel behave correctly; switching schools cannot attach a draft to the wrong EMIS.
- Failed saves retain the draft and do not change stored geometry or history. Repeated Save clicks cannot duplicate a revision.
- Invalid geometry and oversized payloads fail server-side. Coordinate round trips place features in the same location.
- Two editors cannot silently overwrite each other. Concurrent first saves also produce a conflict rather than last-write-wins.
- Profile and history writes succeed or roll back together. History exposes no unauthorized editor metadata.
- Running both real-coordinate import and sample bootstrap leaves manual corrections effective and boundaries unchanged.
- Saved changes appear immediately and on return navigation, without stale session WFS data. Public layers omit private metadata.
- Existing dashboard selection, performance display, older WFS layer, and read-only proxy security tests still pass.
- Imagery attribution remains visible, unavailable imagery falls back, and ordinary map use still works without edit permission.

Run backend build/tests, frontend tests/lint/check, Python spatial regression tests, and browser editing tests during implementation. Results are recorded in the [implementation session](../sessions/2026-10-19-school-map-editing.md). Verification uses disposable infrastructure rather than changing the running application database or GeoServer.

## Deferred scope

District/catchment assignment, split/merge tools, parcel imports, bulk editing, shared-edge topology, approval queues, live collaboration locks, boundary deletion, and one-click historical restore are outside the first version. Retain revision snapshots now so a later restore can create a new audited revision rather than deleting history.
