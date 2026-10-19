// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from 'vitest';
import type OlMap from 'ol/Map';
import type VectorLayer from 'ol/layer/Vector';
import { spatialMapStyle } from './school-map-style';
import { createSpatialInteractions } from './school-map-editing';

afterEach(() => {
	document.documentElement.removeAttribute('style');
	document.documentElement.classList.remove('dark');
});
describe('semantic spatial canvas styles', () => {
	it('resolves draft and published colours from CSS tokens', () => {
		document.documentElement.style.setProperty('--primary', '190 70% 40%');
		document.documentElement.style.setProperty('--muted-foreground', '220 20% 60%');
		expect(spatialMapStyle(true).getStroke()?.getColor()).toBe('hsl(190 70% 40%)');
		expect(spatialMapStyle(false).getStroke()?.getColor()).toBe('hsl(220 20% 60%)');
	});
	it('restyles active draft layers when the theme changes', async () => {
		const layers: VectorLayer[] = [];
		const map = {
			addLayer: (layer: VectorLayer) => layers.push(layer),
			removeLayer: vi.fn(),
			getView: vi.fn()
		} as unknown as OlMap;
		const editing = createSpatialInteractions(
			map,
			{
				emisNumber: '1',
				revision: 0,
				boundary: null,
				footprint: null,
				footprintSource: 'missing',
				location: null,
				locationSource: 'missing',
				hasLocationOverride: false,
				areaSquareMetres: null,
				warnings: []
			},
			vi.fn()
		);
		document.documentElement.style.setProperty('--primary', '300 70% 40%');
		document.documentElement.classList.add('dark');
		await vi.waitFor(() => expect(layers[0]?.getStyle()).toEqual(spatialMapStyle(true)));
		editing.destroy();
	});
});
