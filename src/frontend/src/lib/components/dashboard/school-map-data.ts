import type { School } from '$lib/types/dashboard';

type SchoolFeature = { properties?: Record<string, unknown>; [key: string]: unknown };

/** Boundary polygons and incomplete features must never open a school popup. */
export function schoolMapPopup(properties: Record<string, unknown>) {
	const { emis_number: id, name, province, passed, wrote } = properties;
	if (
		typeof id !== 'string' ||
		!id.trim() ||
		typeof name !== 'string' ||
		typeof province !== 'string' ||
		typeof passed !== 'number' ||
		typeof wrote !== 'number' ||
		!Number.isFinite(passed) ||
		!Number.isFinite(wrote) ||
		passed < 0 ||
		wrote < passed
	)
		return null;
	return {
		id,
		name,
		province,
		passed,
		wrote,
		passRate: wrote > 0 ? Math.round((passed / wrote) * 1000) / 10 : 0
	};
}

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
