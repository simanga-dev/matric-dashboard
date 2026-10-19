export interface MapCoordinate {
	latitude: number;
	longitude: number;
}

const DECIMAL = String.raw`-?\d+(?:\.\d+)?`;
const PAIR = new RegExp(`^(${DECIMAL})\\s*[, ]\\s*(${DECIMAL})$`);
const AT = new RegExp(`@(${DECIMAL}),(${DECIMAL})`);
const PLACE = new RegExp(`!3d(${DECIMAL})!4d(${DECIMAL})`);
const QUERY = new RegExp(`[?&](?:q|query|ll|center)=(${DECIMAL}),(${DECIMAL})`);

function valid(latitude: number, longitude: number): MapCoordinate | null {
	if (!Number.isFinite(latitude) || !Number.isFinite(longitude)) return null;
	if (Math.abs(latitude) > 90 || Math.abs(longitude) > 180) return null;
	return { latitude, longitude };
}

function fromMatch(match: RegExpMatchArray | null): MapCoordinate | null {
	if (!match) return null;
	return valid(Number(match[1]), Number(match[2]));
}

/**
 * Reads a pasted decimal pair (latitude first) or a full Google Maps URL.
 * Short maps.app.goo.gl links do not include coordinates and are rejected.
 */
export function parseMapCoordinates(input: string): MapCoordinate | null {
	const value = input.trim();
	if (!value) return null;
	return (
		fromMatch(value.match(PAIR)) ??
		fromMatch(value.match(PLACE)) ??
		fromMatch(value.match(AT)) ??
		fromMatch(value.match(QUERY))
	);
}
