import { createApiClient } from '$lib/api/client';
import { fetchPastPaperFacets, fetchPastPapers } from '$lib/api/past-papers';
import { parsePastPaperFilters, toPastPaperQuery } from '$lib/components/past-papers/papers-url';
import type { PastPaperFacets, PastPaperList } from '$lib/types/past-papers';
import type { PageLoad } from './$types';

/**
 * Loads one page of past papers for the filters in the query string, plus the
 * filter options. Query changes rerun this load, so the URL drives the view.
 * Failures render an in-page retry instead of an error page.
 */
export const load: PageLoad = async ({ fetch, url }) => {
	const filters = parsePastPaperFilters(url.searchParams);
	const client = createApiClient(fetch, url.origin);

	const [list, facets] = await Promise.allSettled([
		fetchPastPapers(toPastPaperQuery(filters), client),
		fetchPastPaperFacets(client)
	]);

	const papers: PastPaperList | null = list.status === 'fulfilled' ? list.value : null;
	const options: PastPaperFacets | null = facets.status === 'fulfilled' ? facets.value : null;

	return {
		filters,
		papers,
		facets: options,
		loadError: papers ? null : 'Failed to load past papers'
	};
};
