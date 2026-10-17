import type { PageServerLoad } from './$types';
import { getDashboardStats, getPassRateTrends } from '$lib/server/dashboard';

export const load: PageServerLoad = async ({ fetch, url }) => {
	const [stats, trends] = await Promise.all([
		getDashboardStats(fetch, url.origin),
		getPassRateTrends(fetch, url.origin)
	]);

	return { stats, trends };
};
