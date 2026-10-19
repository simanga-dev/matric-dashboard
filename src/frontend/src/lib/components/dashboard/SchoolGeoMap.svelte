<script lang="ts">
	import { onMount, onDestroy, tick } from 'svelte';
	import 'ol/ol.css';
	import { resolve } from '$app/paths';
	import { Button } from '$lib/components/ui/button';
	import { schoolPath } from '$lib/config';
	import type { School } from '$lib/types/dashboard';
	import { schoolMapGeoJson, schoolMapPopup } from './school-map-data';
	import type Feature from 'ol/Feature';
	import type { FeatureLike } from 'ol/Feature';
	import type { StyleFunction } from 'ol/style/Style';
	import type { Point } from 'ol/geom';
	import type OlMap from 'ol/Map';
	import type VectorLayer from 'ol/layer/Vector';
	import { Maximize, Home, AlertCircle, MapPin, ExternalLink } from '@lucide/svelte';
	import { toTitleCase } from '$lib/utils';
	import {
		passRateBand,
		passRateBarClass,
		passRateTextClass,
		resolvePassRateColor,
		type PassRateBand
	} from './school-hierarchy';
	import { loadGeoJson, GeoLayerUnavailableError } from '$lib/state';
	/**
	 * School points served by GeoServer over WFS, proxied through the API.
	 * The GeoServer layer is a PostGIS table (geo.school_locations).
	 *
	 * Selection is keyed on the EMIS number, which is the same identifier the
	 * school tree uses (SchoolOutput.Id), so selecting a school in the tree
	 * highlights and zooms to it here - and vice versa.
	 */
	const WFS_BASE =
		'/api/geo/wfs?service=WFS&version=1.0.0&request=GetFeature&outputFormat=application/json';
	const wfsUrl = (typeName: string) => `${WFS_BASE}&typeName=${typeName}`;
	const WFS_URL = wfsUrl('matric:school_locations');

	interface SchoolPopup {
		id: string;
		name: string;
		province: string;
		passRate: number;
		passed: number;
		wrote: number;
	}

	let {
		schools,
		selectedSchoolId = $bindable(null),
		selectedProvince = null,
		selectedDistrict = null
	}: {
		schools: School[];
		selectedSchoolId?: string | null;
		selectedProvince?: string | null;
		selectedDistrict?: string | null;
	} = $props();

	let mapHost: HTMLDivElement | null = $state(null);
	let popupHost: HTMLDivElement | null = $state(null);
	let loading = $state(true);
	let error = $state<string | null>(null);
	let featureCount = $state(0);
	let popup = $state<SchoolPopup | null>(null);
	let selectedLocationMissing = $state(false);

	// OpenLayers objects are held outside $state to avoid Svelte's deep proxy
	// wrapping them (which breaks OpenLayers internals).
	let olMap: OlMap | null = null;
	let vecLayer: VectorLayer | null = null;
	let boundaryLayers: VectorLayer[] = [];
	let provinceFeatures: Feature[] = [];
	let districtFeatures: Feature[] = [];
	let highlightProvince: string | null = null;
	let highlightDistrict: string | null = null;
	let highlightCountry = false;
	let resizeObserver: ResizeObserver | null = null;
	let themeObserver: MutationObserver | null = null;
	let selectedEmis: string | null = null;
	let disposed = false;
	let resetView: (() => void) | null = null;
	let fitSchools: (() => void) | null = null;
	let hidePopup: (() => void) | null = null;

	function passRate(passed: number, wrote: number): number {
		return wrote > 0 ? Math.round((passed / wrote) * 1000) / 10 : 0;
	}

	/** Case-insensitive name match; false when either side is empty. */
	function sameName(value: unknown, target: string | null): boolean {
		if (!target) return false;
		return String(value ?? '').toUpperCase() === target.toUpperCase();
	}

	/** Union of the geometry extents of the given features. */
	function boundaryExtent(features: Feature[]): [number, number, number, number] | null {
		let minX = Infinity;
		let minY = Infinity;
		let maxX = -Infinity;
		let maxY = -Infinity;
		let found = false;
		for (const feature of features) {
			const box = feature.getGeometry()?.getExtent();
			if (!box) continue;
			const [x0, y0, x1, y1] = box;
			if (x0 === undefined || y0 === undefined || x1 === undefined || y1 === undefined) continue;
			if (!Number.isFinite(x0)) continue;
			minX = Math.min(minX, x0);
			minY = Math.min(minY, y0);
			maxX = Math.max(maxX, x1);
			maxY = Math.max(maxY, y1);
			found = true;
		}
		return found ? [minX, minY, maxX, maxY] : null;
	}

	/**
	 * Reflects the current selection on the boundary layers: the whole country
	 * when nothing narrower is selected, otherwise a province or district.
	 */
	function applyRegionFocus() {
		highlightProvince = selectedProvince;
		highlightDistrict = selectedDistrict;
		highlightCountry = !selectedSchoolId && !selectedProvince && !selectedDistrict;
		for (const layer of boundaryLayers) layer.changed();
		if (!olMap) return;

		if (highlightCountry) {
			const country = boundaryExtent(provinceFeatures);
			if (country) {
				olMap.getView().fit(country, { padding: [40, 40, 40, 40], duration: 400 });
			}
			return;
		}

		const target = highlightDistrict
			? districtFeatures.find(
					(feature) =>
						sameName(feature.get('name'), highlightDistrict) &&
						sameName(feature.get('province'), highlightProvince)
				)
			: highlightProvince
				? provinceFeatures.find((feature) => sameName(feature.get('name'), highlightProvince))
				: null;

		const extent = target?.getGeometry()?.getExtent();
		if (extent && Number.isFinite(extent[0])) {
			olMap.getView().fit(extent, { padding: [40, 40, 40, 40], duration: 400, maxZoom: 10 });
		}
	}

	const SCHOOL_ICON_NODE = [
		'<path d="M14 21v-3a2 2 0 0 0-4 0v3"/>',
		'<path d="M18 5v16"/>',
		'<path d="m4 6 7.106-3.79a2 2 0 0 1 1.788 0L20 6"/>',
		'<path d="m6 11-3.52 2.147a1 1 0 0 0-.48.854V19a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-5a1 1 0 0 0-.48-.853L18 11"/>',
		'<path d="M6 5v16"/>',
		'<circle cx="12" cy="9" r="2"/>'
	].join('');

	const markerIconCache: Record<string, string> = {};
	// Resolving theme variables costs a style read, so cache one colour per band
	// and clear it when the palette changes.
	let markerColorCache: Partial<Record<PassRateBand, string>> = {};

	function markerColor(rate: number): string {
		const band = passRateBand(rate);
		const cached = markerColorCache[band];
		if (cached) return cached;
		const color = resolvePassRateColor(rate);
		markerColorCache[band] = color;
		return color;
	}

	/** Lucide school glyph as an OpenLayers icon, haloed so it reads on the basemap. */
	function schoolMarkerIcon(color: string, selected: boolean): string {
		const key = `${color}:${selected ? 1 : 0}`;
		const cached = markerIconCache[key];
		if (cached) return cached;
		const halo = selected
			? `<g stroke="#111827" stroke-width="5">${SCHOOL_ICON_NODE}</g>`
			: `<g stroke="#ffffff" stroke-width="4">${SCHOOL_ICON_NODE}</g>`;
		const svg =
			'<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 24 24" ' +
			`fill="none" stroke-linecap="round" stroke-linejoin="round">${halo}` +
			`<g stroke="${color}" stroke-width="2">${SCHOOL_ICON_NODE}</g></svg>`;
		const src = `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
		markerIconCache[key] = src;
		return src;
	}

	function findFeature(emis: string): FeatureLike | null {
		const source = vecLayer?.getSource();
		if (!source) return null;
		return source.getFeatures().find((f) => String(f.get('emis_number')) === emis) ?? null;
	}

	/** Applies the selection: restyle, and pan/zoom to the selected school. */
	function applySelection(emis: string | null) {
		if (!vecLayer) return;
		if (selectedEmis === emis) return;
		selectedEmis = emis;
		vecLayer?.changed();
		popup = null;
		hidePopup?.();
		selectedLocationMissing = false;

		if (!emis || !olMap) return;
		const feature = findFeature(emis);
		const coord = (feature?.getGeometry() as Point | undefined)?.getCoordinates();
		if (!coord || coord.length < 2 || !coord.every(Number.isFinite)) {
			selectedLocationMissing = true;
			return;
		}

		const view = olMap.getView();
		// A new click replaces the current pan rather than competing with it.
		view.cancelAnimations();
		view.animate({
			center: coord as number[],
			zoom: Math.max(view.getZoom() ?? 6, 10),
			duration: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 0 : 450
		});
	}

	$effect(() => {
		applySelection(selectedSchoolId);
	});

	$effect(() => {
		applyRegionFocus();
	});

	onDestroy(() => {
		disposed = true;
		resizeObserver?.disconnect();
		themeObserver?.disconnect();
		olMap?.setTarget(undefined);
	});

	async function loadMap() {
		resizeObserver?.disconnect();
		themeObserver?.disconnect();
		themeObserver = null;
		olMap?.setTarget(undefined);
		olMap = null;
		vecLayer = null;
		boundaryLayers = [];
		provinceFeatures = [];
		districtFeatures = [];
		selectedEmis = null;
		popup = null;
		resetView = null;
		fitSchools = null;
		loading = true;
		error = null;
		try {
			const geojson = await loadGeoJson(WFS_URL);
			if (disposed) return;

			const [
				{ default: OlMapCtor },
				{ default: View },
				{ default: TileLayer },
				{ default: VectorLayerCtor },
				{ default: VectorSource },
				{ default: OSM },
				{ default: GeoJSON },
				{ default: Overlay },
				proj,
				style,
				{ defaults: defaultControls },
				{ defaults: defaultInteractions }
			] = await Promise.all([
				import('ol/Map'),
				import('ol/View'),
				import('ol/layer/Tile'),
				import('ol/layer/Vector'),
				import('ol/source/Vector'),
				import('ol/source/OSM'),
				import('ol/format/GeoJSON'),
				import('ol/Overlay'),
				import('ol/proj'),
				import('ol/style'),
				import('ol/control'),
				import('ol/interaction/defaults')
			]);

			const { fromLonLat } = proj;
			if (disposed) return;
			const { Style, Icon: IconStyle, Fill, Stroke } = style;

			const reader = new GeoJSON();
			const source = new VectorSource();
			const features = reader.readFeatures(schoolMapGeoJson(geojson, schools), {
				dataProjection: 'EPSG:4326',
				featureProjection: 'EPSG:3857'
			});
			source.addFeatures(features);
			featureCount = features.length;

			// Administrative boundaries are optional: if the layer is missing the
			// school map still renders, just without region highlighting.
			const provinceSource = new VectorSource();
			const districtSource = new VectorSource();
			try {
				const [provinceGeo, districtGeo] = await Promise.all([
					loadGeoJson(wfsUrl('matric:provinces')).catch(() => null),
					loadGeoJson(wfsUrl('matric:districts')).catch(() => null)
				]);
				if (provinceGeo) {
					provinceFeatures = reader.readFeatures(provinceGeo, {
						dataProjection: 'EPSG:4326',
						featureProjection: 'EPSG:3857'
					});
					provinceSource.addFeatures(provinceFeatures);
				}
				if (districtGeo) {
					districtFeatures = reader.readFeatures(districtGeo, {
						dataProjection: 'EPSG:4326',
						featureProjection: 'EPSG:3857'
					});
					districtSource.addFeatures(districtFeatures);
				}
			} catch {
				// Boundaries are an enhancement; ignore fetch/parse failures.
			}
			if (disposed) return;

			// The host stays mounted beneath the loading overlay so OpenLayers
			// measures the final panel dimensions before anything is revealed.
			await tick();

			if (disposed || !mapHost || !popupHost) {
				return;
			}

			const styleFor: StyleFunction = (feature: FeatureLike) => {
				const emis = String(feature.get('emis_number'));
				const isSelected = selectedEmis !== null && emis === selectedEmis;
				const rate = passRate(Number(feature.get('passed')), Number(feature.get('wrote')));

				return new Style({
					zIndex: isSelected ? 2 : 1,
					image: new IconStyle({
						src: schoolMarkerIcon(markerColor(rate), isSelected),
						anchor: [0.5, 0.5],
						scale: isSelected ? 1.15 : 0.85
					})
				});
			};

			const regionStyle = () =>
				new Style({
					fill: new Fill({ color: 'rgba(29, 78, 216, 0.15)' }),
					stroke: new Stroke({ color: '#1d4ed8', width: 2 })
				});

			const provinceStyle: StyleFunction = (feature: FeatureLike) => {
				if (highlightCountry) return regionStyle();
				if (highlightDistrict) return undefined;
				return sameName(feature.get('name'), highlightProvince) ? regionStyle() : undefined;
			};

			const districtStyle: StyleFunction = (feature: FeatureLike) => {
				if (highlightCountry || !highlightDistrict) return undefined;
				return sameName(feature.get('name'), highlightDistrict) &&
					sameName(feature.get('province'), highlightProvince)
					? regionStyle()
					: undefined;
			};

			const provinceLayer = new VectorLayerCtor({
				source: provinceSource,
				style: provinceStyle
			});
			const districtLayer = new VectorLayerCtor({
				source: districtSource,
				style: districtStyle
			});
			boundaryLayers = [provinceLayer, districtLayer];

			const view = new View({ center: fromLonLat([24.7, -29]), zoom: 5 });

			olMap = new OlMapCtor({
				target: mapHost,
				controls: defaultControls({ rotate: false }),
				// Accept wheel zoom over the map without requiring a click or stealing focus.
				interactions: defaultInteractions({ onFocusOnly: false }),
				layers: [
					// Theme only the raster background, never the performance markers.
					new TileLayer({ source: new OSM(), className: 'school-basemap' }),
					provinceLayer,
					districtLayer,
					new VectorLayerCtor({ source, style: styleFor })
				],
				view
			});
			olMap.once('rendercomplete', () => {
				if (!disposed) loading = false;
			});
			vecLayer = olMap.getLayers().getArray()[3] as VectorLayer;

			// Marker colours are resolved from theme variables for the SVG data
			// URL, so restyle when the palette, scheme or chart colour changes.
			themeObserver = new MutationObserver(() => {
				markerColorCache = {};
				vecLayer?.changed();
			});
			themeObserver.observe(document.documentElement, {
				attributes: true,
				attributeFilter: ['class', 'data-theme-preset', 'data-theme-chart']
			});

			const extent = source.getExtent();
			fitSchools = () => {
				if (extent && Number.isFinite(extent[0]))
					view.fit(extent, { padding: [48, 48, 48, 48], maxZoom: 9, duration: 0 });
			};
			// Whole South Africa: 16.3E-33E, 34.9S-22.1S.
			const countryExtent = [...fromLonLat([16.3, -34.9]), ...fromLonLat([33.0, -22.1])];
			resetView = () => {
				view.fit(countryExtent, { padding: [24, 24, 24, 24], duration: 300 });
			};
			fitSchools();

			// A region selection arriving before the map loaded takes precedence
			// over the default fit-to-schools framing.
			applyRegionFocus();

			resizeObserver = new ResizeObserver(() => olMap?.updateSize());
			resizeObserver.observe(mapHost);

			const overlay = new Overlay({
				element: popupHost,
				autoPan: true,
				positioning: 'bottom-center',
				offset: [0, -12]
			});
			olMap.addOverlay(overlay);
			hidePopup = () => overlay.setPosition(undefined);

			olMap.on('singleclick', (event) => {
				const hit = olMap?.forEachFeatureAtPixel(event.pixel, (f) => f, {
					layerFilter: (layer) => layer === vecLayer
				});
				const selectedPopup = hit ? schoolMapPopup(hit.getProperties()) : null;
				if (!selectedPopup) {
					popup = null;
					overlay.setPosition(undefined);
					return;
				}

				// Apply selection before opening the popup, so tree selections can dismiss it.
				selectedSchoolId = selectedPopup.id;
				applySelection(selectedSchoolId);
				popup = selectedPopup;
				overlay.setPosition(event.coordinate);
			});

			// Apply any selection that was set before the map finished loading.
			applySelection(selectedSchoolId);
			olMap.updateSize();
		} catch (e) {
			if (!disposed) {
				error =
					e instanceof GeoLayerUnavailableError
						? 'School map layer is not available. Publish it with scripts/geoserver/bootstrap.sh.'
						: e instanceof Error
							? e.message
							: 'Failed to load school locations.';
			}
			if (!disposed) loading = false;
		}
	}

	onMount(() => {
		void loadMap();
	});
</script>

<section
	aria-label="School locations"
	class="flex h-full min-h-0 flex-col bg-card text-card-foreground"
>
	<div class="relative flex min-h-0 flex-1 flex-col" aria-busy={loading}>
		{#if loading}
			<div role="status" class="absolute inset-0 z-30 flex items-center justify-center bg-card">
				<div
					class="flex items-center gap-2 rounded-lg border bg-muted/30 px-4 py-3 text-sm text-muted-foreground"
				>
					<MapPin class="size-4 motion-safe:animate-pulse" aria-hidden="true" />
					<span>Loading school map…</span>
				</div>
			</div>
		{/if}
		{#if error}
			<div role="alert" class="m-auto max-w-sm space-y-3 p-6">
				<AlertCircle class="size-6 text-destructive" />
				<h3 class="text-sm font-semibold">Map unavailable</h3>
				<p class="text-xs break-words text-muted-foreground">{error}</p>
				<button
					type="button"
					onclick={loadMap}
					class="rounded-md border px-3 py-2 text-xs hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring"
					>Try again</button
				>
				<p class="text-xs text-muted-foreground">
					You can still browse schools and view their results.
				</p>
			</div>
		{:else}
			<div class="flex min-h-0 flex-1 flex-col" inert={loading}>
				<div class="relative min-h-0 flex-1 overflow-hidden">
					<!-- OpenLayers installs keyboard interaction on this focusable map target. -->
					<!-- svelte-ignore a11y_no_noninteractive_tabindex -->
					<div
						bind:this={mapHost}
						tabindex="0"
						role="region"
						aria-label="Interactive school map. Use arrow keys to pan and plus or minus to zoom."
						class="school-map h-full w-full bg-background focus-visible:outline-2 focus-visible:outline-ring"
					></div>
					<div
						class="absolute end-3 top-3 z-20 flex items-center gap-1 rounded-lg bg-background/85 p-1 shadow-md backdrop-blur"
					>
						<button
							type="button"
							aria-label="Fit all school locations"
							title="Fit all school locations"
							onclick={() => fitSchools?.()}
							class="rounded-md p-1.5 text-foreground hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring"
							><Maximize class="size-4" /></button
						>
						<button
							type="button"
							aria-label="Reset map to full view"
							title="Reset map to full view"
							onclick={() => resetView?.()}
							class="rounded-md p-1.5 text-foreground hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring"
							><Home class="size-4" /></button
						>
					</div>
					{#if featureCount === 0}<div
							role="status"
							class="pointer-events-none absolute inset-x-4 top-4 rounded-md border bg-background/95 p-3 text-sm shadow-sm"
						>
							No school locations are available yet. Use the directory to view school results.
						</div>{/if}
					{#if selectedLocationMissing && featureCount > 0}<div
							role="status"
							class="pointer-events-none absolute inset-x-4 top-4 rounded-md border bg-background/95 p-3 text-xs shadow-sm"
						>
							The selected school has no mapped location. Its results are shown in the details
							panel.
						</div>{/if}
					<div
						bind:this={popupHost}
						class="pointer-events-none w-60 rounded-xl border bg-popover p-3 shadow-lg {popup
							? ''
							: 'hidden'}"
					>
						{#if popup}
							<div class="min-w-0">
								<div class="truncate text-sm font-semibold">{toTitleCase(popup.name)}</div>
								<div class="mt-0.5 flex items-center gap-1 text-xs text-muted-foreground">
									<MapPin class="size-3 shrink-0" />
									<span class="truncate">{toTitleCase(popup.province)}</span>
								</div>
							</div>

							<div class="mt-3 flex items-end justify-between gap-3">
								<span class="text-xs text-muted-foreground">Pass rate</span>
								<span
									class="text-2xl leading-none font-semibold tabular-nums {passRateTextClass(
										popup.passRate
									)}"
								>
									{popup.passRate}%
								</span>
							</div>

							<div class="mt-2 h-1.5 overflow-hidden rounded-full bg-muted" aria-hidden="true">
								<div
									class="h-full rounded-full {passRateBarClass(popup.passRate)}"
									style:width={`${Math.min(100, Math.max(0, popup.passRate))}%`}
								></div>
							</div>

							<div class="mt-2 flex items-center justify-between text-xs text-muted-foreground">
								<span>{popup.passed} passed</span>
								<span>{popup.wrote - popup.passed} did not pass</span>
							</div>

							<Button
								variant="outline"
								size="sm"
								class="pointer-events-auto mt-3 w-full gap-1.5"
								href={resolve(schoolPath(popup.id))}
							>
								<ExternalLink class="size-3.5" />
								View school
							</Button>
						{/if}
					</div>
				</div>
			</div>
		{/if}
	</div>
</section>

<style>
	/* Keep filters on the tile canvas so vector layers and popups retain their colours. */
	.school-map :global(.school-basemap canvas) {
		filter: grayscale(1);
		transition: filter 250ms ease;
	}

	:global(.dark) .school-map :global(.school-basemap canvas) {
		filter: grayscale(1) invert(1) brightness(0.9) contrast(0.85);
	}

	.school-map :global(.school-basemap)::after {
		content: '';
		position: absolute;
		inset: 0;
		background: hsl(var(--primary) / 0.06);
		pointer-events: none;
	}

	.school-map :global(.ol-control) {
		background: hsl(var(--background) / 0.9);
		color: hsl(var(--foreground));
		border: 0;
		border-radius: var(--radius);
	}

	.school-map :global(.ol-control button) {
		background: hsl(var(--card));
		color: hsl(var(--foreground));
	}

	.school-map :global(.ol-control button:hover),
	.school-map :global(.ol-control button:focus-visible) {
		background: hsl(var(--accent));
		color: hsl(var(--accent-foreground));
	}

	.school-map :global(.ol-control button:focus-visible) {
		outline: 2px solid hsl(var(--ring));
		outline-offset: 2px;
	}

	.school-map :global(.ol-attribution a) {
		color: hsl(var(--primary));
	}

	.school-map :global(.ol-attribution ul) {
		color: hsl(var(--foreground));
		text-shadow: none;
	}

	@media (prefers-reduced-motion: reduce) {
		.school-map :global(.school-basemap canvas) {
			transition: none;
		}
	}
</style>
