import type GeoJSON from 'ol/format/GeoJSON';
import type VectorSource from 'ol/source/Vector';
import type { SpatialState } from '$lib/api/school-spatial-contract';
import type { School } from '$lib/types/dashboard';
import { schoolMapGeoJson } from './school-map-data';

const projections = { dataProjection: 'EPSG:4326', featureProjection: 'EPSG:3857' };

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
