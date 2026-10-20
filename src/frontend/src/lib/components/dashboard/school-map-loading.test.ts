import { describe, expect, it, vi } from 'vitest';
import { createViewportLoader, schoolLayerUrl, viewportLayerUrl } from './school-map-loading';

const collection = { type: 'FeatureCollection', features: [] };

describe('school map request size and viewport loading', () => {
	it('requests only identifiers and geometry for school layers', () => {
		const url = new URL(schoolLayerUrl('matric:school_locations_effective'), 'http://localhost');
		expect(url.searchParams.get('typeName')).toBe('matric:school_locations_effective');
		expect(url.searchParams.get('propertyName')).toBe('emis_number,geom');
	});
	it('uses WFS 1.0 longitude/latitude bbox ordering and bounded precision', () => {
		const url = new URL(
			viewportLayerUrl('matric:school_footprints', [28.123456789, -26, 29, -25]),
			'http://localhost'
		);
		expect(url.searchParams.get('bbox')).toBe(
			'28.123457,-26.000000,29.000000,-25.000000,EPSG:4326'
		);
	});
	it('does not download detailed polygons at the country zoom', async () => {
		const load = vi.fn();
		const apply = vi.fn();
		await createViewportLoader(load, apply).load([16, -35, 33, -22], 5);
		expect(load).not.toHaveBeenCalled();
		expect(apply).toHaveBeenCalledWith(collection, collection);
	});
	it('downloads footprints and grounds only for the zoomed viewport', async () => {
		const load = vi.fn().mockResolvedValue(collection);
		const apply = vi.fn();
		await createViewportLoader(load, apply).load([28, -26, 29, -25], 12);
		expect(load).toHaveBeenCalledTimes(2);
		for (const [url, signal] of load.mock.calls) {
			expect(new URL(url, 'http://localhost').searchParams.has('bbox')).toBe(true);
			expect(signal).toBeInstanceOf(AbortSignal);
		}
		expect(apply).toHaveBeenCalledWith(collection, collection);
	});
	it('aborts superseded requests and ignores late responses', async () => {
		const resolvers: Array<(value: Record<string, unknown>) => void> = [];
		const load = vi
			.fn()
			.mockImplementation(() => new Promise((resolve) => resolvers.push(resolve)));
		const apply = vi.fn();
		const loader = createViewportLoader(load, apply);
		const pending = loader.load([28, -26, 29, -25], 12);
		const firstSignal = load.mock.calls[0]![1] as AbortSignal;
		// Zooming out also supersedes a pending detailed request.
		await loader.load([16, -35, 33, -22], 5);
		expect(firstSignal.aborted).toBe(true);
		// Both requests can ignore abort at the transport layer; neither may apply late.
		for (const resolve of resolvers) resolve(collection);
		await pending;
		expect(apply).toHaveBeenCalledTimes(1);
	});
	it('does not apply any response after disposal', async () => {
		const resolvers: Array<(value: Record<string, unknown>) => void> = [];
		const load = vi
			.fn()
			.mockImplementation(() => new Promise((resolve) => resolvers.push(resolve)));
		const apply = vi.fn();
		const loader = createViewportLoader(load, apply);
		const pending = loader.load([28, -26, 29, -25], 12);
		loader.dispose();
		for (const resolve of resolvers) resolve(collection);
		await pending;
		expect(apply).not.toHaveBeenCalled();
		expect((load.mock.calls[0]![1] as AbortSignal).aborted).toBe(true);
	});
	it('keeps a successful layer when the other is unavailable', async () => {
		const load = vi
			.fn()
			.mockResolvedValueOnce(collection)
			.mockRejectedValueOnce(new Error('Unavailable'));
		const apply = vi.fn();
		await createViewportLoader(load, apply).load([28, -26, 29, -25], 12);
		expect(apply).toHaveBeenCalledWith(collection, null);
	});
});
