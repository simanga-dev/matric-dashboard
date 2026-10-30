import { browserClient } from './client';
import type { PastPaperFacets, PastPaperList, PastPaperQuery } from '$lib/types/past-papers';

type ApiClient = typeof browserClient;

/** Fetches one page of past papers matching the query. */
export async function fetchPastPapers(
	query: PastPaperQuery = {},
	client: ApiClient = browserClient
): Promise<PastPaperList> {
	const { data, error } = await client.GET('/api/v1/past-papers', { params: { query } });
	if (error || !data) throw new Error('Failed to fetch past papers');
	return data;
}

/** Fetches the subjects, years, sessions and languages available for filtering. */
export async function fetchPastPaperFacets(
	client: ApiClient = browserClient
): Promise<PastPaperFacets> {
	const { data, error } = await client.GET('/api/v1/past-papers/facets');
	if (error || !data) throw new Error('Failed to fetch past paper filters');
	return data;
}

/**
 * URL of a past paper file. `download` asks the API for an attachment
 * instead of inline display.
 */
export function pastPaperFileUrl(id: string, download = false): string {
	const path = `/api/v1/past-papers/${encodeURIComponent(id)}/file`;
	return download ? `${path}?download=true` : path;
}
