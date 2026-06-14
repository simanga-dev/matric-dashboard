import { browserClient } from './client';
import type { DashboardStats, PassRateTrend, SchoolList } from '$lib/types/dashboard';

export async function fetchDashboardStats(): Promise<DashboardStats> {
	const { data, error } = await browserClient.GET('/api/v1/dashboard/stats');
	if (error) throw new Error('Failed to fetch dashboard stats');
	return data as unknown as DashboardStats;
}

export async function fetchPassRateTrends(years?: string): Promise<PassRateTrend[]> {
	const { data, error } = await browserClient.GET('/api/v1/dashboard/pass-rate-trends', {
		params: { query: years ? ({ years } as never) : undefined }
	});
	if (error) throw new Error('Failed to fetch pass rate trends');
	return data as unknown as PassRateTrend[];
}

export async function fetchSchools(
	pageNumber = 1,
	pageSize = 10,
	search?: string
): Promise<SchoolList> {
	const params: Record<string, unknown> = { pageNumber, pageSize };
	if (search) params.search = search;
	const { data, error } = await browserClient.GET('/api/v1/dashboard/schools', {
		params: { query: params } as never
	});
	if (error) throw new Error('Failed to fetch schools');
	return data as unknown as SchoolList;
}
