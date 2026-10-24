/**
 * Query-string state for the school directory page, so a filtered, sorted and
 * paged view can be shared or bookmarked. Unknown or malformed values fall
 * back to defaults instead of being forwarded to the API.
 */

import type { SchoolSort, SchoolSortOrder } from '$lib/types/dashboard';

/** The directory view a URL describes. */
export interface SchoolsViewQuery {
	search: string;
	province: string | null;
	sort: SchoolSort | null;
	order: SchoolSortOrder | null;
	page: number;
}

const SORTS: readonly SchoolSort[] = [
	'name',
	'province',
	'totalWrote',
	'totalPassed',
	'passRate',
	'mapUpdatedAt'
];

function isSort(value: string | null): value is SchoolSort {
	return value !== null && (SORTS as readonly string[]).includes(value);
}

/** Reads the directory view from a query string. */
export function parseSchoolsQuery(params: URLSearchParams): SchoolsViewQuery {
	const rawSort = params.get('sort');
	const sort = isSort(rawSort) ? rawSort : null;
	const rawOrder = params.get('order');
	const order = sort && (rawOrder === 'asc' || rawOrder === 'desc') ? rawOrder : null;
	const rawPage = Number(params.get('page'));
	return {
		search: params.get('search') ?? '',
		province: params.get('province')?.trim() || null,
		sort,
		order: sort ? (order ?? 'asc') : null,
		page: Number.isInteger(rawPage) && rawPage > 0 ? rawPage : 1
	};
}

/** Serialises a directory view without the leading `?`; defaults are omitted. */
export function schoolsQueryString(query: SchoolsViewQuery): string {
	const params = new URLSearchParams();
	if (query.search) params.set('search', query.search);
	if (query.province) params.set('province', query.province);
	if (query.sort) {
		params.set('sort', query.sort);
		params.set('order', query.order ?? 'asc');
	}
	if (query.page > 1) params.set('page', String(query.page));
	return params.toString();
}
