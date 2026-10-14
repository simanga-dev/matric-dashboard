import type { School, SchoolList } from '$lib/types/dashboard';

const HIGH_COLOR = '#16a34a';
const MEDIUM_COLOR = '#eab308';
const LOW_COLOR = '#ef4444';

export function passRateColor(rate: number): string {
	if (rate >= 80) return HIGH_COLOR;
	if (rate >= 60) return MEDIUM_COLOR;
	return LOW_COLOR;
}

export function circuitKey(province: string, circuit: string): string {
	return JSON.stringify([province, circuit]);
}

/** Split a circuit key back into its province and circuit, or null if malformed. */
export function circuitParts(key: string): [string, string] | null {
	try {
		const value: unknown = JSON.parse(key);
		if (Array.isArray(value) && typeof value[0] === 'string' && typeof value[1] === 'string') {
			return [value[0], value[1]];
		}
	} catch {
		// fall through to null
	}
	return null;
}

/** Returns a name-sorted copy, leaving the input untouched. */
export function sortSchoolsByName(schools: School[]): School[] {
	return [...schools].sort((a, b) => a.name.localeCompare(b.name));
}

export function groupSchools(schools: School[], search = '') {
	return groupSortedSchools(sortSchoolsByName(schools), search);
}

/**
 * Groups an already name-sorted directory. Callers that regroup on every
 * keystroke should sort once and reuse it instead of re-sorting thousands
 * of rows per filter edit.
 */
export function groupSortedSchools(schools: School[], search = '') {
	const query = search.trim().toLocaleLowerCase();
	const groups = new Map<string, Map<string, School[]>>();
	for (const school of schools) {
		if (
			query &&
			![school.name, String(school.id), school.province, school.circuit].some((value) =>
				value.toLocaleLowerCase().includes(query)
			)
		)
			continue;
		let circuits = groups.get(school.province);
		if (!circuits) groups.set(school.province, (circuits = new Map()));
		const items = circuits.get(school.circuit) ?? [];
		items.push(school);
		circuits.set(school.circuit, items);
	}
	return new Map(
		[...groups]
			.sort(([a], [b]) => a.localeCompare(b))
			.map(([province, circuits]) => [
				province,
				new Map([...circuits].sort(([a], [b]) => a.localeCompare(b)))
			])
	);
}

export async function loadSchoolHierarchy(
	fetchPage: (page: number, size: number) => Promise<SchoolList>,
	isCancelled: () => boolean = () => false
): Promise<School[]> {
	const schools = new Map<string, School>();
	let page = 1;
	while (!isCancelled()) {
		const result = await fetchPage(page, 1000);
		for (const school of result.items) schools.set(String(school.id), school);
		if (!result.hasNextPage) break;
		if (result.items.length === 0) throw new Error('School list returned an empty page.');
		page++;
	}
	return [...schools.values()];
}
