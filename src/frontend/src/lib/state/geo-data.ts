/**
 * Session-scoped cache for GeoServer WFS layers.
 *
 * The school overview map fetches every school point (and the optional
 * province/district boundary layers) from GeoServer on each mount. Caching
 * the parsed GeoJSON avoids re-downloading the full layer when navigating
 * back to the overview. Failures are evicted so a retry refetches.
 */

/** Thrown when a WFS layer responds with an error or a non-JSON body. */
export class GeoLayerUnavailableError extends Error {
	constructor() {
		super('GeoServer layer is unavailable.');
		this.name = 'GeoLayerUnavailableError';
	}
}

const geoJsonCache = new Map<string, Promise<Record<string, unknown>>>();

/**
 * Fetches and caches a WFS layer's GeoJSON, deduplicating concurrent callers.
 * GeoServer returns an OGC XML exception with HTTP 200 when a layer is not
 * published, so a non-JSON content type is treated as unavailable.
 */
export function loadGeoJson(url: string): Promise<Record<string, unknown>> {
	const existing = geoJsonCache.get(url);
	if (existing) return existing;

	const pending = (async () => {
		const response = await fetch(url);
		const contentType = response.headers.get('content-type') ?? '';
		if (!response.ok || !contentType.includes('json')) {
			throw new GeoLayerUnavailableError();
		}
		return (await response.json()) as Record<string, unknown>;
	})();

	geoJsonCache.set(url, pending);
	pending.catch(() => geoJsonCache.delete(url));
	return pending;
}
