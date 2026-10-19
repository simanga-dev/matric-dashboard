import { describe, expect, it } from 'vitest';
import GeoJSON from 'ol/format/GeoJSON';
import VectorSource from 'ol/source/Vector';
import type { SpatialState } from '$lib/api/school-spatial-contract';
import { applyCanonicalSpatial } from './school-map-publication';

describe('canonical school map publication', () => {
	it('replaces stale coordinates and grounds immediately while retaining performance data and other schools', () => {
		const reader = new GeoJSON();
		const points = new VectorSource();
		const grounds = new VectorSource();
		const schools = [
			{
				id: '00123',
				name: 'School',
				province: 'GAUTENG',
				circuit: 'Central',
				totalWrote: 20,
				totalPassed: 18,
				passRate: 90,
				totalAchieved: null
			},
			{
				id: '00456',
				name: 'Other school',
				province: 'GAUTENG',
				circuit: 'Central',
				totalWrote: 10,
				totalPassed: 8,
				passRate: 80,
				totalAchieved: null
			}
		];
		const state: SpatialState = {
			emisNumber: '00123',
			revision: 1,
			location: { type: 'Point', coordinates: [28, -26] },
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
			hasLocationOverride: true,
			locationSource: 'editor',
			areaSquareMetres: 1000,
			warnings: []
		};
		applyCanonicalSpatial(reader, points, grounds, { ...state, emisNumber: '00456' }, schools);
		applyCanonicalSpatial(
			reader,
			points,
			grounds,
			{ ...state, location: { type: 'Point', coordinates: [29, -27] } },
			schools
		);
		// GeoServer may return old data after invalidation. The API state wins again.
		applyCanonicalSpatial(reader, points, grounds, state, schools);
		expect(points.getFeatures()).toHaveLength(2);
		expect(grounds.getFeatures()).toHaveLength(2);
		const selected = points
			.getFeatures()
			.find((feature) => feature.get('emis_number') === '00123')!;
		expect(selected.get('passed')).toBe(18);
		expect(selected.get('wrote')).toBe(20);
		const point = reader.writeGeometryObject(selected.getGeometry()!, {
			dataProjection: 'EPSG:4326',
			featureProjection: 'EPSG:3857'
		});
		if (point.type !== 'Point') throw new Error('Expected canonical point');
		expect(point.coordinates[0]).toBeCloseTo(28);
		expect(point.coordinates[1]).toBeCloseTo(-26);
	});
});
