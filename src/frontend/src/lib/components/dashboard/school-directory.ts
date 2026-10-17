/**
 * Shared school-directory loader for the overview page load.
 *
 * A universal (`+page.ts`) load so the directory is fetched on the server for
 * the first paint and reused from memory during client-side navigation. The
 * module-level cache also acts as an in-process server cache across SSR
 * requests. Data is public and changes at most once a year, so a short TTL is
 * enough. Failures are not cached, so the next load retries.
 */

import { createApiClient } from '$lib/api/client';
import { fetchSchools } from '$lib/api/dashboard';
import type { School } from '$lib/types/dashboard';
import { loadSchoolHierarchy } from './school-hierarchy';

type Fetch = typeof globalThis.fetch;

const DIRECTORY_TTL_MS = 5 * 60 * 1000;

let cache: { schools: School[]; expiresAt: number } | null = null;
let inflight: Promise<School[]> | null = null;

/** Full school directory, paged and cached in-process. */
export function loadSchoolDirectory(fetch: Fetch, origin: string): Promise<School[]> {
	if (cache && cache.expiresAt > Date.now()) {
		return Promise.resolve(cache.schools);
	}
	if (!inflight) {
		const client = createApiClient(fetch, origin);
		inflight = loadSchoolHierarchy((page, size) => fetchSchools(page, size, undefined, client))
			.then((schools) => {
				cache = { schools, expiresAt: Date.now() + DIRECTORY_TTL_MS };
				return schools;
			})
			.finally(() => {
				inflight = null;
			});
	}
	return inflight;
}
