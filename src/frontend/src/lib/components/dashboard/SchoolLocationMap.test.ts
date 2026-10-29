// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { mount, unmount } from 'svelte';
import { toLonLat, fromLonLat } from 'ol/proj';
import type View from 'ol/View';
import SchoolLocationMap from './SchoolLocationMap.svelte';
import type { SpatialState } from '$lib/api/school-spatial-contract';

const mocks = vi.hoisted(() => ({ map: vi.fn() }));

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
		setTarget() {}
		updateSize() {}
		once(_event: string, listener: () => void) {
			queueMicrotask(listener);
		}
		on() {}
	}
}));

const spatial: SpatialState = {
	emisNumber: '200500039',
	boundary: null,
	footprint: null,
	location: { type: 'Point', coordinates: [28.0473, -26.2041] },
	hasLocationOverride: false,
	locationSource: 'imported',
	revision: 0,
	areaSquareMetres: null,
	warnings: [],
	footprintSource: 'missing'
};

let component: ReturnType<typeof mount> | undefined;

beforeEach(() => {
	vi.clearAllMocks();
	vi.stubGlobal(
		'ResizeObserver',
		class {
			observe() {}
			unobserve() {}
			disconnect() {}
		}
	);
	vi.stubGlobal('matchMedia', (query: string) => ({
		matches: false,
		media: query,
		onchange: null,
		addEventListener: () => {},
		removeEventListener: () => {},
		addListener: () => {},
		removeListener: () => {},
		dispatchEvent: () => false
	}));
});

afterEach(async () => {
	if (component) await unmount(component);
	component = undefined;
	document.body.replaceChildren();
	vi.unstubAllGlobals();
});

function mountMap(props: { spatial: SpatialState | null; schoolName: string }) {
	const target = document.createElement('div');
	document.body.append(target);
	component = mount(SchoolLocationMap, { target, props });
	return target;
}

describe('school location hero', () => {
	it('opens a close-up view centered on the verified school marker', async () => {
		const target = mountMap({ spatial, schoolName: 'Bokamoso Secondary' });
		await vi.waitFor(() => expect(mocks.map).toHaveBeenCalledTimes(1));

		const options = mocks.map.mock.calls[0]?.[0] as { view: View };
		const [longitude, latitude] = toLonLat(options.view.getCenter() ?? []);
		expect(longitude).toBeCloseTo(28.0473, 5);
		expect(latitude).toBeCloseTo(-26.2041, 5);
		expect(options.view.getZoom()).toBe(17);

		expect(target.querySelector('[role="region"]')?.getAttribute('aria-label')).toContain(
			'Bokamoso Secondary'
		);
	});

	it('pins the school to three quarters width when the detail card sits on the left', async () => {
		vi.stubGlobal('matchMedia', (query: string) => ({
			matches: query.includes('min-width: 1024px'),
			media: query,
			onchange: null,
			addEventListener: () => {},
			removeEventListener: () => {},
			addListener: () => {},
			removeListener: () => {},
			dispatchEvent: () => false
		}));

		mountMap({ spatial, schoolName: 'Bokamoso Secondary' });
		await vi.waitFor(() => expect(mocks.map).toHaveBeenCalledTimes(1));

		const options = mocks.map.mock.calls[0]?.[0] as { view: View };
		const [schoolX = 0] = fromLonLat([28.0473, -26.2041]);
		const [centerX = 0] = options.view.getCenter() ?? [];
		const resolution = options.view.getResolution() ?? 0;
		expect(schoolX - centerX).toBeCloseTo(250 * resolution, 1);
	});

	it('explains when a school has no verified location without loading a map', async () => {
		const target = mountMap({
			spatial: { ...spatial, locationSource: 'sample' },
			schoolName: 'Sample school'
		});
		await Promise.resolve();

		expect(target.textContent).toContain('No verified location for this school yet');
		expect(mocks.map).not.toHaveBeenCalled();
	});

	it('keeps the page usable when the spatial service is unavailable', async () => {
		const target = mountMap({
			spatial: null,
			schoolName: 'Unmapped school'
		});
		await Promise.resolve();

		expect(target.textContent).toContain('The school map is unavailable right now');
		expect(mocks.map).not.toHaveBeenCalled();
	});
});
