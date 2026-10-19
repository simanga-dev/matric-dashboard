# School map editing

## Scope

Implement the [school map editing plan](../architecture/school-map-editing-plan.md).
Approved editors can trace physical school grounds and independently correct
school markers. Imagery is configurable, with no provider selected by default.

## Persistence and API

- PostGIS profiles retain manual corrections separately from imported locations.
- EF migration `20261019093817_AddSchoolSpatialProfiles` owns profiles, immutable
  before/after history, spatial checks, and safe publication views. It provisions
  the source table only if absent and preserves it on rollback.
- Public `GET /api/v1/schools/{emis}/spatial` returns canonical GeoJSON and revision.
- PUT and history require `schools.spatial.edit`. No default User or Admin grant.
- Parent-row locking and expected revisions protect both first-save and later
  edit races. Profile and history writes share one transaction.
- Topology validation and area/distance calculations run in PostGIS. SQL-owned
  geometry avoids adding spatial serializer or EF geometry dependencies.
- Archived schools do not cause overlap warnings. Integration coverage includes
  holes, detached parcels, invalid holes, rollback, and import precedence.

## Publication and operations

- Existing point layer `school_locations_effective` remains available. The new
  `school_footprints` polygon view and `school_boundaries` view expose no private
  correction metadata.
- GeoServer bootstrap assigns stable UUID keys through `geo.gt_pk_metadata`,
  handles empty spatial views, and refuses to overwrite another metadata setup.
  WFS JSON exceptions are rejected rather than mistaken for an empty layer.
- `PUBLIC_MAP_IMAGERY_URL` and `PUBLIC_MAP_IMAGERY_ATTRIBUTION` are optional runtime
  configuration. Compose forwards them; both are blank by default.
- [Spatial operations](../spatial-operations.md#school-grounds-and-editor-location-corrections)
  documents deployment order, correction precedence, history, imagery, and recovery.

## Frontend

- The map editor uses isolated OpenLayers drafts, explicit Save/Cancel, a fixed
  school identity during editing, navigation warnings, and protected history.
- The floating map toolbar holds the background switcher, fit/home controls, and
  every editing action: draw, edit grounds, add/move marker, save, and cancel.
  The panel below keeps evidence, warnings, area, revision, and history.
- Each school renders as an editable location-footprint polygon instead of a
  point icon. A 25 m circular footprint is generated when no correction exists.
  Editors can redraw the grounds, move the polygon, or reshape its vertices
  independently from the imported point.
- Reason and evidence fields start with editable defaults so a correction can be
  saved immediately after changing geometry. Editors can replace the text with
  more specific evidence.
- Escape acts like Cancel. It closes a clean editor immediately and asks before
  discarding unsaved geometry changes.
- If the corrected footprint layer is unavailable or empty, the map converts
  imported `school_locations` points into the same small polygon shape and says
  so. The fallback disappears once the corrected layer is published.
- Marker changes are independent of grounds geometry. Errors and HTTP 409 retain
  the local draft rather than silently reverting or overwriting saved data.
- The browser client uses generated OpenAPI routes with runtime GeoJSON narrowing.
  Types were generated from the new backend specification, not hand-edited.
- Runtime imagery requires a valid HTTPS XYZ URL and attribution. Attribution is
  escaped, imagery is not dark-mode filtered, and tile failures fall back to OSM.
- Geometry placement and vertex editing remain pointer-based. Keyboard-only
  coordinate editing is a documented limitation of this first version.

## Verification

- Backend build passed. Release tests passed, 1,231 tests and 15 existing skips,
  including PostGIS integration tests in disposable containers.
- Frontend tests passed, 547 tests. Lint and type checks passed; the 11 reported
  Svelte warnings already existed in unrelated components. Production build passed.
- Browser editing and imagery regressions passed against a production build with
  fixture API routes. Editing coverage includes missing markers, drawing grounds,
  conflict draft retention, newer revisions before Cancel, remount cache refresh,
  and rendered published geometry.
- Python script regressions passed, 37 tests.
- Real-container publication smoke test passed, including empty grounds,
  UUID feature IDs, new geometry visibility, and repeat bootstrap/store credentials.
- Ruff checks and formatting passed for the new publication smoke test.
- Backend correctness, integration, and security reviews found no blocking issues.
  Review suggestions for stale map cache, archived-school warnings, complex
  geometry coverage, control sizing, and theme colours were fixed and rechecked.

Existing NuGet vulnerability warnings remain for MailKit, Microsoft.OpenApi,
OpenTelemetry packages, and System.Security.Cryptography.Pkcs. Aspire also reports
its existing CLI-bundle warning. Dependency upgrades are outside this feature.

Verification used mocks and disposable databases/containers. The local Aspire
development database applied the new migration while the API dev host restarted
during verification; it contains no correction profiles or history rows. No
production application database migration, data import, published production
layer change, or role assignment was performed. Pre-existing NSC-history and
subject-page work was left untouched.
