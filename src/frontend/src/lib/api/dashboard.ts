import { browserClient } from './client';
import type {
	DashboardStats,
	PassRateTrend,
	RegionStats,
	RegionSubjects,
	SchoolDetail,
	SchoolList,
	SchoolSubjects
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

export async function fetchSchools(
	pageNumber = 1,
	pageSize = 10,
	search?: string,
	client: ApiClient = browserClient
): Promise<SchoolList> {
	const { data, error } = await client.GET('/api/v1/dashboard/schools', {
		params: { query: { pageNumber, pageSize, search } }
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
