/**
 * Server-only dashboard reads used by `+page.server.ts` load functions.
 *
 * Dashboard endpoints are public (`[AllowAnonymous]`), so these helpers do not
 * wire auth middleware. They reuse the shared request builders from
 * `$lib/api/dashboard`, passing a client bound to SvelteKit's `fetch` against
 * the app origin (which routes through the `/api/[...path]` proxy).
 *
 * The heavy school directory is intentionally not here - it is served by a
 * universal load (`school-directory.ts`) so it is cached on both sides.
 */

import { createApiClient } from '$lib/api/client';
import { fetchDashboardStats, fetchPassRateTrends, fetchSchoolById } from '$lib/api/dashboard';
import { spatialStateResponse } from '$lib/api/school-spatial-contract';
import type { SpatialState } from '$lib/api/school-spatial-contract';
import type { DashboardStats, PassRateTrend, SchoolDetail } from '$lib/types/dashboard';

type Fetch = typeof globalThis.fetch;

const api = (fetch: Fetch, origin: string) => createApiClient(fetch, origin);

/** Aggregated dashboard statistics (latest year and year-on-year trends). */
export function getDashboardStats(fetch: Fetch, origin: string): Promise<DashboardStats> {
	return fetchDashboardStats(api(fetch, origin));
}

/** Historical NSC pass-rate trend points. */
export function getPassRateTrends(fetch: Fetch, origin: string): Promise<PassRateTrend[]> {
	return fetchPassRateTrends(undefined, api(fetch, origin));
}

/** A single school by EMIS number, or null when it does not exist. */
export function getSchoolById(
	fetch: Fetch,
	origin: string,
	id: string
): Promise<SchoolDetail | null> {
	return fetchSchoolById(id, api(fetch, origin));
}

/**
 * Authoritative public map state for a school. The detail page treats the
 * map as progressive enhancement, so an unavailable spatial service returns
 * null rather than failing the whole page.
 */
export async function getSchoolSpatial(
	fetch: Fetch,
	origin: string,
	id: string
): Promise<SpatialState | null> {
	const response = await api(fetch, origin)
		.GET('/api/v1/schools/{emis}/spatial', { params: { path: { emis: id } } })
		.catch(() => null);
	if (!response || response.error || !response.data) return null;
	try {
		return spatialStateResponse(response.data);
	} catch {
		return null;
	}
}
