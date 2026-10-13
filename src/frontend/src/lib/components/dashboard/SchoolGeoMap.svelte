<script lang="ts">
	import { onMount, onDestroy, tick } from 'svelte';
	import 'ol/ol.css';
	import { resolve } from '$app/paths';
	import { Button } from '$lib/components/ui/button';
	import { schoolPath } from '$lib/config';
	import type Feature from 'ol/Feature';
	import type { FeatureLike } from 'ol/Feature';
	import type { StyleFunction } from 'ol/style/Style';
	import type { Point } from 'ol/geom';
	import type OlMap from 'ol/Map';
	import type VectorLayer from 'ol/layer/Vector';
	import { Maximize, Home, AlertCircle, MapPin, ExternalLink } from '@lucide/svelte';
	import { toTitleCase } from '$lib/utils';
	import { passRateColor } from './school-hierarchy';
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

	interface SchoolProperties {
		emis_number?: string;
		name?: string;
		province?: string;
		passed?: number;
		wrote?: number;
	}

	interface SchoolPopup {
		id: string;
		name: string;
		province: string;
		passRate: number;
		passed: number;
		wrote: number;
	}

	let {
		selectedSchoolId = $bindable(null),
		selectedProvince = null,
		selectedDistrict = null
	}: {
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
	let resizeObserver: ResizeObserver | null = null;
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

	/** Highlights the selected province or district and zooms to it. */
	function applyRegionFocus() {
		highlightProvince = selectedProvince;
		highlightDistrict = selectedDistrict;
		for (const layer of boundaryLayers) layer.changed();
		if (!olMap) return;

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

	function rateTextClass(rate: number): string {
		if (rate >= 80) return 'text-green-700 dark:text-green-400';
		if (rate >= 60) return 'text-amber-700 dark:text-amber-400';
		return 'text-red-700 dark:text-red-400';
	}

	function rateBarClass(rate: number): string {
		if (rate >= 80) return 'bg-green-500';
		if (rate >= 60) return 'bg-amber-500';
		return 'bg-red-500';
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
		view.animate({
			center: coord as number[],
			zoom: Math.max(view.getZoom() ?? 6, 10),
			duration: 500
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
		olMap?.setTarget(undefined);
	});

	async function loadMap() {
		resizeObserver?.disconnect();
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
			const response = await fetch(WFS_URL);
			const contentType = response.headers.get('content-type') ?? '';

			// GeoServer returns an OGC XML exception with HTTP 200 when the layer
			// is not published yet - do not feed that to JSON.parse.
			if (!response.ok || !contentType.includes('json')) {
				throw new Error(
					'School map layer is not available. Publish it with scripts/geoserver/bootstrap.sh.'
				);
			}

			const geojson = await response.json();
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
				{ defaults: defaultControls }
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
				import('ol/control')
			]);

			const { fromLonLat } = proj;
			if (disposed) return;
			const { Style, Icon: IconStyle, Fill, Stroke } = style;

			const reader = new GeoJSON();
			const source = new VectorSource();
			const features = reader.readFeatures(geojson, {
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
				const [provinceResponse, districtResponse] = await Promise.all([
					fetch(wfsUrl('matric:provinces')),
					fetch(wfsUrl('matric:districts'))
				]);
				if (provinceResponse.ok && provinceResponse.headers.get('content-type')?.includes('json')) {
					provinceFeatures = reader.readFeatures(await provinceResponse.json(), {
						dataProjection: 'EPSG:4326',
						featureProjection: 'EPSG:3857'
					});
					provinceSource.addFeatures(provinceFeatures);
				}
				if (districtResponse.ok && districtResponse.headers.get('content-type')?.includes('json')) {
					districtFeatures = reader.readFeatures(await districtResponse.json(), {
						dataProjection: 'EPSG:4326',
						featureProjection: 'EPSG:3857'
					});
					districtSource.addFeatures(districtFeatures);
				}
			} catch {
				// Boundaries are an enhancement; ignore fetch/parse failures.
			}
			if (disposed) return;

			// The map host lives inside the success branch, so it only exists
			// once `loading` is false. Flip state and wait for the DOM update
			// before attaching OpenLayers to the target element.
			loading = false;
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
						src: schoolMarkerIcon(passRateColor(rate), isSelected),
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
				if (highlightDistrict) return undefined;
				return sameName(feature.get('name'), highlightProvince) ? regionStyle() : undefined;
			};

			const districtStyle: StyleFunction = (feature: FeatureLike) => {
				if (!highlightDistrict) return undefined;
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
				layers: [
					new TileLayer({ source: new OSM() }),
					provinceLayer,
					districtLayer,
					new VectorLayerCtor({ source, style: styleFor })
				],
				view
			});
			vecLayer = olMap.getLayers().getArray()[3] as VectorLayer;

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
				const hit = olMap?.forEachFeatureAtPixel(event.pixel, (f) => f);
				if (!hit) {
					popup = null;
					overlay.setPosition(undefined);
					return;
				}

				const props = hit.getProperties() as SchoolProperties;
				// Apply selection before opening the popup, so tree selections can dismiss it.
				if (props.emis_number) {
					selectedSchoolId = String(props.emis_number);
					applySelection(selectedSchoolId);
				}
				const passed = Number(props.passed);
				const wrote = Number(props.wrote);
				popup = {
					id: String(props.emis_number),
					name: String(props.name),
					province: String(props.province),
					passRate: passRate(passed, wrote),
					passed,
					wrote
				};
				overlay.setPosition(event.coordinate);
			});

			// Apply any selection that was set before the map finished loading.
			applySelection(selectedSchoolId);
			olMap.updateSize();
		} catch (e) {
			if (!disposed) error = e instanceof Error ? e.message : 'Failed to load school locations.';
		} finally {
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
	<div class="flex min-h-0 flex-1 flex-col" aria-busy={loading}>
		{#if loading}
			<div role="status" class="relative min-h-0 flex-1 overflow-hidden bg-muted/40">
				<span class="sr-only">Loading school locations…</span>
				<div
					class="absolute inset-0 opacity-50"
					style="background-image: linear-gradient(hsl(var(--border)) 1px, transparent 1px), linear-gradient(90deg, hsl(var(--border)) 1px, transparent 1px); background-size: 3rem 3rem;"
				></div>
				{#each [[22, 32], [58, 54], [40, 70], [70, 26], [33, 48]] as [left, top], i (i)}
					<span
						class="absolute size-3 animate-pulse rounded-full bg-primary/40"
						style:left={`${left}%`}
						style:top={`${top}%`}
					></span>
				{/each}
				<span
					class="absolute inset-0 flex items-center justify-center text-sm text-muted-foreground"
					>Loading school locations…</span
				>
			</div>
		{:else if error}
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
			<div class="flex min-h-0 flex-1 flex-col">
				<div class="relative min-h-0 flex-1 overflow-hidden">
					<!-- OpenLayers installs keyboard interaction on this focusable map target. -->
					<!-- svelte-ignore a11y_no_noninteractive_tabindex -->
					<div
						bind:this={mapHost}
						tabindex="0"
						role="region"
						aria-label="Interactive school map. Use arrow keys to pan and plus or minus to zoom."
						class="h-full w-full bg-muted/20 focus-visible:outline-2 focus-visible:outline-ring"
					></div>
					<div
						class="absolute top-3 right-3 z-20 flex items-center gap-1 rounded-lg border bg-background/85 p-1 shadow-md backdrop-blur"
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
									class="text-2xl leading-none font-semibold tabular-nums {rateTextClass(
										popup.passRate
									)}"
								>
									{popup.passRate}%
								</span>
							</div>

							<div class="mt-2 h-1.5 overflow-hidden rounded-full bg-muted" aria-hidden="true">
								<div
									class="h-full rounded-full {rateBarClass(popup.passRate)}"
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
