import { browserClient } from './client';
import type { SpatialSave } from './school-spatial-contract';
import type { SpatialState } from './school-spatial-contract';
import { spatialStateResponse, spatialHistoryResponse } from './school-spatial-contract';

/** Latest authoritative states survive remounts. WFS has no revision to prove catch-up. */
export const savedSpatialStates = new Map<string, SpatialState>();

function rememberSpatialState(state: SpatialState): SpatialState {
	const latest = savedSpatialStates.get(state.emisNumber);
	if (latest && latest.revision > state.revision) return structuredClone(latest);
	savedSpatialStates.set(state.emisNumber, structuredClone(state));
	return state;
}

/** WFS has no revision. Revalidate cached schools before using local overlays on publication. */
export async function refreshAuthoritativeSpatialStates(
	client = browserClient
): Promise<SpatialState[]> {
	const states = await Promise.all(
		[...savedSpatialStates.keys()].map((emis) => fetchSchoolSpatial(emis, client).catch(() => null))
	);
	return states.filter((state): state is SpatialState => state !== null);
}

export class SpatialApiError extends Error {
	constructor(
		public status: number,
		detail?: string | null
	) {
		super(
			status === 409
				? 'Another editor saved this school. Your draft is retained. Cancel and reopen to load the latest revision.'
				: detail ||
						'School map service is unavailable. Your draft has not been discarded. Try again.'
		);
	}
}

function unavailable(): never {
	throw new SpatialApiError(0);
}

export async function fetchSchoolSpatial(emis: string, client = browserClient) {
	const { data, error, response } = await client
		.GET('/api/v1/schools/{emis}/spatial', {
			params: { path: { emis } }
		})
		.catch(unavailable);
	if (error || !data) throw new SpatialApiError(response.status, error?.detail);
	return rememberSpatialState(spatialStateResponse(data));
}

export async function saveSchoolSpatial(emis: string, body: SpatialSave, client = browserClient) {
	const { data, error, response } = await client
		.PUT('/api/v1/schools/{emis}/spatial', {
			params: { path: { emis } },
			body
		})
		.catch(unavailable);
	if (error || !data) throw new SpatialApiError(response.status, error?.detail);
	return rememberSpatialState(spatialStateResponse(data));
}

export async function fetchSchoolSpatialHistory(emis: string, client = browserClient) {
	const { data, error, response } = await client
		.GET('/api/v1/schools/{emis}/spatial/history', {
			params: { path: { emis } }
		})
		.catch(unavailable);
	if (error || !data) throw new SpatialApiError(response.status, error?.detail);
	return data.map(spatialHistoryResponse);
}
