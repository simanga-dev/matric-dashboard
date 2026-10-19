import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createApiClient } from './client';
import {
	fetchSchoolSpatial,
	fetchSchoolSpatialHistory,
	saveSchoolSpatial,
	savedSpatialStates,
	refreshAuthoritativeSpatialStates,
	SpatialApiError
} from './school-spatial';
import type { SpatialState } from './school-spatial-contract';

const state: SpatialState = {
	emisNumber: '00123',
	revision: 2,
	boundary: null,
	location: null,
	hasLocationOverride: false,
	locationSource: 'missing',
	areaSquareMetres: null,
	warnings: []
};
const body = {
	expectedRevision: 1,
	boundary: null,
	location: null,
	locationMode: 'imported' as const,
	reason: 'Grounds',
	sourceDescription: 'Field observation'
};
beforeEach(() => savedSpatialStates.clear());
describe('school spatial API transport', () => {
	it('reconciles newer GETs and revalidates overlays on remount, including equal-revision imported point changes', async () => {
		const revisions = [
			{ ...state, revision: 1, location: { type: 'Point', coordinates: [28, -26] } },
			{ ...state, revision: 2, location: { type: 'Point', coordinates: [29, -27] } },
			{ ...state, revision: 3, location: { type: 'Point', coordinates: [30, -28] } },
			{ ...state, revision: 3, location: { type: 'Point', coordinates: [31, -29] } }
		];
		const client = createApiClient(
			vi.fn<typeof fetch>().mockImplementation(async () => Response.json(revisions.shift())),
			'https://frontend.test'
		);
		await saveSchoolSpatial('00123', body, client);
		expect((await fetchSchoolSpatial('00123', client)).revision).toBe(2);
		expect(savedSpatialStates.get('00123')?.revision).toBe(2);
		expect((await refreshAuthoritativeSpatialStates(client))[0]?.revision).toBe(3);
		expect((await refreshAuthoritativeSpatialStates(client))[0]?.location?.coordinates).toEqual([
			31, -29
		]);
		expect(savedSpatialStates.get('00123')?.location?.coordinates).toEqual([31, -29]);
	});
	it('never reapplies an unverified cached overlay after a failed authoritative refresh', async () => {
		savedSpatialStates.set('00123', { ...state, revision: 1 });
		const client = createApiClient(
			vi.fn<typeof fetch>().mockRejectedValue(new Error('Offline')),
			'https://frontend.test'
		);
		expect(await refreshAuthoritativeSpatialStates(client)).toEqual([]);
	});
	it('does not regress the cache when an older in-flight GET arrives late', async () => {
		savedSpatialStates.set('00123', { ...state, revision: 3 });
		const client = createApiClient(
			vi
				.fn<typeof fetch>()
				.mockImplementation(async () => Response.json({ ...state, revision: 2 })),
			'https://frontend.test'
		);
		expect((await fetchSchoolSpatial('00123', client)).revision).toBe(3);
	});
	it.each([
		{
			type: 'LineString',
			coordinates: [
				[28, -26],
				[29, -27]
			]
		},
		{ type: 'Point', coordinates: [181, -26] },
		{ type: 'Point', coordinates: ['28', '-26'] }
	])('narrows generated JsonElement geometry before exposing it to the map', async (location) => {
		const client = createApiClient(
			vi.fn<typeof fetch>().mockImplementation(async () => Response.json({ ...state, location })),
			'https://frontend.test'
		);
		await expect(fetchSchoolSpatial('00123', client)).rejects.toThrow('unsupported geometry');
	});
	it('turns network failures into a useful retained-draft error', async () => {
		const client = createApiClient(
			vi.fn<typeof fetch>().mockRejectedValue(new TypeError('Failed to fetch')),
			'https://frontend.test'
		);
		await expect(saveSchoolSpatial('00123', body, client)).rejects.toThrow(
			'draft has not been discarded'
		);
		expect(savedSpatialStates.size).toBe(0);
	});
	it('uses browser-client transport and middleware for mutation, preserving string EMIS', async () => {
		const transport = vi.fn<typeof fetch>().mockResolvedValue(Response.json(state));
		const onRequest = vi.fn(({ request }) => {
			request.headers.set('X-Test-Auth', 'middleware');
			return request;
		});
		const client = createApiClient(transport, 'https://frontend.test', [{ onRequest }]);
		expect(await saveSchoolSpatial('00123', body, client)).toEqual(state);
		const request = transport.mock.calls[0]![0] as Request;
		expect(request.url).toBe('https://frontend.test/api/v1/schools/00123/spatial');
		expect(request.method).toBe('PUT');
		expect(request.headers.get('X-Test-Auth')).toBe('middleware');
		expect(await request.json()).toEqual(body);
		expect(savedSpatialStates.get('00123')).toEqual(state);
	});
	it.each([400, 401, 403, 409, 503])(
		'retains draft and canonical cache on %i errors',
		async (status) => {
			const client = createApiClient(
				vi.fn<typeof fetch>().mockResolvedValue(Response.json({ detail: 'Rejected' }, { status })),
				'https://frontend.test'
			);
			const snapshot = structuredClone(body);
			await expect(saveSchoolSpatial('00123', body, client)).rejects.toMatchObject({ status });
			expect(body).toEqual(snapshot);
			expect(savedSpatialStates.size).toBe(0);
			if (status === 409)
				expect(new SpatialApiError(status).message).toContain('draft is retained');
		}
	);
	it('handles unavailable public geometry and protected history without clearing anything', async () => {
		const client = createApiClient(
			vi
				.fn<typeof fetch>()
				.mockImplementation(async () => Response.json({ detail: 'Unavailable' }, { status: 503 })),
			'https://frontend.test'
		);
		await expect(fetchSchoolSpatial('00123', client)).rejects.toMatchObject({ status: 503 });
		await expect(fetchSchoolSpatialHistory('00123', client)).rejects.toMatchObject({ status: 503 });
	});
});
