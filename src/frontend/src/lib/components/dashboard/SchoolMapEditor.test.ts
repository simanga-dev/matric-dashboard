// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { mount, tick, unmount } from 'svelte';
import type OlMap from 'ol/Map';
import type { SpatialBoundary, SpatialPoint, SpatialState } from '$lib/api/school-spatial-contract';
import SchoolMapEditor from './SchoolMapEditor.svelte';

const mocks = vi.hoisted(() => ({
	read: vi.fn(),
	save: vi.fn(),
	history: vi.fn(),
	navigate: vi.fn(),
	destroy: vi.fn(),
	status: vi.fn(),
	change: null as
		| null
		| ((
				boundary: SpatialBoundary | null,
				point: SpatialPoint | null,
				markerEdited: boolean,
				footprint: SpatialBoundary | null,
				footprintEdited: boolean
		  ) => void)
}));
vi.mock('$app/navigation', () => ({ beforeNavigate: mocks.navigate }));
vi.mock('$lib/api/school-spatial', () => ({
	fetchSchoolSpatial: mocks.read,
	saveSchoolSpatial: mocks.save,
	fetchSchoolSpatialHistory: mocks.history
}));
vi.mock('./school-map-editing', async (original) => ({
	...(await original<typeof import('./school-map-editing')>()),
	createSpatialInteractions: (_map: unknown, _state: unknown, changed: typeof mocks.change) => {
		mocks.change = changed;
		return { tool: vi.fn(), stop: vi.fn(), area: () => null, destroy: mocks.destroy };
	}
}));
const state: SpatialState = {
	emisNumber: '00123',
	revision: 0,
	location: null,
	boundary: null,
	footprint: null,
	footprintSource: 'missing',
	hasLocationOverride: false,
	locationSource: 'missing',
	areaSquareMetres: null,
	warnings: []
};
interface EditorCommands {
	selectTool: (tool: 'draw' | 'grounds' | 'footprint' | 'marker') => void;
	saveDraft: () => void;
	cancelEdit: () => void;
}
let component: ReturnType<typeof mount> & EditorCommands;
const close = vi.fn();
const saved = vi.fn();
const loaded = vi.fn();
async function evidence() {
	const reason = document.querySelector('textarea')!;
	reason.value = 'Verified school grounds';
	reason.dispatchEvent(new Event('input', { bubbles: true }));
	const source = document.querySelector('input')!;
	source.value = 'Field observation';
	source.dispatchEvent(new Event('input', { bubbles: true }));
	await tick();
}
beforeEach(async () => {
	vi.clearAllMocks();
	mocks.read.mockResolvedValue(structuredClone(state));
	mocks.history.mockResolvedValue([]);
	const target = document.createElement('div');
	document.body.append(target);
	component = mount(SchoolMapEditor, {
		target,
		props: {
			map: {} as OlMap,
			emis: '00123',
			name: 'Original school',
			onsaved: saved,
			onloaded: loaded,
			onclose: close,
			onstatus: mocks.status
		}
	}) as ReturnType<typeof mount> & EditorCommands;
	await vi.waitFor(() => expect(document.querySelector('textarea')).not.toBeNull());
});
afterEach(async () => {
	await unmount(component);
	document.body.replaceChildren();
});
describe('explicit editor save and cancel', () => {
	it('updates published state from the authoritative GET before Cancel can remove the draft', async () => {
		expect(loaded).toHaveBeenCalledWith(state);
		component.cancelEdit();
		await tick();
		expect(close).toHaveBeenCalled();
		expect(loaded.mock.invocationCallOrder[0]).toBeLessThan(close.mock.invocationCallOrder[0]!);
	});
	it('keeps imported marker mode on grounds-only save and prevents repeated submissions', async () => {
		let finish!: (value: SpatialState) => void;
		mocks.save.mockImplementation(
			() =>
				new Promise((resolve) => {
					finish = resolve;
				})
		);
		mocks.change?.(
			{
				type: 'Polygon',
				coordinates: [
					[
						[28, -26],
						[28.01, -26],
						[28.01, -26.01],
						[28, -26]
					]
				]
			},
			null,
			false,
			null,
			false
		);
		await evidence();
		component.saveDraft();
		await tick();
		expect(mocks.save).toHaveBeenCalledWith(
			'00123',
			expect.objectContaining({ locationMode: 'imported', location: null, expectedRevision: 0 })
		);
		component.saveDraft();
		await tick();
		expect(mocks.save).toHaveBeenCalledTimes(1);
		finish({ ...state, revision: 1 });
		await vi.waitFor(() => expect(close).toHaveBeenCalledTimes(1));
		expect(saved).toHaveBeenCalledWith(expect.objectContaining({ revision: 1 }));
	});
	it('retains draft and evidence after conflict and permits retry', async () => {
		mocks.save.mockRejectedValue(new Error('Revision conflict. Your draft is retained.'));
		mocks.change?.(null, { type: 'Point', coordinates: [28, -26] }, true, null, false);
		await evidence();
		component.saveDraft();
		await vi.waitFor(() =>
			expect(document.querySelector('[role="alert"]')?.textContent).toContain('draft is retained')
		);
		expect(document.querySelector('textarea')?.value).toBe('Verified school grounds');
		expect(close).not.toHaveBeenCalled();
		expect(saved).not.toHaveBeenCalled();
		expect(mocks.save).toHaveBeenCalledWith(
			'00123',
			expect.objectContaining({
				locationMode: 'override',
				location: { type: 'Point', coordinates: [28, -26] }
			})
		);
	});
	it('warns on browser unload, cancels in-app navigation, and does not discard on declined Cancel', async () => {
		await evidence();
		const event = new Event('beforeunload', { cancelable: true });
		window.dispatchEvent(event);
		expect(event.defaultPrevented).toBe(true);
		vi.spyOn(window, 'confirm').mockReturnValue(false);
		const cancel = vi.fn();
		mocks.navigate.mock.calls[0]![0]({ willUnload: false, cancel });
		expect(cancel).toHaveBeenCalled();
		component.cancelEdit();
		await tick();
		expect(close).not.toHaveBeenCalled();
		vi.spyOn(window, 'confirm').mockReturnValue(true);
		component.cancelEdit();
		await tick();
		expect(close).toHaveBeenCalled();
	});
	it('reports toolbar state for drawing, marker, dirty, save, and validation controls', async () => {
		expect(mocks.status).toHaveBeenLastCalledWith(
			expect.objectContaining({
				activeTool: null,
				hasDraft: true,
				hasBoundary: false,
				hasMarker: false,
				dirty: false,
				saving: false,
				validationCount: 2
			})
		);
		component.selectTool('draw');
		await tick();
		expect(mocks.status).toHaveBeenLastCalledWith(expect.objectContaining({ activeTool: 'draw' }));
		mocks.change?.(
			{
				type: 'Polygon',
				coordinates: [
					[
						[28, -26],
						[28.01, -26],
						[28.01, -26.01],
						[28, -26]
					]
				]
			},
			null,
			false,
			null,
			false
		);
		await tick();
		expect(mocks.status).toHaveBeenLastCalledWith(
			expect.objectContaining({ hasBoundary: true, dirty: true, validationCount: 2 })
		);
	});
});
