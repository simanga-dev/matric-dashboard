// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { mount, unmount } from 'svelte';
import SchoolGeoMap from './SchoolGeoMap.svelte';
import type View from 'ol/View';
import { toLonLat } from 'ol/proj';
import type { SpatialState } from '$lib/api/school-spatial-contract';

const mocks = vi.hoisted(() => ({
	load: vi.fn(),
	map: vi.fn(),
	target: vi.fn(),
	refresh: vi.fn(),
	states: new Map<string, SpatialState>()
}));
vi.mock('$lib/state/geo-data', async (original) => ({
	...(await original<typeof import('$lib/state/geo-data')>()),
	loadGeoJson: mocks.load
}));
vi.mock('$app/state', () => ({
	page: {
		data: {
			user: { permissions: ['schools.spatial.edit'], roles: [] }
		}
	}
}));
vi.mock('$lib/api/school-spatial', () => ({
	savedSpatialStates: mocks.states,
	refreshAuthoritativeSpatialStates: mocks.refresh
}));
vi.mock('ol/Map', () => ({
	default: class {
		private view;
		constructor(options: { view: unknown }) {
			this.view = options.view;
			mocks.map(options);
		}
		getView() {
			return this.view;
		}
		getSize() {
			return [1000, 600];
		}
		setTarget = mocks.target;
		updateSize() {}
		addOverlay() {}
		on() {}
		once(_event: string, listener: () => void) {
			queueMicrotask(listener);
		}
	}
}));

let component: ReturnType<typeof mount> | undefined;
const school = {
	id: '00123',
	name: 'Test school',
	province: 'GAUTENG',
	circuit: 'Central',
	totalWrote: 20,
	totalPassed: 18,
	passRate: 90,
	totalAchieved: null
};
beforeEach(() => {
	vi.clearAllMocks();
	mocks.states.clear();
	mocks.refresh.mockResolvedValue([]);
	vi.stubGlobal(
		'ResizeObserver',
		class {
			observe() {}
			unobserve() {}
			disconnect() {}
		}
	);
	vi.stubGlobal('matchMedia', () => ({ matches: true }));
});
afterEach(async () => {
	if (component) await unmount(component);
	component = undefined;
	document.body.replaceChildren();
	vi.unstubAllGlobals();
});

