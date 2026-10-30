import type { PastPaperFacets, PastPaperList, PastPaperQuery } from '$lib/types/past-papers';
import type { components } from './v1';

type GetResponse<T> = {
	200: { content: { 'application/json': T } };
	default: { content: { 'application/problem+json': components['schemas']['ProblemDetails'] } };
};

/** Past paper contracts supplement the generated spec until it includes these endpoints. */
export interface PastPaperPaths {
	'/api/v1/past-papers': {
		get: {
			parameters: { query?: PastPaperQuery };
			responses: GetResponse<PastPaperList>;
		};
	};
	'/api/v1/past-papers/facets': {
		get: { responses: GetResponse<PastPaperFacets> };
	};
}
