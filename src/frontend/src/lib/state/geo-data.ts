/**
 * Session-scoped cache for GeoServer WFS layers.
 *
 * Overview points and optional province/district boundaries are reused on
 * remount. Detailed viewport polygons bypass this cache and support aborting
 * superseded requests. Failures are evicted so a retry refetches.
 */

/** Thrown when a WFS layer responds with an error or a non-JSON body. */
export class GeoLayerUnavailableError extends Error {
	constructor() {
		super('GeoServer layer is unavailable.');
		this.name = 'GeoLayerUnavailableError';
	}
}

const geoJsonCache = new Map<string, Promise<Record<string, unknown>>>();

/** Evict only the affected published layer, leaving region caches untouched. */
export function invalidateGeoLayer(typeName: string): void {
	for (const url of geoJsonCache.keys()) {
		if (new URL(url, 'http://localhost').searchParams.get('typeName') === typeName) {
			geoJsonCache.delete(url);
		}
	}
}

/**
 * Fetches and caches a WFS layer's GeoJSON, deduplicating concurrent callers.
 * GeoServer returns an OGC XML exception with HTTP 200 when a layer is not
 * published, so a non-JSON content type is treated as unavailable.
 */
export function loadGeoJson(
	url: string,
	options: { cache?: boolean; signal?: AbortSignal } = {}
): Promise<Record<string, unknown>> {
	// Viewport requests are short-lived and must not accumulate in the session cache.
	const cache = options.cache !== false && !options.signal;
	const existing = cache ? geoJsonCache.get(url) : undefined;
	if (existing) return existing;

	const pending = (async () => {
		const response = await fetch(url, options.signal ? { signal: options.signal } : undefined);
		const contentType = response.headers.get('content-type') ?? '';
		if (!response.ok || !contentType.includes('json')) {
			throw new GeoLayerUnavailableError();
		}
		return (await response.json()) as Record<string, unknown>;
	})();

	if (cache) geoJsonCache.set(url, pending);
	pending.catch(() => {
		if (geoJsonCache.get(url) === pending) geoJsonCache.delete(url);
	});
	return pending;
}
