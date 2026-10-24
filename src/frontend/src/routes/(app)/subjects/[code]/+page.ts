import { error } from '@sveltejs/kit';
import { createApiClient } from '$lib/api/client';
import { fetchSubjectDetail } from '$lib/api/dashboard';
import {
	parseSubjectScope,
	SUBJECT_DETAIL_DEPENDENCY
} from '$lib/components/dashboard/subject-url';
import type { SubjectDetail } from '$lib/types/dashboard';
import type { PageLoad } from './$types';

/**
 * Loads one subject for the scope in the query string. Query changes rerun
 * this load, so the URL stays the single source of truth for the view.
 * An unknown subject is a 404; other failures render an in-page retry.
 */
export const load: PageLoad = async ({ fetch, url, params, depends }) => {
	depends(SUBJECT_DETAIL_DEPENDENCY);
	const scope = parseSubjectScope(url.searchParams);
	let detail: SubjectDetail | null = null;
	let loadError: string | null = null;
	try {
		detail = await fetchSubjectDetail(params.code, scope, createApiClient(fetch, url.origin));
	} catch {
		loadError = 'Failed to load subject results';
	}
	if (!detail && !loadError) error(404, 'Subject not found');
	return { code: params.code, scope, detail, loadError };
};
