import { describe, expect, it } from 'vitest';
import type { SpatialState } from '$lib/api/school-spatial-contract';
import { hasVerifiedLocation, schoolLocationMapGeoJson } from './school-location-map';

const boundary = {
	type: 'Polygon' as const,
	coordinates: [
		[
			[28.1, -26.2],
			[28.101, -26.2],
			[28.101, -26.201],
			[28.1, -26.201],
			[28.1, -26.2]
		]
	]
};
const footprint = {
	type: 'Polygon' as const,
	coordinates: [
		[
			[28.1001, -26.2001],
			[28.1002, -26.2001],
			[28.1002, -26.2002],
			[28.1001, -26.2002],
			[28.1001, -26.2001]
		]
	]
};

const spatial: SpatialState = {
	emisNumber: '200500039',
	boundary,
	footprint,
	location: { type: 'Point', coordinates: [28.1, -26.2] },
	hasLocationOverride: false,
	locationSource: 'imported',
	revision: 0,
	areaSquareMetres: 1200,
	warnings: [],
	footprintSource: 'editor'
};

describe('school location hero geometry', () => {
	it('treats missing and sample locations as having no close-up view', () => {
		expect(hasVerifiedLocation(null)).toBe(false);
		expect(hasVerifiedLocation({ ...spatial, location: null })).toBe(false);
		expect(hasVerifiedLocation({ ...spatial, locationSource: 'missing' })).toBe(false);
		expect(hasVerifiedLocation({ ...spatial, locationSource: 'sample' })).toBe(false);
		expect(schoolLocationMapGeoJson({ ...spatial, locationSource: 'sample' }).features).toEqual([]);
	});

	it('draws grounds under the footprint and marker', () => {
		const result = schoolLocationMapGeoJson(spatial);
		expect(result.type).toBe('FeatureCollection');
		expect(result.features.map((feature) => feature.properties?.role)).toEqual([
			'boundary',
			'footprint',
			'location'
		]);
		expect(result.features[0]?.geometry).toBe(boundary);
		expect(result.features[1]?.geometry).toBe(footprint);
		expect(result.features[2]?.geometry).toEqual({
			type: 'Point',
			coordinates: [28.1, -26.2]
		});
	});

	it('keeps a verified marker even when no grounds have been drawn', () => {
		const result = schoolLocationMapGeoJson({ ...spatial, boundary: null, footprint: null });
		expect(result.features.map((feature) => feature.properties?.role)).toEqual(['location']);
	});
});
