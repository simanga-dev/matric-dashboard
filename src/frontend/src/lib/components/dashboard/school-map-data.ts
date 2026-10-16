import type { School } from '$lib/types/dashboard';

type SchoolFeature = { properties?: Record<string, unknown>; [key: string]: unknown };

/** Use GeoServer for geometry and the directory's current results for every marker. */
export function schoolMapGeoJson(geojson: Record<string, unknown>, schools: readonly School[]) {
	const directory = new Map(schools.map((school) => [String(school.id), school]));
	const features = ((geojson.features ?? []) as SchoolFeature[]).flatMap(
		(feature): SchoolFeature[] => {
			const school = directory.get(String(feature.properties?.emis_number ?? ''));
			// Unmatched and sample points must not fall back to old performance data.
			if (!school) return [];
			return [
				{
					...feature,
					properties: {
						...feature.properties,
						emis_number: school.id,
						name: school.name,
						province: school.province,
						district: school.circuit,
						wrote: school.totalWrote,
						passed: school.totalPassed
					}
				}
			];
		}
	);
	return { ...geojson, features, totalFeatures: features.length };
}
