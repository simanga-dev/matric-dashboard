<script lang="ts">
	import { onMount } from 'svelte';
	import { page } from '$app/state';
	import type OlMap from 'ol/Map';
	import type { FeatureLike } from 'ol/Feature';
	import type { SpatialState } from '$lib/api/school-spatial-contract';
	import { hasVerifiedLocation, schoolLocationMapGeoJson } from './school-location-map';
	import { attributionHtml, mapImagery } from './school-map-imagery';
	import { MapPin } from '@lucide/svelte';
	import 'ol/ol.css';

	let {
		spatial,
		schoolName,
		bottomInset = 0
	}: {
		spatial: SpatialState | null;
		schoolName: string;
		/** Pixels covered by the overlay card, so the marker is framed above it. */
		bottomInset?: number;
	} = $props();

	let mapHost: HTMLDivElement | null = $state(null);
	let map = $state.raw<OlMap | null>(null);
	let loading = $state(true);
	let failed = $state(false);
	let sideBySide = $state(false);
	let mapped = $derived(hasVerifiedLocation(spatial));
	let imagery = $derived(mapImagery(page.data.mapImageryUrl, page.data.mapImageryAttribution));
	let frameView: ((inset: number, sideBySide: boolean) => void) | null = null;

	$effect(() => {
		frameView?.(bottomInset, sideBySide);
	});

	onMount(() => {
		let disposed = false;
		let resizeObserver: ResizeObserver | null = null;
		const wideQuery = window.matchMedia('(min-width: 1024px)');
		sideBySide = wideQuery.matches;
		const onWideChange = (event: MediaQueryListEvent) => {
			sideBySide = event.matches;
		};
		wideQuery.addEventListener('change', onWideChange);

		if (!mapped) {
			loading = false;
			return () => wideQuery.removeEventListener('change', onWideChange);
		}

		async function loadMap() {
			try {
				const [
					{ default: OlMapCtor },
					{ default: View },
					{ default: TileLayer },
					{ default: VectorLayer },
					{ default: VectorSource },
					{ default: OSM },
					{ default: XYZ },
					{ default: GeoJSON },
					{ default: Style },
					{ default: Fill },
					{ default: Stroke },
					{ default: Circle },
					proj,
					{ defaults: defaultControls },
					{ defaults: defaultInteractions }
				] = await Promise.all([
					import('ol/Map'),
					import('ol/View'),
					import('ol/layer/Tile'),
					import('ol/layer/Vector'),
					import('ol/source/Vector'),
					import('ol/source/OSM'),
					import('ol/source/XYZ'),
					import('ol/format/GeoJSON'),
					import('ol/style/Style'),
					import('ol/style/Fill'),
					import('ol/style/Stroke'),
					import('ol/style/Circle'),
					import('ol/proj'),
					import('ol/control'),
					import('ol/interaction/defaults')
				]);

				if (disposed || !mapHost || !spatial?.location) return;

				const { fromLonLat } = proj;
				const tokens = getComputedStyle(document.documentElement);
				const primary = tokens.getPropertyValue('--primary').trim() || '215 20% 45%';
				const background = tokens.getPropertyValue('--background').trim() || '0 0% 100%';

				const source = new VectorSource();
				source.addFeatures(
					new GeoJSON().readFeatures(schoolLocationMapGeoJson(spatial), {
						dataProjection: 'EPSG:4326',
						featureProjection: 'EPSG:3857'
					})
				);

				const boundaryStyle = new Style({
					zIndex: 1,
					fill: new Fill({ color: `hsl(${primary} / 0.1)` }),
					stroke: new Stroke({ color: `hsl(${primary})`, width: 2 })
				});
				const footprintStyle = new Style({
					zIndex: 1,
					fill: new Fill({ color: `hsl(${primary} / 0.24)` }),
					stroke: new Stroke({ color: `hsl(${primary})`, width: 2 })
				});
				const haloStyle = new Style({
					zIndex: 2,
					image: new Circle({
						radius: 16,
						fill: new Fill({ color: `hsl(${primary} / 0.18)` })
					})
				});
				const markerStyle = new Style({
					zIndex: 3,
					image: new Circle({
						radius: 8,
						fill: new Fill({ color: `hsl(${primary})` }),
						stroke: new Stroke({ color: `hsl(${background})`, width: 2.5 })
					})
				});
				const styleFor = (feature: FeatureLike) => {
					const role = feature.get('role');
					if (role === 'location') return [haloStyle, markerStyle];
					if (role === 'footprint') return footprintStyle;
					return boundaryStyle;
				};

				const [longitude, latitude] = spatial.location.coordinates;
				if (longitude === undefined || latitude === undefined) return;
				const view = new View({
					center: fromLonLat([longitude, latitude]),
					zoom: 17
				});

				// The detail hero opens on satellite imagery when a provider is
				// configured, with the street map one failed tile away.
				const streetLayer = new TileLayer({
					source: new OSM(),
					className: 'school-hero-basemap',
					visible: !imagery
				});
				const imagerySource = imagery
					? new XYZ({
							url: imagery.url,
							attributions: attributionHtml(imagery.attribution)
						})
					: null;
				const imageryLayer = imagerySource
					? new TileLayer({ source: imagerySource, className: 'school-hero-imagery' })
					: null;
				imagerySource?.on('tileloaderror', () => {
					streetLayer.setVisible(true);
					imageryLayer?.setVisible(false);
				});

				map = new OlMapCtor({
					target: mapHost,
					controls: defaultControls({
						zoom: false,
						rotate: false,
						attributionOptions: { collapsible: false }
					}),
					// The hero sits inside a scrolling page, so wheel zoom only
					// engages after the map itself receives focus.
					interactions: defaultInteractions({ onFocusOnly: true }),
					layers: [
						streetLayer,
						...(imageryLayer ? [imageryLayer] : []),
						new VectorLayer({ source, style: styleFor })
					],
					view
				});

				map.once('postrender', () => {
					if (!disposed) loading = false;
				});

				// Frame the drawn grounds when the school has them, otherwise show
				// the immediate surroundings around the verified marker. On wide
				// screens the school is pinned to roughly 75% of the width so the
				// floating detail card can own the left side.
				const extent = source.getExtent() ?? [0, 0, 0, 0];
				const [minX = 0, minY = 0, maxX = 0, maxY = 0] = extent;
				const hasArea = Number.isFinite(minX) && (minX !== maxX || minY !== maxY);
				const applyInset = (inset: number, wide: boolean) => {
					const size = map?.getSize();
					const width = size?.[0];
					const heightPixels = size?.[1];
					if (width === undefined || heightPixels === undefined) return;

					const safeInset = Math.max(0, Math.round(inset));
					const focusX = wide ? width * 0.75 : width * 0.5;
					const focusY = wide ? heightPixels * 0.5 : (heightPixels - safeInset) / 2;
					if (hasArea) {
						const usableWidth = wide ? width * 0.5 : width;
						const usableHeight = wide ? heightPixels : heightPixels - safeInset;
						view.fit(extent, {
							size: [Math.max(160, usableWidth - 96), Math.max(120, usableHeight - 96)],
							maxZoom: 18,
							duration: 0
						});
						view.centerOn(
							[(minX + maxX) / 2, (minY + maxY) / 2],
							[width, heightPixels],
							[focusX, focusY]
						);
					} else {
						view.setZoom(17);
						view.centerOn(
							fromLonLat([longitude, latitude]),
							[width, heightPixels],
							[focusX, focusY]
						);
					}
				};
				frameView = applyInset;
				applyInset(bottomInset, sideBySide);

				resizeObserver = new ResizeObserver(() => {
					map?.updateSize();
					frameView?.(bottomInset, sideBySide);
				});
				resizeObserver.observe(mapHost);
				map.updateSize();
			} catch {
				if (!disposed) {
					failed = true;
					loading = false;
				}
			}
		}

		void loadMap();

		return () => {
			disposed = true;
			wideQuery.removeEventListener('change', onWideChange);
			frameView = null;
			resizeObserver?.disconnect();
			map?.setTarget(undefined);
			map = null;
		};
	});
