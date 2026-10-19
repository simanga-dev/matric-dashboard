import { beforeEach, describe, expect, it, vi } from 'vitest';
import { invalidateGeoLayer, loadGeoJson } from './geo-data';
const url = (typeName: string) => `/api/geo/wfs?typeName=${typeName}`;
beforeEach(() => {
	for (const layer of [
		'matric:provinces',
		'matric:school_locations_effective',
		'matric:school_boundaries'
	])
		invalidateGeoLayer(layer);
});
describe('targeted published layer invalidation', () => {
	it('reloads effective points and grounds without evicting province cache', async () => {
		const transport = vi
			.fn()
			.mockImplementation(() =>
				Promise.resolve(Response.json({ type: 'FeatureCollection', features: [] }))
			);
		vi.stubGlobal('fetch', transport);
		const layers = [
			'matric:provinces',
			'matric:school_locations_effective',
			'matric:school_boundaries'
		];
		await Promise.all(layers.map((layer) => loadGeoJson(url(layer))));
		expect(transport).toHaveBeenCalledTimes(3);
		invalidateGeoLayer(layers[1]!);
		invalidateGeoLayer(layers[2]!);
		await Promise.all(layers.map((layer) => loadGeoJson(url(layer))));
		expect(transport).toHaveBeenCalledTimes(5);
		vi.unstubAllGlobals();
	});
	it('does not let an old failing request evict a replacement', async () => {
		let reject!: (reason: Error) => void;
		const transport = vi
			.fn()
			.mockImplementationOnce(
				() =>
					new Promise((_, fail) => {
						reject = fail;
					})
			)
			.mockResolvedValue(Response.json({ features: [] }));
		vi.stubGlobal('fetch', transport);
		const first = loadGeoJson(url('matric:school_boundaries'));
		invalidateGeoLayer('matric:school_boundaries');
		await loadGeoJson(url('matric:school_boundaries'));
		reject(new Error('Old request failed'));
		await expect(first).rejects.toThrow();
		await loadGeoJson(url('matric:school_boundaries'));
		expect(transport).toHaveBeenCalledTimes(2);
		vi.unstubAllGlobals();
	});
});
