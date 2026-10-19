import { describe, expect, it } from 'vitest';
import { schoolMapGeoJson, schoolMapPopup } from './school-map-data';
import type { School } from '$lib/types/dashboard';

const school: School = {
	id: '200500039',
	name: 'Current name',
	province: 'EASTERN CAPE',
	circuit: 'District',
	totalWrote: 358,
	totalPassed: 333,
	passRate: 93,
	totalAchieved: null
};
const geometry = { type: 'Point', coordinates: [28, -30] };
const oldFeature = {
	type: 'Feature',
	geometry,
	properties: { emis_number: school.id, wrote: 352, passed: 308, name: 'Old name' }
};

describe('school map results', () => {
	it('does not open a school popup for a province or district boundary', () => {
		expect(schoolMapPopup({ name: 'GAUTENG', code: 'GP' })).toBeNull();
		expect(schoolMapPopup({ name: 'District', province: 'GAUTENG' })).toBeNull();
	});

	it('requires a school identifier and finite counts before opening a popup', () => {
		const properties = {
			emis_number: school.id,
			name: school.name,
			province: school.province,
			wrote: 10,
			passed: 7
		};
		expect(schoolMapPopup(properties)).toMatchObject({
			id: school.id,
			passRate: 70,
			passed: 7,
			wrote: 10
		});
		expect(schoolMapPopup({ ...properties, emis_number: '' })).toBeNull();
		expect(schoolMapPopup({ ...properties, passed: NaN })).toBeNull();
		expect(schoolMapPopup({ ...properties, wrote: undefined })).toBeNull();
		expect(schoolMapPopup({ ...properties, wrote: 0, passed: 0 })?.passRate).toBe(0);
	});

	it('uses the same current cohort as the directory, retaining the real geometry', () => {
		const result = schoolMapGeoJson({ type: 'FeatureCollection', features: [oldFeature] }, [
			school
		]);
		expect(result.features[0]?.properties).toMatchObject({
			wrote: 358,
			passed: 333,
			name: 'Current name',
			province: school.province,
			district: school.circuit
		});
		expect(result.features[0]?.geometry).toBe(geometry);
		expect(oldFeature.properties).toMatchObject({ wrote: 352, passed: 308 });
	});

	it('never resurrects historical results for a school with no current cohort', () => {
		const result = schoolMapGeoJson({ features: [oldFeature] }, [
			{ ...school, totalWrote: 0, totalPassed: 0, passRate: 0 }
		]);
		expect(result.features[0]?.properties).toMatchObject({ wrote: 0, passed: 0 });
	});

	it('omits unmatched and sample points rather than displaying stale statistics', () => {
		const result = schoolMapGeoJson(
			{
				features: [
					oldFeature,
					{ ...oldFeature, properties: { emis_number: 'SAMPLE-001', wrote: 200 } }
				]
			},
			[school]
		);
		expect(result.features).toHaveLength(1);
		expect(result.totalFeatures).toBe(1);
	});
});
