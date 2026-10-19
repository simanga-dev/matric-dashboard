import { describe, expect, it, vi } from 'vitest';
import type { School, SchoolList } from '$lib/types/dashboard';
import {
	circuitKey,
	countSchools,
	groupSchools,
	loadSchoolHierarchy,
	passRateBand,
	passRateBarClass,
	passRateColor,
	passRateTextClass,
	resolvePassRateColor,
	titleCase
} from './school-hierarchy';

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
	it('maps a pass rate to its band at the boundaries', () => {
		expect(passRateBand(100)).toBe('high');
		expect(passRateBand(80)).toBe('high');
		expect(passRateBand(79.9)).toBe('medium');
		expect(passRateBand(60)).toBe('medium');
		expect(passRateBand(59.9)).toBe('low');
		expect(passRateBand(0)).toBe('low');
	});

	it('uses theme variables for DOM colours instead of fixed hex values', () => {
		expect(passRateColor(90)).toBe('hsl(var(--pass-rate-high))');
		expect(passRateColor(70)).toBe('hsl(var(--pass-rate-medium))');
		expect(passRateColor(40)).toBe('hsl(var(--pass-rate-low))');
	});

	it('returns matching text and bar classes for each band', () => {
		expect(passRateTextClass(90)).toBe('pass-rate-text-high');
		expect(passRateBarClass(70)).toBe('pass-rate-bar-medium');
		expect(passRateTextClass(40)).toBe('pass-rate-text-low');
	});

	it('resolves concrete colours for canvas and image contexts', () => {
		// Node has no document, so this is the SSR fallback used before the map mounts.
		expect(resolvePassRateColor(90)).toBe('#2563eb');
		expect(resolvePassRateColor(70)).toBe('#d97706');
		expect(resolvePassRateColor(40)).toBe('#dc2626');
	});
});

describe('titleCase cache', () => {
	it('title-cases and caches without changing the result', () => {
		expect(titleCase('AZALEA COMBINED SCHOOL')).toBe('Azalea Combined School');
		expect(titleCase('AZALEA COMBINED SCHOOL')).toBe('Azalea Combined School');
		expect(titleCase('')).toBe('');
	});
});

describe('countSchools', () => {
	it('totals schools across provinces and circuits', () => {
		const grouped = new Map([
			[
				'Gauteng',
				new Map([
					['Central', [school('1', 'Alpha'), school('2', 'Beta')]],
					['North', [school('3', 'Gamma')]]
				])
			],
			['Limpopo', new Map([['South', [school('4', 'Delta')]]])]
		]);
		expect(countSchools(grouped)).toBe(4);
		expect(countSchools(new Map())).toBe(0);
	});
});
