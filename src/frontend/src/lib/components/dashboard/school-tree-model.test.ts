import { describe, expect, it } from 'vitest';
import type { School } from '$lib/types/dashboard';
import { circuitKey } from './school-hierarchy';
import {
	filterSchoolDirectory,
	flattenSchoolTree,
	indexSchoolDirectory,
	normalizeSchoolSearch,
	schoolTreeWindow
} from './school-tree-model';

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

const schools = [
	school('2', 'Zulu'),
	school('1', 'École High'),
	school('3', 'Beta', 'Limpopo', 'North')
];

describe('indexed school filtering', () => {
	it('normalizes accents, decomposed characters, whitespace and case', () => {
		expect(normalizeSchoolSearch('  E\u0301COLE  ')).toBe('ecole');
	});

	it('finds names, EMIS, provinces and circuits without network requests', () => {
		const directory = indexSchoolDirectory(schools);
		for (const query of ['école', 'ECOLE', '1', 'gauteng ecole', 'central high']) {
			expect(filterSchoolDirectory(directory, query).map((s) => s.id)).toEqual(['1']);
		}
		expect(filterSchoolDirectory(directory, 'north').map((s) => s.id)).toEqual(['3']);
		expect(filterSchoolDirectory(directory, 'missing')).toEqual([]);
	});

	it('keeps directory order stable and leaves API data untouched', () => {
		const directory = indexSchoolDirectory(schools);
		expect(filterSchoolDirectory(directory, '').map((s) => s.name)).toEqual([
			'Beta',
			'École High',
			'Zulu'
		]);
		expect(schools.map((s) => s.id)).toEqual(['2', '1', '3']);
		expect(directory.byId.get('1')?.name).toBe('École High');
	});
});

describe('flattened hierarchy', () => {
	const directory = indexSchoolDirectory(schools);
	it('only includes open branches', () => {
		expect(flattenSchoolTree(directory.groups, false, new Set(), new Set())).toHaveLength(1);
		const collapsed = flattenSchoolTree(directory.groups, true, new Set(), new Set());
		expect(collapsed.map((row) => row.kind)).toEqual(['country', 'province', 'province']);
		const rows = flattenSchoolTree(
			directory.groups,
			true,
			new Set(['Gauteng']),
			new Set([circuitKey('Gauteng', 'Central')])
		);
		expect(rows.map((row) => row.kind)).toEqual([
			'country',
			'province',
			'circuit',
			'school',
			'school',
			'province'
		]);
	});

	it('preserves hierarchy and sibling metadata for virtualized accessibility', () => {
		const rows = flattenSchoolTree(directory.groups, false, new Set(), new Set(), true);
		const first = rows.find((row) => row.key === 'school:1');
		expect(first).toMatchObject({
			level: 4,
			position: 1,
			siblings: 2,
			parent: circuitKey('Gauteng', 'Central')
		});
		expect(new Set(rows.map((row) => row.key)).size).toBe(rows.length);
		expect(rows[0]).toMatchObject({ count: 3, expanded: true });
	});

	it('does not overwrite browse expansion state while searching', () => {
		const provinces = new Set<string>();
		const circuits = new Set<string>();
		expect(
			flattenSchoolTree(directory.groups, true, provinces, circuits, true).filter(
				(row) => row.kind === 'school'
			)
		).toHaveLength(3);
		expect(provinces.size).toBe(0);
		expect(circuits.size).toBe(0);
	});

	it('keeps duplicate circuit names in different provinces independent', () => {
		const groups = indexSchoolDirectory([school('1', 'One'), school('2', 'Two', 'Limpopo')]).groups;
		const rows = flattenSchoolTree(
			groups,
			true,
			new Set(['Gauteng', 'Limpopo']),
			new Set([circuitKey('Limpopo', 'Central')])
		);
		expect(rows.filter((row) => row.kind === 'school').map((row) => row.key)).toEqual(['school:2']);
	});
});

describe('virtual tree window', () => {
	it('bounds rendered rows independently of a 10,000-school directory', () => {
		expect(schoolTreeWindow(10_000, 0, 600, 36)).toEqual({
			start: 0,
			end: 23,
			totalHeight: 360_000
		});
		const middle = schoolTreeWindow(10_000, 180_000, 600, 36);
		expect(middle.end - middle.start).toBe(29);
		expect(middle.start).toBe(4994);
	});

	it('clamps stale scroll offsets after filtering or collapsing', () => {
		expect(schoolTreeWindow(3, 300_000, 600, 36)).toEqual({ start: 0, end: 3, totalHeight: 108 });
		expect(schoolTreeWindow(0, 500, 600, 36)).toEqual({ start: 0, end: 0, totalHeight: 0 });
	});

	it('supports touch row heights and negative browser overscroll', () => {
		expect(schoolTreeWindow(100, -100, 440, 44)).toEqual({ start: 0, end: 16, totalHeight: 4400 });
	});
});
