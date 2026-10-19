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

const FOOTPRINT_RADIUS_METRES = 25;
const FOOTPRINT_SEGMENTS = 12;

function pointCircle([longitude, latitude]: number[]) {
	const latitudeRadians = (latitude! * Math.PI) / 180;
	const latitudeDelta = FOOTPRINT_RADIUS_METRES / 111_320;
	const longitudeDelta =
		FOOTPRINT_RADIUS_METRES / (111_320 * Math.max(0.01, Math.cos(latitudeRadians)));
	const ring = Array.from({ length: FOOTPRINT_SEGMENTS + 1 }, (_, index) => {
		const angle = (index / FOOTPRINT_SEGMENTS) * Math.PI * 2;
		return [
			longitude! + longitudeDelta * Math.cos(angle),
			latitude! + latitudeDelta * Math.sin(angle)
		];
	});
	return { type: 'MultiPolygon', coordinates: [[ring]] };
}

/** Older layers publish points. Convert them to the same small polygon shape. */
export function ensureFootprintPolygons(layer: GeoJsonLayer): GeoJsonLayer {
	if (!Array.isArray(layer.features)) return layer;
	return {
		...layer,
		features: layer.features.map((value) => {
			const feature = value as {
				geometry?: { type?: string; coordinates?: unknown };
				[key: string]: unknown;
			};
			if (
				feature.geometry?.type !== 'Point' ||
				!Array.isArray(feature.geometry.coordinates) ||
				feature.geometry.coordinates.length < 2 ||
				!feature.geometry.coordinates.every((part) => typeof part === 'number')
			)
				return feature;
			return { ...feature, geometry: pointCircle(feature.geometry.coordinates as number[]) };
		})
	};
}

/**
 * Reads corrected school footprints, falling back to the legacy imported layer
 * while corrected layers have not been published or contain no rows.
 */
export async function loadPublishedSchoolLocations(
	load: (url: string) => Promise<GeoJsonLayer>,
	effectiveUrl: string,
	legacyUrl: string
): Promise<{ geojson: GeoJsonLayer; fallback: boolean }> {
	const effective = await load(effectiveUrl).catch(() => null);
	if (effective && featureCount(effective) > 0)
		return { geojson: ensureFootprintPolygons(effective), fallback: false };
	const legacy = await load(legacyUrl).catch(() => null);
	if (legacy && featureCount(legacy) > 0)
		return { geojson: ensureFootprintPolygons(legacy), fallback: true };
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
	const collection = (geometry: SpatialState['footprint'] | SpatialState['boundary']) => ({
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
		reader.readFeatures(schoolMapGeoJson(collection(state.footprint), schools), projections)
	);
	grounds.addFeatures(reader.readFeatures(collection(state.boundary), projections));
}
