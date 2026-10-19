import { describe, expect, it, vi } from 'vitest';
import type OlMap from 'ol/Map';
import Feature from 'ol/Feature';
import Point from 'ol/geom/Point';
import MultiPolygon from 'ol/geom/MultiPolygon';
import VectorLayer from 'ol/layer/Vector';
import { Draw, Modify, Snap, Translate } from 'ol/interaction';
import { DrawEvent } from 'ol/interaction/Draw';
import {
	readSpatialGeometry,
	writeSpatialGeometry,
	spatialDraft,
	validateSpatialDraft,
	createSpatialInteractions,
	spatialDraftWarnings
} from './school-map-editing';
import { selectionWhileEditing } from './school-map-selection';
import type { SpatialState } from '$lib/api/school-spatial-contract';

export const state: SpatialState = {
	emisNumber: '00123',
	revision: 4,
	boundary: {
		type: 'MultiPolygon',
		coordinates: [
			[
				[
					[28, -26],
					[28.01, -26],
					[28.01, -26.01],
					[28, -26]
				]
			]
		]
	},
	footprint: {
		type: 'MultiPolygon',
		coordinates: [
			[
				[
					[27.9998, -26.0002],
					[28.0002, -26.0002],
					[28.0002, -25.9998],
					[27.9998, -25.9998],
					[27.9998, -26.0002]
				]
			]
		]
	},
	footprintSource: 'default',
	location: { type: 'Point', coordinates: [28, -26] },
	hasLocationOverride: false,
	locationSource: 'imported',
	areaSquareMetres: 1000,
	warnings: []
};
describe('school map drafts', () => {
	it('preserves holes and detached parcels through grounds modification and full draft serialization', () => {
		const complex: SpatialState = {
			...state,
			boundary: {
				type: 'MultiPolygon',
				coordinates: [
					[
						[
							[28, -26],
							[28.02, -26],
							[28.02, -26.02],
							[28, -26.02],
							[28, -26]
						],
						[
							[28.005, -26.005],
							[28.005, -26.01],
							[28.01, -26.01],
							[28.01, -26.005],
							[28.005, -26.005]
						]
					],
					[
						[
							[28.03, -26],
							[28.04, -26],
							[28.04, -26.01],
							[28.03, -26.01],
							[28.03, -26]
						]
					]
				]
			}
		};
		const snapshot = structuredClone(complex);
		const interactions: (Draw | Modify | Snap | Translate)[] = [];
		const layers: VectorLayer[] = [];
		const map = {
			addLayer: (layer: VectorLayer) => layers.push(layer),
			removeLayer: vi.fn(),
			addInteraction: (interaction: Draw | Modify | Snap | Translate) =>
				interactions.push(interaction),
			removeInteraction: (interaction: Draw | Modify | Snap | Translate) =>
				interactions.splice(interactions.indexOf(interaction), 1),
			getView: () => ({ fit: vi.fn() })
		} as unknown as OlMap;
		const draft = spatialDraft(complex);
		const editing = createSpatialInteractions(
			map,
			complex,
			(boundary) => (draft.boundary = boundary)
		);
		editing.tool('grounds');
		const geometry = layers[0]!
			.getSource()!
			.getFeatures()
			.find((feature) => feature.getGeometry() instanceof MultiPolygon)!
			.getGeometry();
		if (!(geometry instanceof MultiPolygon)) throw new Error('Expected multipart grounds');
		const coordinates = geometry.getCoordinates();
		coordinates[0]![0]![1]![0]! += 1;
		geometry.setCoordinates(coordinates);
		interactions[0]!.dispatchEvent('modifyend');
		const saved = JSON.parse(JSON.stringify(draft)) as typeof draft;
		if (saved.boundary?.type !== 'MultiPolygon' || complex.boundary?.type !== 'MultiPolygon')
			throw new Error('Expected MultiPolygon');
		expect(saved.boundary.coordinates).toHaveLength(2);
		expect(saved.boundary.coordinates[0]).toHaveLength(2);
		for (const [polygon, rings] of complex.boundary.coordinates.entries())
			for (const [ring, positions] of rings.entries())
				for (const [vertex, position] of positions.entries())
					for (const [axis, value] of position.entries()) {
						if (polygon === 0 && ring === 0 && vertex === 1 && axis === 0) continue;
						expect(saved.boundary.coordinates[polygon]![ring]![vertex]![axis]).toBeCloseTo(
							value,
							8
						);
					}
		expect(saved.locationMode).toBe('imported');
		expect(saved.location).toBeNull();
		expect(complex).toEqual(snapshot);
		editing.destroy();
	});
	it('warns about outside markers and large displacement without rejecting the save', () => {
		const draft = spatialDraft(state);
		draft.locationMode = 'override';
		draft.location = { type: 'Point', coordinates: [29, -27] };
		expect(spatialDraftWarnings(draft, state)).toHaveLength(2);
	});
	it('keeps Draw Modify and Snap on detached features and cleans them up', async () => {
		const interactions: (Draw | Modify | Snap | Translate)[] = [];
		let layer: VectorLayer | null = null;
		const map = {
			addLayer: (value: VectorLayer) => {
				layer = value;
			},
			removeLayer: vi.fn(),
			addInteraction: (interaction: Draw | Modify | Snap | Translate) =>
				interactions.push(interaction),
			removeInteraction: (interaction: Draw | Modify | Snap | Translate) =>
				interactions.splice(interactions.indexOf(interaction), 1),
			getView: () => ({ fit: vi.fn() })
		} as unknown as OlMap;
		const original = structuredClone(state);
		const changed = vi.fn();
		const editing = createSpatialInteractions(map, original, changed);
		editing.tool('marker');
		expect(interactions[0]).toBeInstanceOf(Modify);
		expect(interactions[1]).toBeInstanceOf(Snap);
		interactions[0]!.dispatchEvent('modifyend');
		expect(changed).toHaveBeenLastCalledWith(
			original.boundary,
			expect.objectContaining({ type: 'Point' }),
			true,
			expect.objectContaining({ type: 'MultiPolygon' }),
			false
		);
		expect(original).toEqual(state);
		editing.destroy();
		expect(interactions).toHaveLength(0);
		expect(map.removeLayer).toHaveBeenCalledWith(layer);
		const toolChanged = vi.fn();
		const missing = createSpatialInteractions(
			map,
			{ ...state, boundary: null, location: null },
			changed,
			toolChanged
		);
		missing.tool('marker');
		const draw = interactions[0];
		if (!(draw instanceof Draw)) throw new Error('Missing marker must use Draw');
		draw.dispatchEvent(new DrawEvent('drawend', new Feature(new Point([0, 0]))));
		await Promise.resolve();
		expect(toolChanged).toHaveBeenLastCalledWith(null);
		expect(changed).toHaveBeenLastCalledWith(
			null,
			{ type: 'Point', coordinates: [0, 0] },
			true,
			expect.objectContaining({ type: 'MultiPolygon' }),
			false
		);
		missing.destroy();
	});
	it('exposes the location footprint as a drawable, movable and reshapeable polygon', async () => {
		const interactions: (Draw | Modify | Snap | Translate)[] = [];
		const map = {
			addLayer: vi.fn(),
			removeLayer: vi.fn(),
			addInteraction: (interaction: Draw | Modify | Snap | Translate) =>
				interactions.push(interaction),
			removeInteraction: (interaction: Draw | Modify | Snap | Translate) =>
				interactions.splice(interactions.indexOf(interaction), 1),
			getView: () => ({ fit: vi.fn() })
		} as unknown as OlMap;
		const draft = spatialDraft(state);
		const editing = createSpatialInteractions(
			map,
			state,
			(_boundary, _point, _marker, footprint, edited) => {
				if (edited && footprint) draft.footprint = footprint;
			}
		);
		editing.tool('footprint');
		expect(interactions[0]).toBeInstanceOf(Modify);
		expect(interactions[1]).toBeInstanceOf(Translate);
		interactions[1]!.dispatchEvent('translateend');
		expect(draft.footprint?.type).toBe('MultiPolygon');
		expect(draft.boundary).toEqual(state.boundary);
		editing.tool('draw-footprint');
		const draw = interactions[0];
		if (!(draw instanceof Draw)) throw new Error('Footprint drawing must use Draw');
		draw.dispatchEvent(
			new DrawEvent(
				'drawend',
				new Feature(
					new MultiPolygon([
						[
							[
								[0, 0],
								[1, 0],
								[1, 1],
								[0, 0]
							]
						]
					])
				)
			)
		);
		await Promise.resolve();
		expect(draft.footprint?.type).toBe('MultiPolygon');
		editing.destroy();
	});
	it('round trips EPSG:4326 longitude first through EPSG:3857', () => {
		const projected = readSpatialGeometry(state.location!);
		expect(projected.getExtent()[0]).toBeGreaterThan(1_000_000);
		const result = writeSpatialGeometry(projected);
		if (result.type !== 'Point') throw new Error('Expected point');
		expect(result.coordinates[0]).toBeCloseTo(28, 8);
		expect(result.coordinates[1]).toBeCloseTo(-26, 8);
		const polygon = writeSpatialGeometry(readSpatialGeometry(state.boundary!));
		expect(polygon.type).toBe('MultiPolygon');
	});
	it('isolates draft coordinates and retains imported mode unless marker explicitly changes', () => {
		const original = structuredClone(state);
		const draft = spatialDraft(original);
		expect(draft.locationMode).toBe('imported');
		expect(draft.location).toBeNull();
		expect(draft.footprint).toBeNull();
		if (draft.boundary?.type !== 'MultiPolygon') throw new Error('Expected MultiPolygon');
		draft.boundary.coordinates[0]![0]![0]![0] = 30;
		expect(original.boundary).toEqual(state.boundary);
		const override = spatialDraft({ ...state, hasLocationOverride: true });
		expect(override.locationMode).toBe('override');
		override.location!.coordinates[0] = 29;
		expect(state.location!.coordinates[0]).toBe(28);
	});
	it('freezes external selection, including deselection, while editor identity is captured', () => {
		expect(selectionWhileEditing('00123', '99999')).toBe('00123');
		expect(selectionWhileEditing('00123', null)).toBe('00123');
		expect(selectionWhileEditing(null, '99999')).toBe('99999');
		expect(selectionWhileEditing(null, null)).toBeNull();
	});
	it('requires evidence and reason and rejects invalid rings and out of range positions', () => {
		const draft = spatialDraft(state);
		expect(validateSpatialDraft(draft)).toEqual([]);
		draft.reason = '';
		draft.sourceDescription = '';
		expect(validateSpatialDraft(draft)).toHaveLength(2);
		draft.reason = 'Correction';
		draft.sourceDescription = 'Field survey';
		expect(validateSpatialDraft(draft)).toEqual([]);
		draft.locationMode = 'override';
		draft.location = { type: 'Point', coordinates: [181, -26] };
		expect(validateSpatialDraft(draft)).toContain('Place a valid school marker.');
		draft.boundary = {
			type: 'Polygon',
			coordinates: [
				[
					[28, -26],
					[29, -26],
					[29, -27]
				]
			]
		};
		expect(validateSpatialDraft(draft).join()).toContain('closed');
	});
});
