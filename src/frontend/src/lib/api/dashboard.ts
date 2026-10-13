import { browserClient } from './client';
import type { DashboardStats, PassRateTrend, SchoolDetail, SchoolList } from '$lib/types/dashboard';

export async function fetchDashboardStats(): Promise<DashboardStats> {
	const { data, error } = await browserClient.GET('/api/v1/dashboard/stats');
	if (error || !data) throw new Error('Failed to fetch dashboard stats');
	return data;
}

export async function fetchPassRateTrends(years?: string): Promise<PassRateTrend[]> {
	const { data, error } = await browserClient.GET('/api/v1/dashboard/pass-rate-trends', {
		params: { query: { years } }
	});
	if (error || !data) throw new Error('Failed to fetch pass rate trends');
	return data;
}

export async function fetchSchools(
	pageNumber = 1,
	pageSize = 10,
	search?: string
): Promise<SchoolList> {
	const { data, error } = await browserClient.GET('/api/v1/dashboard/schools', {
		params: { query: { pageNumber, pageSize, search } }
	});
	if (error || !data) throw new Error('Failed to fetch schools');
	return data;
}

export async function fetchSchoolById(id: string): Promise<SchoolDetail | null> {
	const { data, error, response } = await browserClient.GET('/api/v1/dashboard/schools/{id}', {
		params: { path: { id } }
	});
	if (response.status === 404) return null;
	if (error || !data) throw new Error('Failed to fetch school');
	return data;
}
