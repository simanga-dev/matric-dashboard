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
