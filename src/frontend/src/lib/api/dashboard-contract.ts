import type {
	DashboardStats,
	PassRateTrend,
	RegionStats,
	RegionSubjects,
	SchoolDetail,
	SchoolList,
	SchoolSort,
	SchoolSortOrder,
	SchoolSubjects
} from '$lib/types/dashboard';
import type { components } from './v1';

type GetResponse<T> = {
	200: { content: { 'application/json': T } };
	default: { content: { 'application/problem+json': components['schemas']['ProblemDetails'] } };
};

/** Counts returned after republishing the school directory to Meilisearch. */
export interface SearchIndexRebuild {
	indexed: number;
	removed: number;
	durationMs: number;
}

/** Dashboard contracts supplement the generated spec until it includes these endpoints. */
export interface DashboardPaths {
	'/api/v1/dashboard/stats': {
		get: { responses: GetResponse<DashboardStats> };
	};
	'/api/v1/dashboard/pass-rate-trends': {
		get: {
			parameters: { query?: { years?: string } };
			responses: GetResponse<PassRateTrend[]>;
		};
	};
	'/api/v1/dashboard/schools': {
		get: {
			parameters: {
				query?: {
					pageNumber?: number;
					pageSize?: number;
					search?: string;
					sort?: SchoolSort;
					order?: SchoolSortOrder;
				};
			};
			responses: GetResponse<SchoolList>;
		};
	};
	'/api/v1/dashboard/schools/{id}': {
		get: {
			parameters: { path: { id: string } };
			responses: GetResponse<SchoolDetail>;
		};
	};
	'/api/v1/dashboard/schools/{id}/subjects': {
		get: {
			parameters: { path: { id: string }; query?: { year?: number } };
			responses: GetResponse<SchoolSubjects>;
		};
	};
	'/api/v1/dashboard/regions/stats': {
		get: {
			parameters: { query?: { province?: string; district?: string } };
			responses: GetResponse<RegionStats>;
		};
	};
	'/api/v1/dashboard/regions/subjects': {
		get: {
			parameters: { query?: { province?: string; district?: string; year?: number } };
			responses: GetResponse<RegionSubjects>;
		};
	};
	'/api/v1/search-index/schools': {
		post: {
			responses: GetResponse<SearchIndexRebuild>;
		};
	};
}