describe('progressive school map startup', () => {
	it('renders while optional boundaries are pending and avoids country-wide footprint requests', async () => {
		mocks.load.mockImplementation((url: string) => {
			if (url.includes('school_locations_effective'))
				return Promise.resolve({
					type: 'FeatureCollection',
					features: [
						{
							type: 'Feature',
							properties: { emis_number: '00123' },
							geometry: { type: 'Point', coordinates: [28, -26] }
						}
					]
				});
			return new Promise(() => {});
		});
		const target = document.createElement('div');
		document.body.append(target);
		component = mount(SchoolGeoMap, { target, props: { schools: [school] } });
		await vi.waitFor(() => expect(mocks.map).toHaveBeenCalledTimes(1));
		await vi.waitFor(() => expect(target.textContent).not.toContain('Loading school map'));
		expect(mocks.load.mock.calls.some(([url]) => url.includes('matric:provinces'))).toBe(true);
		expect(mocks.load.mock.calls.some(([url]) => url.includes('matric:districts'))).toBe(true);
		expect(mocks.load.mock.calls.some(([url]) => url.includes('matric:school_footprints'))).toBe(
			false
		);
		expect(mocks.load.mock.calls.some(([url]) => url.includes('matric:school_boundaries'))).toBe(
			false
		);
	});
	it('opens the map editor when E is pressed with a school selected', async () => {
		mocks.load.mockImplementation((url: string) =>
			Promise.resolve({
				type: 'FeatureCollection',
				features: url.includes('school_locations_effective')
					? [
							{
								type: 'Feature',
								properties: { emis_number: '00123' },
								geometry: { type: 'Point', coordinates: [28, -26] }
							}
						]
					: []
			})
		);
		const target = document.createElement('div');
		document.body.append(target);
		component = mount(SchoolGeoMap, {
			target,
			props: { schools: [school], selectedSchoolId: '00123' }
		});
		await vi.waitFor(() => expect(mocks.map).toHaveBeenCalledTimes(1));
		await vi.waitFor(() => expect(target.textContent).not.toContain('Loading school map'));
		expect(target.textContent).not.toContain('Map layers');
		window.dispatchEvent(new KeyboardEvent('keydown', { key: 'e', bubbles: true }));
		await vi.waitFor(() => expect(target.textContent).toContain('Map layers'));
	});
	it('keeps a selected school in focus when optional boundaries arrive later', async () => {
		let resolveProvince!: (value: Record<string, unknown>) => void;
		mocks.load.mockImplementation((url: string) => {
			if (url.includes('school_locations_effective'))
				return Promise.resolve({
					type: 'FeatureCollection',
					features: [
						{
							type: 'Feature',
							properties: { emis_number: '00123' },
							geometry: { type: 'Point', coordinates: [28, -26] }
						}
					]
				});
			if (url.includes('matric:provinces'))
				return new Promise((resolve) => {
					resolveProvince = resolve;
				});
			return Promise.resolve({ type: 'FeatureCollection', features: [] });
		});
		const target = document.createElement('div');
		document.body.append(target);
		component = mount(SchoolGeoMap, {
			target,
			props: { schools: [school], selectedSchoolId: '00123' }
		});
		await vi.waitFor(() => expect(mocks.map).toHaveBeenCalledTimes(1));
		await vi.waitFor(() => expect(resolveProvince).toBeDefined());
		const view = mocks.map.mock.calls[0]![0].view as View;
		const center = [...view.getCenter()!];
		const zoom = view.getZoom();
		resolveProvince({
			type: 'FeatureCollection',
			features: [
				{
					type: 'Feature',
					properties: { name: 'OTHER PROVINCE' },
					geometry: {
						type: 'Polygon',
						coordinates: [
							[
								[20, -30],
								[21, -30],
								[21, -31],
								[20, -30]
							]
						]
					}
				}
			]
		});
		await vi.waitFor(() => expect(target.textContent).not.toContain('Loading school map'));
		expect(view.getCenter()).toEqual(center);
		expect(view.getZoom()).toBe(zoom);
		expect(
			mocks.load.mock.calls.some(
				([url]) => url.includes('matric:school_footprints') && url.includes('&bbox=')
			)
		).toBe(true);
	});
	it('uses cached canonical coordinates for the initial selected-school focus', async () => {
		mocks.states.set('00123', {
			emisNumber: '00123',
			revision: 1,
			location: { type: 'Point', coordinates: [29, -27] },
			footprint: {
				type: 'MultiPolygon',
				coordinates: [
					[
						[
							[28.999, -27.001],
							[29.001, -27.001],
							[29.001, -26.999],
							[28.999, -26.999],
							[28.999, -27.001]
						]
					]
				]
			},
			boundary: null,
			footprintSource: 'editor',
			hasLocationOverride: true,
			locationSource: 'editor',
			areaSquareMetres: null,
			warnings: []
		});
		mocks.load.mockImplementation((url: string) =>
			Promise.resolve({
				type: 'FeatureCollection',
				features: url.includes('school_locations_effective')
					? [
							{
								type: 'Feature',
								properties: { emis_number: '00123' },
								geometry: { type: 'Point', coordinates: [28, -26] }
							}
						]
					: []
			})
		);
		const target = document.createElement('div');
		document.body.append(target);
		component = mount(SchoolGeoMap, {
			target,
			props: { schools: [school], selectedSchoolId: '00123' }
		});
		await vi.waitFor(() => expect(mocks.map).toHaveBeenCalledTimes(1));
		await vi.waitFor(() => expect(target.textContent).not.toContain('Loading school map'));
		const view = mocks.map.mock.calls[0]![0].view as View;
		const [longitude, latitude] = toLonLat(view.getCenter()!);
		expect(longitude).toBeCloseTo(29, 3);
		expect(latitude).toBeCloseTo(-27, 3);
	});
});
