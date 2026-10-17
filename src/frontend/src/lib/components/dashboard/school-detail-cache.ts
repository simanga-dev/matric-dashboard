import { fetchSchoolById } from '$lib/api/dashboard';
import type { SchoolDetail } from '$lib/types/dashboard';

/** Small, short-lived browser cache shared by tree prefetch and the details panel. */
// Client-only: effects and event handlers populate it, so SSR requests never
// share entries between users.
export function createSchoolDetailCache(
	fetchSchool: (id: string) => Promise<SchoolDetail | null> = fetchSchoolById,
	now: () => number = Date.now,
	capacity = 40,
	ttl = 60_000
) {
	const cached = new Map<string, { school: SchoolDetail; expires: number }>();
	const pending = new Map<string, Promise<SchoolDetail | null>>();

	function peek(id: string) {
		const entry = cached.get(id);
		if (!entry) return null;
		cached.delete(id);
		if (entry.expires <= now()) return null;
		cached.set(id, entry);
		return entry.school;
	}

	function load(id: string): Promise<SchoolDetail | null> {
		const school = peek(id);
		if (school) return Promise.resolve(school);
		const inflight = pending.get(id);
		if (inflight) return inflight;
		const request = fetchSchool(id)
			.then((school) => {
				if (school) {
					cached.set(id, { school, expires: now() + ttl });
					if (cached.size > capacity) {
						const oldest = cached.keys().next().value;
						if (oldest !== undefined) cached.delete(oldest);
					}
				}
				return school;
			})
			.finally(() => pending.delete(id));
		pending.set(id, request);
		return request;
	}

	function prefetch(id: string) {
		// Speculative requests must not flood the API when the pointer crosses rows.
		if (pending.size >= 2 || peek(id)) return;
		void load(id).catch(() => {});
	}

	return { peek, load, prefetch };
}

export const schoolDetailCache = createSchoolDetailCache();
