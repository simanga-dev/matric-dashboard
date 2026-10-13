import { describe, expect, it, vi } from 'vitest';
import type { School, SchoolList } from '$lib/types/dashboard';
import { circuitKey, groupSchools, loadSchoolHierarchy, passRateColor } from './school-hierarchy';

function school(id: string, name: string, province = 'Gauteng', circuit = 'Central'): School {
	return {
		id,
		name,
		province,
		circuit,
		totalWrote: 100,
		totalPassed: 80,
		passRate: 80,
		totalAchieved: null
	};
}

function page(items: School[], pageNumber: number, hasNextPage: boolean): SchoolList {
	return {
		items,
		pageNumber,
		hasNextPage,
		totalCount: 3,
		pageSize: 1000,
		totalPages: 2,
		hasPreviousPage: pageNumber > 1
	};
}

describe('school hierarchy', () => {
	it('keeps identical circuit names independent across provinces, including delimiter-like names', () => {
		expect(circuitKey('Gauteng', 'Central')).not.toBe(circuitKey('Limpopo', 'Central'));
		expect(circuitKey('a:b', 'c')).not.toBe(circuitKey('a', 'b:c'));
		const grouped = groupSchools([school('1', 'Alpha'), school('2', 'Beta', 'Limpopo')]);
		expect(
			grouped
				.get('Gauteng')
				?.get('Central')
				?.map((item) => item.id)
		).toEqual(['1']);
		expect(
			grouped
				.get('Limpopo')
				?.get('Central')
				?.map((item) => item.id)
		).toEqual(['2']);
	});

	it('finds names, EMIS numbers and entire matching areas without empty branches', () => {
		const schools = [school('100', 'Alpha High'), school('200', 'Beta High', 'Limpopo', 'North')];
		expect([...groupSchools(schools, '  ALPHA  ').keys()]).toEqual(['Gauteng']);
		expect(groupSchools(schools, '200').get('Limpopo')?.get('North')?.[0]?.name).toBe('Beta High');
		expect(groupSchools(schools, 'limpopo').get('Limpopo')?.get('North')).toHaveLength(1);
		expect(groupSchools(schools, 'north').get('Limpopo')?.get('North')).toHaveLength(1);
		expect(groupSchools(schools, 'missing').size).toBe(0);
	});

	it('sorts the directory without changing the API array', () => {
		const schools = [school('2', 'Zulu'), school('1', 'Alpha')];
		expect(
			groupSchools(schools)
				.get('Gauteng')
				?.get('Central')
				?.map((item) => item.name)
		).toEqual(['Alpha', 'Zulu']);
		expect(schools.map((item) => item.name)).toEqual(['Zulu', 'Alpha']);
	});

	it('loads later pages and deduplicates stable IDs', async () => {
		const fetchPage = vi
			.fn()
			.mockResolvedValueOnce(page([school('1', 'Alpha')], 1, true))
			.mockResolvedValueOnce(page([school('1', 'Alpha'), school('2', 'Beta')], 2, false));
		expect((await loadSchoolHierarchy(fetchPage)).map((item) => item.id)).toEqual(['1', '2']);
		expect(fetchPage.mock.calls).toEqual([
			[1, 1000],
			[2, 1000]
		]);
	});

	it('reports a later-page failure rather than presenting an incomplete directory', async () => {
		const fetchPage = vi
			.fn()
			.mockResolvedValueOnce(page([school('1', 'Alpha')], 1, true))
			.mockRejectedValueOnce(new Error('offline'));
		await expect(loadSchoolHierarchy(fetchPage)).rejects.toThrow('offline');
	});

	it('stops pagination after the browser is disposed', async () => {
		let cancelled = false;
		const fetchPage = vi.fn(async () => {
			cancelled = true;
			return page([school('1', 'Alpha')], 1, true);
		});
		await loadSchoolHierarchy(fetchPage, () => cancelled);
		expect(fetchPage).toHaveBeenCalledTimes(1);
	});

	it('rejects a broken empty continuation page instead of looping', async () => {
		const fetchPage = vi.fn().mockResolvedValue(page([], 1, true));
		await expect(loadSchoolHierarchy(fetchPage)).rejects.toThrow('empty page');
		expect(fetchPage).toHaveBeenCalledTimes(1);
	});
});

describe('pass rate legend', () => {
	it('maps a pass rate to its band colour at the boundaries', () => {
		expect(passRateColor(100)).toBe('#16a34a');
		expect(passRateColor(80)).toBe('#16a34a');
		expect(passRateColor(79.9)).toBe('#eab308');
		expect(passRateColor(60)).toBe('#eab308');
		expect(passRateColor(59.9)).toBe('#ef4444');
		expect(passRateColor(0)).toBe('#ef4444');
	});
});