</script>

<section
	aria-label="School location map"
	class="relative h-full w-full overflow-hidden bg-muted"
	style:--school-map-inset={`${bottomInset}px`}
>
	<div class="relative h-full w-full" aria-busy={mapped && loading}>
		{#if mapped}
			<!-- OpenLayers installs keyboard interaction on this focusable map target. -->
			<!-- svelte-ignore a11y_no_noninteractive_tabindex -->
			<div
				bind:this={mapHost}
				tabindex="0"
				role="region"
				aria-label="Interactive close-up map of {schoolName}. Use arrow keys to pan and plus or minus to zoom."
				class="school-location-map h-full w-full bg-muted focus-visible:outline-2 focus-visible:outline-ring"
			></div>

			{#if loading}
				<div
					class="absolute inset-0 z-10 flex items-center justify-center bg-card"
					style:padding-bottom={`${bottomInset}px`}
				>
					<p
						role="status"
						class="flex items-center gap-2 rounded-lg border bg-muted/30 px-4 py-3 text-sm text-muted-foreground"
					>
						<MapPin class="size-4 motion-safe:animate-pulse" aria-hidden="true" />
						<span>Loading school map…</span>
					</p>
				</div>
			{:else if failed}
				<div
					class="absolute inset-0 z-10 flex flex-col items-center justify-center gap-1 bg-card p-6 text-center"
					style:padding-bottom={`${bottomInset}px`}
				>
					<MapPin class="size-5 text-muted-foreground" aria-hidden="true" />
					<p class="text-sm font-medium">The school map could not be loaded</p>
					<p class="text-xs text-muted-foreground">School results are still available below.</p>
				</div>
			{/if}
		{:else}
			<div
				class="flex h-full flex-col items-center justify-center gap-2 bg-muted/30 px-6 text-center"
				style:padding-bottom={`${bottomInset}px`}
			>
				<div
					class="flex size-10 items-center justify-center rounded-full bg-muted text-muted-foreground"
				>
					<MapPin class="size-5" aria-hidden="true" />
				</div>
				<p class="text-sm font-medium">
					{spatial
						? 'No verified location for this school yet'
						: 'The school map is unavailable right now'}
				</p>
				<p class="max-w-sm text-xs text-muted-foreground">
					{spatial
						? 'A verified map location can be added from the overview map.'
						: 'School results are still available below.'}
				</p>
			</div>
		{/if}
	</div>
</section>

<style>
	/* Keep filters on the tile canvas so the marker and grounds retain their colours. */
	.school-location-map :global(.school-hero-basemap canvas) {
		filter: grayscale(1) contrast(1.05);
	}

	:global(.dark) .school-location-map :global(.school-hero-basemap canvas) {
		filter: grayscale(1) invert(1) brightness(0.9) contrast(0.85);
	}

	/* Tint both raster basemaps so the hero map sits inside the page palette:
	   a theme-hued wash with a soft vignette around the school, which also
	   darkens the edges so the profile card and attribution stay legible. */
	.school-location-map :global(.school-hero-basemap)::after,
	.school-location-map :global(.school-hero-imagery)::after {
		content: '';
		position: absolute;
		inset: 0;
		pointer-events: none;
		background:
			radial-gradient(
				ellipse 120% 100% at 72% 45%,
				transparent 28%,
				hsl(var(--theme-hue) 20% 10% / 0.45) 100%
			),
			linear-gradient(
				to bottom,
				hsl(var(--theme-hue) 45% 32% / 0.26),
				hsl(var(--theme-hue) 45% 32% / 0.1)
			);
	}

	.school-location-map :global(.ol-control) {
		background: hsl(var(--background) / 0.9);
		color: hsl(var(--foreground));
		border: 0;
		border-radius: var(--radius);
	}

	.school-location-map :global(.ol-attribution) {
		bottom: calc(var(--school-map-inset, 0px) + 4px);
		right: 0.5rem;
		font-size: 10px;
	}

	@media (min-width: 1024px) {
		.school-location-map :global(.ol-attribution) {
			bottom: 0.5rem;
		}
	}

	.school-location-map :global(.ol-attribution ul) {
		color: hsl(var(--foreground));
		text-shadow: none;
	}

	.school-location-map :global(.ol-attribution a) {
		color: hsl(var(--primary));
	}

	@media (prefers-reduced-motion: reduce) {
		.school-location-map :global(.school-hero-basemap canvas) {
			transition: none;
		}
	}
</style>
