import type { components } from './v1';

export type SpatialPoint = { type: 'Point'; coordinates: number[] };
export type SpatialBoundary =
	| { type: 'Polygon'; coordinates: number[][][] }
	| { type: 'MultiPolygon'; coordinates: number[][][][] };
/** Generated transport contracts, with JsonElement geometry narrowed at runtime. */
export type SpatialState = Omit<
	components['schemas']['SpatialStateOutput'],
	'boundary' | 'location'
> & {
	boundary: SpatialBoundary | null;
	location: SpatialPoint | null;
};
export type SpatialSave = Omit<
	components['schemas']['SaveSpatialRequest'],
	'boundary' | 'location' | 'locationMode'
> & {
	boundary: SpatialBoundary | null;
	locationMode: 'imported' | 'override';
	location: SpatialPoint | null;
};
export type SpatialHistory = Omit<
	components['schemas']['SpatialHistoryOutput'],
	'beforeBoundary' | 'afterBoundary' | 'beforeLocationOverride' | 'afterLocationOverride'
> & {
	beforeBoundary: SpatialBoundary | null;
	afterBoundary: SpatialBoundary | null;
	beforeLocationOverride: SpatialPoint | null;
	afterLocationOverride: SpatialPoint | null;
};

function position(value: unknown): value is number[] {
	return (
		Array.isArray(value) &&
		value.length === 2 &&
		value.every((coordinate) => typeof coordinate === 'number' && Number.isFinite(coordinate)) &&
		Math.abs(value[0]) <= 180 &&
		Math.abs(value[1]) <= 90
	);
}
function ring(value: unknown): value is number[][] {
	return Array.isArray(value) && value.length >= 4 && value.every(position);
}
function polygon(value: unknown): value is number[][][] {
	return Array.isArray(value) && value.length > 0 && value.every(ring);
}
export function isSpatialPoint(value: unknown): value is SpatialPoint | null {
	return (
		value === null ||
		(typeof value === 'object' &&
			value !== null &&
			'type' in value &&
			value.type === 'Point' &&
			'coordinates' in value &&
			position(value.coordinates))
	);
}
export function isSpatialBoundary(value: unknown): value is SpatialBoundary | null {
	if (value === null) return true;
	if (typeof value !== 'object' || !('type' in value) || !('coordinates' in value)) return false;
	return (
		(value.type === 'Polygon' && polygon(value.coordinates)) ||
		(value.type === 'MultiPolygon' &&
			Array.isArray(value.coordinates) &&
			value.coordinates.length > 0 &&
			value.coordinates.every(polygon))
	);
}
export function spatialStateResponse(
	data: components['schemas']['SpatialStateOutput']
): SpatialState {
	const { boundary, location } = data;
	if (!isSpatialBoundary(boundary) || !isSpatialPoint(location))
		throw new Error(
			'The school map service returned unsupported geometry. Your draft is retained.'
		);
	return { ...data, boundary, location };
}
export function spatialHistoryResponse(
	data: components['schemas']['SpatialHistoryOutput']
): SpatialHistory {
	const { beforeBoundary, afterBoundary, beforeLocationOverride, afterLocationOverride } = data;
	if (
		!isSpatialBoundary(beforeBoundary) ||
		!isSpatialBoundary(afterBoundary) ||
		!isSpatialPoint(beforeLocationOverride) ||
		!isSpatialPoint(afterLocationOverride)
	)
		throw new Error('Revision history contains unsupported geometry.');
	return { ...data, beforeBoundary, afterBoundary, beforeLocationOverride, afterLocationOverride };
}
