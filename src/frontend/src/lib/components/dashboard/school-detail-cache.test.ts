import { describe, expect, it, vi } from 'vitest';
import type { SchoolDetail } from '$lib/types/dashboard';
import { createSchoolDetailCache } from './school-detail-cache';

function school(id: string): SchoolDetail {
	return {
		id,
		name: `School ${id}`,
		province: 'Gauteng',
		circuit: 'Central',
		centreNumber: id,
		quintile: 3,
		totalWrote: 100,
		totalPassed: 80,
		passRate: 80,
		years: [],
		comparison: {
			provinceRank: 1,
			provinceSchoolCount: 1,
			provinceAveragePassRate: 80,
			nationalAveragePassRate: 80
		},
		subjectsOfferedYear: null,
		subjectsOffered: []
	};
}

describe('school detail cache', () => {
	it('bounds speculative requests without blocking an explicit selection', async () => {
		const fetchSchool = vi.fn(() => new Promise<SchoolDetail | null>(() => {}));
		const cache = createSchoolDetailCache(fetchSchool);
		cache.prefetch('1');
		cache.prefetch('2');
		cache.prefetch('3');
		expect(fetchSchool).toHaveBeenCalledTimes(2);
		void cache.load('3');
		expect(fetchSchool).toHaveBeenCalledTimes(3);
	});

	it('reuses completed selections synchronously', async () => {
		const fetchSchool = vi.fn(async (id: string) => school(id));
		const cache = createSchoolDetailCache(fetchSchool);
		expect(cache.peek('1')).toBeNull();
		await cache.load('1');
		expect(cache.peek('1')?.id).toBe('1');
		await cache.load('1');
		expect(fetchSchool).toHaveBeenCalledTimes(1);
	});

	it('shares a hover prefetch with a selection already in flight', async () => {
		let finish!: (school: SchoolDetail) => void;
		const fetchSchool = vi.fn(
			() =>
				new Promise<SchoolDetail>((resolve) => {
					finish = resolve;
				})
		);
		const cache = createSchoolDetailCache(fetchSchool);
		const prefetch = cache.load('1');
		expect(cache.load('1')).toBe(prefetch);
		finish(school('1'));
		await prefetch;
		expect(fetchSchool).toHaveBeenCalledOnce();
	});

	it('expires old results rather than keeping stale details indefinitely', async () => {
		let now = 0;
		const fetchSchool = vi.fn(async (id: string) => school(id));
		const cache = createSchoolDetailCache(fetchSchool, () => now);
		await cache.load('1');
		now = 60_000;
		expect(cache.peek('1')).toBeNull();
		await cache.load('1');
		expect(fetchSchool).toHaveBeenCalledTimes(2);
	});

	it('evicts the least recently used result at capacity', async () => {
		const cache = createSchoolDetailCache(
			async (id) => school(id),
			() => 0,
			2
		);
		await cache.load('1');
		await cache.load('2');
		cache.peek('1');
		await cache.load('3');
		expect(cache.peek('1')?.id).toBe('1');
		expect(cache.peek('2')).toBeNull();
		expect(cache.peek('3')?.id).toBe('3');
	});

	it('allows retry after failed or missing results', async () => {
		const fetchSchool = vi
			.fn()
			.mockRejectedValueOnce(new Error('Offline'))
			.mockResolvedValueOnce(null)
			.mockResolvedValueOnce(school('1'));
		const cache = createSchoolDetailCache(fetchSchool);
		await expect(cache.load('1')).rejects.toThrow('Offline');
		expect(await cache.load('1')).toBeNull();
		expect((await cache.load('1'))?.id).toBe('1');
		expect(fetchSchool).toHaveBeenCalledTimes(3);
	});
});
