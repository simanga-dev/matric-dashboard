import { fetchSchools } from '$lib/api/dashboard';
import type { SchoolList } from '$lib/types/dashboard';

type FetchPage = (
	page: number,
	size: number,
	query: string,
	signal: AbortSignal
) => Promise<SchoolList>;

/** Debounce typing and cancel obsolete requests, including callbacks from late responses. */
export function scheduleSchoolTreeSearch(
	query: string,
	page: number,
	onResult: (result: SchoolList) => void,
	onError: () => void,
	fetchPage: FetchPage = (page, size, query, signal) =>
		fetchSchools({ pageNumber: page, pageSize: size, search: query }, undefined, signal)
): () => void {
	const controller = new AbortController();
	const timer = setTimeout(
		async () => {
			try {
				const result = await fetchPage(page, 100, query.trim(), controller.signal);
				if (!controller.signal.aborted) onResult(result);
			} catch {
				if (!controller.signal.aborted) onError();
			}
		},
		page === 1 ? 150 : 0
	);

	return () => {
		clearTimeout(timer);
		controller.abort();
	};
}
