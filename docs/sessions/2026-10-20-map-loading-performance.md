# Map loading performance

## Changes

`d0a4139` replaces the country-wide footprint request with corrected school points and loads detailed footprints and grounds only within the visible bounding box at zoom 12 and above. Optional region outlines no longer block the first rendered map frame. OpenLayers imports and school data load concurrently, and canvas styles are reused.

## Files

- `SchoolGeoMap.svelte` coordinates progressive loading, selection, theme changes, and authoritative editor overlays.
- `school-map-loading.ts` builds compact WFS requests and aborts or ignores superseded viewport requests.
- `geo-data.ts` supports uncached, cancellable detail requests without growing the session cache.
- The frontend OGC proxy forwards cancellation while retaining its read-only path and header restrictions.
- Component, request, cache, and proxy tests cover the new behavior.
- `docs/spatial-operations.md` records loading behavior and benchmark limits; `FILEMAP.md` records related consumers.

## Decisions and measurements

Use existing point and polygon publications rather than changing database views or adding infrastructure. Keep the lightweight overview available while detail layers load. Cached editor saves must win over stale WFS responses and affect initial school selection.

With 6,872 schools, local GeoServer payload size fell from 4,620,155 bytes to 1,767,741 bytes. Median local Docker request time across three runs fell from 0.501 seconds to 0.179 seconds. These measure requests, not end-to-end production page load.

## Verification

- 574 frontend tests passed.
- Frontend lint and formatting passed.
- Svelte checking passed with zero errors and 11 existing warnings in unrelated files.
- Production frontend build passed.
- Real local GeoServer requests verified compact point fields and populated viewport footprint responses.

## Follow-up

Deploy and measure browser request timing on production. Recheck region selection, zoom/pan, school details, imagery, and authorized editing. No server upgrade or database migration is required. Unrelated uncommitted admin-bootstrap scripts and deployment documentation are excluded.
