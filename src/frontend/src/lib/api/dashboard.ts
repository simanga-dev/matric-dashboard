import { browserClient } from './client';
import type {
	DashboardStats,
	PassRateTrend,
	RegionStats,
	RegionSubjects,
	SchoolDetail,
	SchoolList,
	SchoolSort,
	SchoolSortOrder,
	SchoolSubjects,
	SubjectDetail,
	SubjectSchoolList,
	SubjectSchoolSort
} from '$lib/types/dashboard';

type ApiClient = typeof browserClient;

export async function fetchDashboardStats(
	client: ApiClient = browserClient
): Promise<DashboardStats> {
	const { data, error } = await client.GET('/api/v1/dashboard/stats');
	if (error || !data) throw new Error('Failed to fetch dashboard stats');
	return data;
}

export async function fetchPassRateTrends(
	years?: string,
	client: ApiClient = browserClient
): Promise<PassRateTrend[]> {
	const { data, error } = await client.GET('/api/v1/dashboard/pass-rate-trends', {
		params: { query: { years } }
	});
	if (error || !data) throw new Error('Failed to fetch pass rate trends');
	return data;
}

/** Filters and ordering for the paginated school directory. */
export interface SchoolQuery {
	pageNumber?: number;
	pageSize?: number;
	search?: string;
	/** Exact province name as stored on the school, e.g. "GAUTENG". */
	province?: string;
	sort?: SchoolSort;
	order?: SchoolSortOrder;
}

export async function fetchSchools(
	query: SchoolQuery = {},
	client: ApiClient = browserClient,
	signal?: AbortSignal
): Promise<SchoolList> {
	const { data, error } = await client.GET('/api/v1/dashboard/schools', {
		params: { query },
		signal
	});
	if (error || !data) throw new Error('Failed to fetch schools');
	return data;
}

export async function fetchSchoolById(
	id: string,
	client: ApiClient = browserClient
): Promise<SchoolDetail | null> {
	const { data, error, response } = await client.GET('/api/v1/dashboard/schools/{id}', {
		params: { path: { id } }
	});
	if (response.status === 404) return null;
	if (error || !data) throw new Error('Failed to fetch school');
	return data;
}

export async function fetchSchoolSubjects(
	id: string,
	year?: number,
	client: ApiClient = browserClient
): Promise<SchoolSubjects> {
	const { data, error } = await client.GET('/api/v1/dashboard/schools/{id}/subjects', {
		params: { path: { id }, query: { year } }
	});
	if (error || !data) throw new Error('Failed to fetch school subjects');
	return data;
}

export async function fetchRegionStats(
	province?: string | null,
	district?: string | null,
	client: ApiClient = browserClient
): Promise<RegionStats> {
	const { data, error } = await client.GET('/api/v1/dashboard/regions/stats', {
		params: { query: { province: province ?? undefined, district: district ?? undefined } }
	});
	if (error || !data) throw new Error('Failed to fetch region stats');
	return data;
}

export async function fetchRegionSubjects(
	province?: string | null,
	district?: string | null,
	year?: number | null,
	client: ApiClient = browserClient
): Promise<RegionSubjects> {
	const { data, error } = await client.GET('/api/v1/dashboard/regions/subjects', {
		params: {
			query: {
				province: province ?? undefined,
				district: district ?? undefined,
				year: year ?? undefined
			}
		}
	});
	if (error || !data) throw new Error('Failed to fetch region subjects');
	return data;
}

/** Geographic scope and year for a subject detail request. Null or absent means unfiltered. */
export interface SubjectScope {
	province?: string | null;
	district?: string | null;
	year?: number | null;
}

function scopeQuery({ province, district, year }: SubjectScope) {
	return {
		province: province ?? undefined,
		district: district ?? undefined,
		year: year ?? undefined
	};
}

/** A subject's performance for a scope and year, or null when the subject code is unknown. */
export async function fetchSubjectDetail(
	code: string,
	scope: SubjectScope = {},
	client: ApiClient = browserClient
): Promise<SubjectDetail | null> {
	const { data, error, response } = await client.GET('/api/v1/dashboard/subjects/{code}', {
		params: { path: { code }, query: scopeQuery(scope) }
	});
	if (response.status === 404) return null;
	if (error || !data) throw new Error('Failed to fetch subject');
	return data;
}

/** Scope, search, paging, and server-side ordering for the schools that wrote a subject. */
export interface SubjectSchoolQuery extends SubjectScope {
	search?: string;
	pageNumber?: number;
	pageSize?: number;
	sort?: SubjectSchoolSort;
	order?: SchoolSortOrder;
}

/** Paginated schools that wrote a subject in the selected scope and year. */
export async function fetchSubjectSchools(
	code: string,
	query: SubjectSchoolQuery = {},
	client: ApiClient = browserClient,
	signal?: AbortSignal
): Promise<SubjectSchoolList> {
	const { search, pageNumber, pageSize, sort, order } = query;
	const { data, error } = await client.GET('/api/v1/dashboard/subjects/{code}/schools', {
		params: {
			path: { code },
			query: {
				...scopeQuery(query),
				search: search || undefined,
				pageNumber,
				pageSize,
				sort,
				order
			}
		},
		signal
	});
	if (error || !data) throw new Error('Failed to fetch subject schools');
	return data;
}
