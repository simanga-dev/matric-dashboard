import type { SpatialBoundary, SpatialPoint, SpatialState } from '$lib/api/school-spatial-contract';

export type SchoolLocationFeatureRole = 'boundary' | 'footprint' | 'location';

interface SchoolLocationFeature {
	type: 'Feature';
	properties: { role: SchoolLocationFeatureRole };
	geometry: SpatialPoint | SpatialBoundary;
}

/**
 * A school only has a usable close-up when the location comes from a real
 * import or an editor correction. Sample seed points must never be presented
 * as verified school grounds.
 */
export function hasVerifiedLocation(spatial: SpatialState | null): boolean {
	return Boolean(
		spatial?.location && spatial.locationSource !== 'sample' && spatial.locationSource !== 'missing'
	);
}

/**
 * Converts authoritative spatial state into the geometry the detail hero
 * draws. Higher-level grounds come first so the marker paints on top.
 */
export function schoolLocationMapGeoJson(spatial: SpatialState | null) {
	const features: SchoolLocationFeature[] = [];
	if (!spatial || !hasVerifiedLocation(spatial)) {
		return { type: 'FeatureCollection' as const, features };
	}
	if (spatial.boundary) {
		features.push({
			type: 'Feature',
			properties: { role: 'boundary' },
			geometry: spatial.boundary
		});
	}
	if (spatial.footprint) {
		features.push({
			type: 'Feature',
			properties: { role: 'footprint' },
			geometry: spatial.footprint
		});
	}
	if (spatial.location) {
		features.push({
			type: 'Feature',
			properties: { role: 'location' },
			geometry: spatial.location
		});
	}
	return { type: 'FeatureCollection' as const, features };
}
