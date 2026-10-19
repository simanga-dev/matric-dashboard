import type GeoJSON from 'ol/format/GeoJSON';
import type VectorSource from 'ol/source/Vector';
import type { SpatialState } from '$lib/api/school-spatial-contract';
import type { School } from '$lib/types/dashboard';
import { schoolMapGeoJson } from './school-map-data';

const projections = { dataProjection: 'EPSG:4326', featureProjection: 'EPSG:3857' };
type GeoJsonLayer = Record<string, unknown>;

function featureCount(layer: GeoJsonLayer | null): number {
	return Array.isArray(layer?.features) ? layer.features.length : 0;
}

/**
 * Reads corrected school points, falling back to the legacy imported layer
 * while corrected layers have not been published or contain no rows.
 */
export async function loadPublishedSchoolLocations(
	load: (url: string) => Promise<GeoJsonLayer>,
	effectiveUrl: string,
	legacyUrl: string
): Promise<{ geojson: GeoJsonLayer; fallback: boolean }> {
	const effective = await load(effectiveUrl).catch(() => null);
	if (effective && featureCount(effective) > 0) return { geojson: effective, fallback: false };
	const legacy = await load(legacyUrl).catch(() => null);
	if (legacy && featureCount(legacy) > 0) return { geojson: legacy, fallback: true };
	return {
		geojson: effective ?? legacy ?? { type: 'FeatureCollection', features: [] },
		fallback: false
	};
}

/** Apply the authoritative API result even when published WFS data is still stale. */
export function applyCanonicalSpatial(
	reader: GeoJSON,
	points: VectorSource,
	grounds: VectorSource,
	state: SpatialState,
	schools: School[]
): void {
	for (const source of [points, grounds]) {
		for (const feature of source.getFeatures()) {
			if (String(feature.get('emis_number')) === state.emisNumber) source.removeFeature(feature);
		}
	}
	const collection = (geometry: SpatialState['location'] | SpatialState['boundary']) => ({
		type: 'FeatureCollection',
		features: geometry
			? [
					{
						type: 'Feature',
						geometry,
						properties: { emis_number: state.emisNumber }
					}
				]
			: []
	});
	points.addFeatures(
		reader.readFeatures(schoolMapGeoJson(collection(state.location), schools), projections)
	);
	grounds.addFeatures(reader.readFeatures(collection(state.boundary), projections));
}
