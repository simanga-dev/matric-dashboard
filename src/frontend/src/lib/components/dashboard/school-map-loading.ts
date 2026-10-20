type GeoJsonLayer = Record<string, unknown>;

const WFS_BASE =
	'/api/geo/wfs?service=WFS&version=1.0.0&request=GetFeature&outputFormat=application/json';

/** Results and display names come from the directory, not GeoServer's performance join. */
export function schoolLayerUrl(typeName: string): string {
	return `${WFS_BASE}&typeName=${typeName}&propertyName=emis_number,geom`;
}

export function regionLayerUrl(typeName: string): string {
	return `${WFS_BASE}&typeName=${typeName}`;
}

const emptyLayer = (): GeoJsonLayer => ({ type: 'FeatureCollection', features: [] });

/** WFS 1.0 uses longitude/latitude ordering with EPSG:4326. */
export function viewportLayerUrl(typeName: string, extent: readonly number[]): string {
	const bbox = extent.map((value) => value.toFixed(6)).join(',');
	return `${schoolLayerUrl(typeName)}&bbox=${bbox},EPSG:4326`;
}

/** Only the newest viewport may update detail layers; obsolete requests are aborted. */
export function createViewportLoader(
	load: (url: string, signal: AbortSignal) => Promise<GeoJsonLayer>,
	apply: (footprints: GeoJsonLayer | null, grounds: GeoJsonLayer | null) => void
) {
	let generation = 0;
	let controller: AbortController | null = null;
	let disposed = false;

	return {
		async load(extent: readonly number[], zoom: number): Promise<void> {
			if (disposed) return;
			const current = ++generation;
			controller?.abort();
			controller = null;
			if (zoom < 12 || extent.length !== 4 || !extent.every(Number.isFinite)) {
				apply(emptyLayer(), emptyLayer());
				return;
			}
			controller = new AbortController();
			const { signal } = controller;
			const [footprints, grounds] = await Promise.all([
				load(viewportLayerUrl('matric:school_footprints', extent), signal).catch(() => null),
				load(viewportLayerUrl('matric:school_boundaries', extent), signal).catch(() => null)
			]);
			if (!disposed && current === generation) apply(footprints, grounds);
		},
		dispose() {
			disposed = true;
			generation += 1;
			controller?.abort();
		}
	};
}
