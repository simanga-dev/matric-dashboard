import type { School } from '$lib/types/dashboard';
import { circuitKey, groupSortedSchools, sortSchoolsByName } from './school-hierarchy';

type Groups = ReturnType<typeof groupSortedSchools>;

/** Normalize once per directory entry, not once per field on every keystroke. */
export function normalizeSchoolSearch(value: string): string {
	return value.normalize('NFD').replace(/\p{M}/gu, '').toLocaleLowerCase().trim();
}

export function indexSchoolDirectory(schools: School[]) {
	const sorted = sortSchoolsByName(schools);
	return {
		groups: groupSortedSchools(sorted),
		byId: new Map(sorted.map((school) => [String(school.id), school])),
		entries: sorted.map((school) => ({
			school,
			text: normalizeSchoolSearch(
				[school.name, school.id, school.province, school.circuit].join('\n')
			)
		}))
	};
}

export function filterSchoolDirectory(
	directory: ReturnType<typeof indexSchoolDirectory>,
	query: string
): School[] {
	const terms = normalizeSchoolSearch(query).split(/\s+/).filter(Boolean);
	return directory.entries
		.filter((entry) => terms.every((term) => entry.text.includes(term)))
		.map((entry) => entry.school);
}

interface RowPosition {
	key: string;
	parent: string | null;
	level: number;
	position: number;
	siblings: number;
}

export type SchoolTreeRow = RowPosition &
	(
		| { kind: 'country'; count: number; expanded: boolean }
		| { kind: 'province'; province: string; count: number; expanded: boolean }
		| { kind: 'circuit'; province: string; circuit: string; count: number; expanded: boolean }
		| { kind: 'school'; school: School }
	);

/** Flatten only open branches. The component renders a small window of this array. */
export function flattenSchoolTree(
	groups: Groups,
	rootOpen: boolean,
	provinces: Set<string>,
	circuits: Set<string>,
	searching = false
): SchoolTreeRow[] {
	let total = 0;
	for (const districts of groups.values()) {
		for (const schools of districts.values()) total += schools.length;
	}
	const rows: SchoolTreeRow[] = [
		{
			kind: 'country',
			key: 'country',
			parent: null,
			level: 1,
			position: 1,
			siblings: 1,
			count: total,
			expanded: rootOpen || searching
		}
	];
	if (!rootOpen && !searching) return rows;
	let provincePosition = 0;
	for (const [province, districts] of groups) {
		// Prefix the key so it can never collide with a circuit key.
		const provinceKey = `province:${JSON.stringify(province)}`;
		const expanded = searching || provinces.has(province);
		let count = 0;
		for (const schools of districts.values()) count += schools.length;
		rows.push({
			kind: 'province',
			key: provinceKey,
			parent: 'country',
			level: 2,
			position: ++provincePosition,
			siblings: groups.size,
			province,
			count,
			expanded
		});
		if (!expanded) continue;
		let circuitPosition = 0;
		for (const [circuit, schools] of districts) {
			const key = circuitKey(province, circuit);
			const expanded = searching || circuits.has(key);
			rows.push({
				kind: 'circuit',
				key,
				parent: provinceKey,
				level: 3,
				position: ++circuitPosition,
				siblings: districts.size,
				province,
				circuit,
				count: schools.length,
				expanded
			});
			if (!expanded) continue;
			for (const [index, school] of schools.entries()) {
				rows.push({
					kind: 'school',
					key: `school:${school.id}`,
					parent: key,
					level: 4,
					position: index + 1,
					siblings: schools.length,
					school
				});
			}
		}
	}
	return rows;
}

export function schoolTreeWindow(
	count: number,
	scrollTop: number,
	height: number,
	rowHeight: number,
	overscan = 6
) {
	const size = Math.max(1, rowHeight);
	const visible = Math.ceil(Math.max(0, height) / size);
	const first = Math.min(Math.max(0, count - visible), Math.floor(Math.max(0, scrollTop) / size));
	return {
		start: Math.max(0, first - overscan),
		end: Math.min(count, first + visible + overscan),
		totalHeight: count * size
	};
}
