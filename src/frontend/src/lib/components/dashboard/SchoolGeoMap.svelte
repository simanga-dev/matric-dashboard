<script lang="ts">
	import { onMount, onDestroy, tick } from 'svelte';
	import { page } from '$app/state';
	import { hasPermission, Permissions } from '$lib/utils/permissions';
	import { mapImagery, attributionHtml } from './school-map-imagery';
	import { selectionWhileEditing } from './school-map-selection';
	import { applyCanonicalSpatial, loadPublishedSchoolLocations } from './school-map-publication';
	import { spatialMapStyle } from './school-map-style';
	import { createViewportLoader, regionLayerUrl, schoolLayerUrl } from './school-map-loading';
	import type { SpatialState } from '$lib/api/school-spatial-contract';
	import { refreshAuthoritativeSpatialStates, savedSpatialStates } from '$lib/api/school-spatial';
	import { invalidateGeoLayer } from '$lib/state/geo-data';
	import {
		isOverlayOpen,
		registerShortcutHandler,
		ShortcutAction
	} from '$lib/state/shortcuts.svelte';
	import 'ol/ol.css';
	import { resolve } from '$app/paths';
	import { Button } from '$lib/components/ui/button';
	import { schoolPath } from '$lib/config';
	import type { School } from '$lib/types/dashboard';
	import { schoolMapGeoJson, schoolMapPopup } from './school-map-data';
	import type Feature from 'ol/Feature';
	import type { FeatureLike } from 'ol/Feature';
	import type { StyleFunction } from 'ol/style/Style';
	import type OlMap from 'ol/Map';
	import type VectorLayer from 'ol/layer/Vector';
	import {
		Maximize,
		Home,
		AlertCircle,
		MapPin,
		ExternalLink,
		Pencil,
		PenLine,
		Spline,
		Save,
		Copy,
		Check,
		X,
		Layers2,
		ChevronsLeft,
		ChevronsRight
	} from '@lucide/svelte';
	import type { EditTool, SpatialEditorHandle, SpatialEditorStatus } from './school-map-editing';
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
	 * Effective coordinates include application-owned editor overrides.
	 *
	 * Selection is keyed on the EMIS number, which is the same identifier the
	 * school tree uses (SchoolOutput.Id), so selecting a school in the tree
	 * highlights and zooms to it here - and vice versa.
	 */
	const WFS_URL = schoolLayerUrl('matric:school_locations_effective');
	const LEGACY_WFS_URL = schoolLayerUrl('matric:school_locations');
	let canEdit = $derived(hasPermission(page.data.user, Permissions.Schools.SpatialEdit));
	let imageryConfig = $derived(
		mapImagery(page.data.mapImageryUrl, page.data.mapImageryAttribution)
	);
	let imagerySelected = $state(false);
	let imageryMessage = $state('');
	let editing = $state<{ emis: string; name: string; initialTool?: EditTool } | null>(null);
	let toolbarCollapsed = $state(false);
	let layersPanelOpen = $state(false);
	let toolbarWidth = $state(0);
	let editorRef = $state<SpatialEditorHandle | null>(null);
	let editorStatus = $state<SpatialEditorStatus | null>(null);
	let selectionMessage = $state('');
	let mapReady = $state(false);
	let switchBackground: ((imagery: boolean) => void) | null = null;
	let updateCanonical: ((state: SpatialState) => void) | null = null;
	let refreshSpatialLayers: (() => Promise<void>) | null = null;

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
	let usingImportedLocations = $state(false);
	let popup = $state<SchoolPopup | null>(null);
	let copiedPopupId = $state<string | null>(null);
	let copyTimer: ReturnType<typeof setTimeout> | null = null;
	let selectedLocationMissing = $state(false);

	// OpenLayers objects are held outside $state to avoid Svelte's deep proxy
	// wrapping them (which breaks OpenLayers internals).
	let olMap = $state.raw<OlMap | null>(null);
	let vecLayer: VectorLayer | null = null;
	let detailLayer: VectorLayer | null = null;
	let cancelLayerLoads: (() => void) | null = null;
	let mapGeneration = 0;
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
	function applyRegionFocus(fit = true) {
		if (editing) return;
		highlightProvince = selectedProvince;
		highlightDistrict = selectedDistrict;
		highlightCountry = !selectedSchoolId && !selectedProvince && !selectedDistrict;
		for (const layer of boundaryLayers) layer.changed();
		if (!olMap || !fit) return;

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

	/** Canvas colours need an explicit alpha channel for translucent footprints. */
	function withAlpha(color: string, alpha: number): string {
		const hsl = color.match(/^hsl\(\s*([\d.]+)\s+([\d.]+%)\s+([\d.]+%)\s*\)$/);
		if (hsl) return `hsla(${hsl[1]}, ${hsl[2]}, ${hsl[3]}, ${alpha})`;
		if (/^#[0-9a-f]{6}$/i.test(color)) {
			const value = Number.parseInt(color.slice(1), 16);
			return `rgba(${(value >> 16) & 255}, ${(value >> 8) & 255}, ${value & 255}, ${alpha})`;
		}
		return color;
	}

	async function copySchoolName() {
		if (!popup) return;
		const name = toTitleCase(popup.name);
		try {
			if (navigator.clipboard?.writeText) {
				await navigator.clipboard.writeText(name);
			} else {
				const area = document.createElement('textarea');
				area.value = name;
				area.setAttribute('readonly', '');
				area.style.position = 'fixed';
				area.style.opacity = '0';
				document.body.appendChild(area);
				area.select();
				const copied = document.execCommand('copy');
				area.remove();
				if (!copied) throw new Error('Copy failed');
			}
			copiedPopupId = popup.id;
			if (copyTimer) clearTimeout(copyTimer);
			copyTimer = setTimeout(() => (copiedPopupId = null), 1500);
		} catch {
			copiedPopupId = null;
		}
	}

	function findFeature(emis: string): FeatureLike | null {
		for (const layer of [detailLayer, vecLayer]) {
			const feature = layer
				?.getSource()
				?.getFeatures()
				.find((f) => String(f.get('emis_number')) === emis);
			if (feature) return feature;
		}
		return null;
	}

	/** Applies the selection: restyle, and pan/zoom to the selected school. */
	function applySelection(emis: string | null) {
		if (!vecLayer) return;
		if (selectedEmis === emis) return;
		selectedEmis = emis;
		vecLayer?.changed();
		detailLayer?.changed();
		popup = null;
		hidePopup?.();
		selectedLocationMissing = false;

		if (!emis || !olMap) return;
		const feature = findFeature(emis);
		const extent = feature?.getGeometry()?.getExtent();
		const coord = extent ? [(extent[0]! + extent[2]!) / 2, (extent[1]! + extent[3]!) / 2] : null;
		if (!coord || !coord.every(Number.isFinite)) {
			selectedLocationMissing = true;
			return;
		}

		const view = olMap.getView();
		// A new click replaces the current pan rather than competing with it.
		view.cancelAnimations();
		view.animate({
			center: coord,
			zoom: Math.max(view.getZoom() ?? 6, 16),
			duration: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 0 : 450
		});
	}

	$effect(() => {
		const allowed = selectionWhileEditing(editing?.emis ?? null, selectedSchoolId);
		if (allowed !== selectedSchoolId) {
			selectedSchoolId = allowed;
			selectionMessage = 'School selection is locked. Save or Cancel the current school map first.';
			return;
		}
		applySelection(selectedSchoolId);
	});

	$effect(() => {
		applyRegionFocus();
	});

	onDestroy(() => {
		disposed = true;
		mapGeneration += 1;
		cancelLayerLoads?.();
		if (copyTimer) clearTimeout(copyTimer);
		resizeObserver?.disconnect();
		themeObserver?.disconnect();
		olMap?.setTarget(undefined);
	});

	async function loadMap() {
		const generation = ++mapGeneration;
		const active = () => !disposed && generation === mapGeneration;
		cancelLayerLoads?.();
		cancelLayerLoads = null;
		mapReady = false;
		resizeObserver?.disconnect();
		themeObserver?.disconnect();
		themeObserver = null;
		olMap?.setTarget(undefined);
		olMap = null;
		vecLayer = null;
		detailLayer = null;
		boundaryLayers = [];
		provinceFeatures = [];
		districtFeatures = [];
		selectedEmis = null;
		popup = null;
		resetView = null;
		fitSchools = null;
		loading = true;
		error = null;
		usingImportedLocations = false;
		try {
			// A missing corrected layer must not hide imported school points or
			// prevent adding a missing marker.
			const schoolsPending = loadPublishedSchoolLocations(loadGeoJson, WFS_URL, LEGACY_WFS_URL);

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
				{ defaults: defaultInteractions },
				schoolsLayer
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
				import('ol/interaction/defaults'),
				schoolsPending
			]);

			const { fromLonLat } = proj;
			if (!active()) return;
			usingImportedLocations = schoolsLayer.fallback;
			const geojson = schoolsLayer.geojson;
			const { Style, Fill, Stroke } = style;

			const reader = new GeoJSON();
			const source = new VectorSource();
			const footprintsSource = new VectorSource();
			const groundsSource = new VectorSource();
			// eslint-disable-next-line svelte/prefer-svelte-reactivity -- OpenLayers redraws explicitly via layer.changed().
			let detailedEmis = new Set<string>();
			const features = reader.readFeatures(schoolMapGeoJson(geojson, schools), {
				dataProjection: 'EPSG:4326',
				featureProjection: 'EPSG:3857'
			});
			source.addFeatures(features);
			featureCount = features.length;
			updateCanonical = (state) => {
				applyCanonicalSpatial(reader, source, groundsSource, state, schools);
				// An editor/API state always wins over an older viewport response.
				for (const feature of footprintsSource.getFeatures()) {
					if (String(feature.get('emis_number')) === state.emisNumber)
						footprintsSource.removeFeature(feature);
				}
				detailedEmis.delete(state.emisNumber);
				featureCount = source.getFeatures().length;
				vecLayer?.changed();
				if (selectedSchoolId === state.emisNumber) selectedLocationMissing = !state.location;
			};
			const reapplyCanonical = () => {
				for (const state of savedSpatialStates.values()) updateCanonical?.(state);
			};
			const revalidateCanonical = async () => {
				for (const state of await refreshAuthoritativeSpatialStates()) {
					if (active()) updateCanonical?.(state);
				}
			};
			const viewportLoader = createViewportLoader(
				(url, signal) => loadGeoJson(url, { cache: false, signal }),
				(footprints, grounds) => {
					if (!active()) return;
					if (footprints) {
						footprintsSource.clear();
						footprintsSource.addFeatures(
							reader.readFeatures(schoolMapGeoJson(footprints, schools), {
								dataProjection: 'EPSG:4326',
								featureProjection: 'EPSG:3857'
							})
						);
						detailedEmis = new Set(
							footprintsSource.getFeatures().map((f) => String(f.get('emis_number')))
						);
					}
					if (grounds) {
						groundsSource.clear();
						groundsSource.addFeatures(
							reader.readFeatures(grounds, {
								dataProjection: 'EPSG:4326',
								featureProjection: 'EPSG:3857'
							})
						);
					}
					reapplyCanonical();
					vecLayer?.changed();
				}
			);
			let viewportTimer: ReturnType<typeof setTimeout> | null = null;
			cancelLayerLoads = () => {
				viewportLoader.dispose();
				if (viewportTimer) clearTimeout(viewportTimer);
			};
			const loadViewport = async () => {
				if (!active() || !olMap) return;
				const view = olMap.getView();
				const extent = proj.transformExtent(
					view.calculateExtent(olMap.getSize()),
					'EPSG:3857',
					'EPSG:4326'
				);
				await viewportLoader.load(extent, view.getZoom() ?? 0);
			};
			refreshSpatialLayers = async () => {
				const schoolsLayer = await loadPublishedSchoolLocations(
					loadGeoJson,
					WFS_URL,
					LEGACY_WFS_URL
				);
				if (!active()) return;
				usingImportedLocations = schoolsLayer.fallback;
				source.clear();
				source.addFeatures(
					reader.readFeatures(schoolMapGeoJson(schoolsLayer.geojson, schools), {
						dataProjection: 'EPSG:4326',
						featureProjection: 'EPSG:3857'
					})
				);
				reapplyCanonical();
				await loadViewport();
				await revalidateCanonical();
			};

			// Administrative boundaries are optional: if the layer is missing the
			// school map still renders, just without region highlighting.
			const provinceSource = new VectorSource();
			const districtSource = new VectorSource();

			// The host stays mounted beneath the loading overlay so OpenLayers
			// measures the final panel dimensions before anything is revealed.
			await tick();

			if (!active() || !mapHost || !popupHost) {
				return;
			}

			// eslint-disable-next-line svelte/prefer-svelte-reactivity -- Canvas style reuse is not component state.
			const featureStyles = new Map<string, InstanceType<typeof Style>>();
			const styleFor: StyleFunction = (feature: FeatureLike) => {
				const emis = String(feature.get('emis_number'));
				const isSelected = selectedEmis !== null && emis === selectedEmis;
				const rate = passRate(Number(feature.get('passed')), Number(feature.get('wrote')));
				const key = `${passRateBand(rate)}:${isSelected}`;
				const existing = featureStyles.get(key);
				if (existing) return existing;

				const result = new Style({
					zIndex: isSelected ? 2 : 1,
					fill: new Fill({
						color: withAlpha(markerColor(rate), isSelected ? 0.35 : 0.22)
					}),
					stroke: new Stroke({
						color: isSelected ? '#111827' : '#ffffff',
						width: isSelected ? 3 : 2
					})
				});
				featureStyles.set(key, result);
				return result;
			};

			// Boundary outlines are always white: the basemap is grayscaled in the
			// light theme and inverted in the dark one, so any theme-derived colour
			// would read as white in one theme and black in the other. Without an
			// explicit style OpenLayers also falls back to its blue default.
			const provinceOutline = new Style({
				stroke: new Stroke({ color: '#ffffff', width: 2.5 })
			});
			const districtOutline = new Style({
				stroke: new Stroke({ color: '#ffffff', width: 1.25 })
			});
			const regionHighlight = new Style({
				fill: new Fill({ color: 'rgba(29, 78, 216, 0.2)' }),
				stroke: new Stroke({ color: '#ffffff', width: 2.5 })
			});

			const provinceStyle: StyleFunction = (feature: FeatureLike) => {
				if (highlightCountry) return regionHighlight;
				if (highlightDistrict) return provinceOutline;
				return sameName(feature.get('name'), highlightProvince) ? regionHighlight : provinceOutline;
			};

			const districtStyle: StyleFunction = (feature: FeatureLike) => {
				if (!highlightDistrict) return districtOutline;
				return sameName(feature.get('name'), highlightDistrict) &&
					sameName(feature.get('province'), highlightProvince)
					? regionHighlight
					: districtOutline;
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
			const streetLayer = new TileLayer({ source: new OSM(), className: 'school-basemap' });
			const imageryLayer = new TileLayer({ visible: false, className: 'school-imagery' });
			if (imageryConfig) {
				const { default: XYZ } = await import('ol/source/XYZ');
				if (!active()) return;
				const imagerySource = new XYZ({
					url: imageryConfig.url,
					attributions: attributionHtml(imageryConfig.attribution)
				});
				imageryLayer.setSource(imagerySource);
				imagerySource.on('tileloaderror', () => {
					imagerySelected = false;
					imageryMessage = 'Imagery tiles could not be loaded. Showing OpenStreetMap instead.';
					streetLayer.setVisible(true);
					imageryLayer.setVisible(false);
				});
			}
			switchBackground = (imagery) => {
				imagerySelected = imagery;
				imageryMessage = '';
				streetLayer.setVisible(!imagery);
				imageryLayer.setVisible(imagery);
			};
			const pointsLayer = new VectorLayerCtor({
				source,
				style: (feature, resolution) =>
					detailedEmis.has(String(feature.get('emis_number')))
						? undefined
						: styleFor(feature, resolution)
			});
			const footprintsLayer = new VectorLayerCtor({ source: footprintsSource, style: styleFor });
			const groundsLayer = new VectorLayerCtor({
				source: groundsSource,
				style: spatialMapStyle(false)
			});

			olMap = new OlMapCtor({
				target: mapHost,
				controls: defaultControls({ rotate: false, attributionOptions: { collapsible: false } }),
				// Accept wheel zoom over the map without requiring a click or stealing focus.
				interactions: defaultInteractions({ onFocusOnly: false }),
				layers: [
					// Theme only the raster background, never the performance markers.
					streetLayer,
					imageryLayer,
					provinceLayer,
					districtLayer,
					groundsLayer,
					pointsLayer,
					footprintsLayer
				],
				view
			});
			// A rendered frame is enough; do not wait for every remote raster tile.
			olMap.once('postrender', () => {
				if (active()) loading = false;
			});
			vecLayer = pointsLayer;
			detailLayer = footprintsLayer;
			mapReady = true;

			// Marker colours are resolved from theme variables for the SVG data
			// URL, so restyle when the palette, scheme or chart colour changes.
			themeObserver = new MutationObserver(() => {
				markerColorCache = {};
				featureStyles.clear();
				vecLayer?.changed();
				detailLayer?.changed();
				groundsLayer.setStyle(spatialMapStyle(false));
			});
			themeObserver.observe(document.documentElement, {
				attributes: true,
				attributeFilter: ['class', 'data-theme-preset', 'data-theme-chart']
			});

			// Cached editor saves must affect the initial fit and school selection too.
			reapplyCanonical();
			fitSchools = () => {
				const extent = source.getExtent();
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
				if (editing) return;
				const hit = olMap?.forEachFeatureAtPixel(event.pixel, (f) => f, {
					layerFilter: (layer) => layer === vecLayer || layer === detailLayer
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
			const initialCenter = [...(view.getCenter() ?? [])];
			const initialResolution = view.getResolution();
			void revalidateCanonical();
			olMap.on('moveend', () => {
				if (viewportTimer) clearTimeout(viewportTimer);
				viewportTimer = setTimeout(() => void loadViewport(), 200);
			});
			void loadViewport();
			// Optional outlines arrive independently, after the interactive map exists.
			for (const [typeName, regionSource] of [
				['matric:provinces', provinceSource],
				['matric:districts', districtSource]
			] as const) {
				void loadGeoJson(regionLayerUrl(typeName))
					.then((geo) => {
						if (!active()) return;
						const features = reader.readFeatures(geo, {
							dataProjection: 'EPSG:4326',
							featureProjection: 'EPSG:3857'
						});
						regionSource.addFeatures(features);
						if (typeName === 'matric:provinces') provinceFeatures = features;
						else districtFeatures = features;
						const viewUnchanged =
							view.getResolution() === initialResolution &&
							view.getCenter()?.every((value, index) => value === initialCenter[index]);
						applyRegionFocus(
							!selectedSchoolId &&
								(Boolean(selectedProvince || selectedDistrict) || Boolean(viewUnchanged))
						);
					})
					.catch(() => {
						/* Optional boundaries must not block the school map. */
					});
			}
		} catch (e) {
			if (active()) {
				error =
					e instanceof GeoLayerUnavailableError
						? 'School map layer is not available. Publish it with scripts/geoserver/bootstrap.sh.'
						: e instanceof Error
							? e.message
							: 'Failed to load school locations.';
			}
			if (active()) loading = false;
		}
	}

	onMount(() => {
		void loadMap();
		return registerShortcutHandler(ShortcutAction.OpenMapEditor, requestMapEditor);
	});
	/** Opens the editor in drawing mode from the E shortcut when a school is selected and editable. */
	function requestMapEditor() {
		if (editing || !selectedSchoolId || !mapReady || !canEdit) return;
		if (isOverlayOpen()) return;
		openEditor('draw-footprint');
	}

	function toolbarButtonClass(
		active = false,
		tone: 'neutral' | 'save' | 'cancel' | 'edit' = 'neutral'
	) {
		const toneClass =
			tone === 'save'
				? 'text-foreground hover:bg-success-emphasis/10 hover:text-success-emphasis dark:hover:bg-success-emphasis/20'
				: tone === 'cancel'
					? 'text-foreground hover:bg-destructive-emphasis/10 hover:text-destructive-emphasis dark:hover:bg-destructive-emphasis/20'
					: tone === 'edit'
						? 'text-success-emphasis hover:bg-success-emphasis/10 dark:hover:bg-success-emphasis/20'
						: 'text-foreground hover:bg-muted';
		const activeClass =
			tone === 'edit'
				? 'bg-success-emphasis/15 text-success-emphasis dark:bg-success-emphasis/25'
				: 'bg-accent text-accent-foreground';
		return `flex size-9 shrink-0 items-center justify-center rounded-md transition-colors pointer-coarse:size-11 ${toneClass} focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none disabled:pointer-events-none disabled:opacity-40 ${active ? activeClass : ''}`;
	}
	function chooseTool(tool: EditTool) {
		editorRef?.selectTool(tool);
	}
	function toggleMapPanel() {
		if (editing) {
			if (editorRef) editorRef.cancelEdit();
			else closeEditor();
			return;
		}
		if (layersPanelOpen) {
			layersPanelOpen = false;
			return;
		}
		openEditor();
		if (!editing) layersPanelOpen = true;
	}
	function openEditor(initialTool?: EditTool) {
		layersPanelOpen = false;
		if (!selectedSchoolId || !canEdit) return;
		const school = schools.find((school) => school.id === selectedSchoolId);
		editorStatus = null;
		editorRef = null;
		editing = { emis: selectedSchoolId, name: school?.name ?? selectedSchoolId, initialTool };
		popup = null;
		hidePopup?.();
		selectionMessage = '';
	}
	function closeEditor() {
		layersPanelOpen = false;
		editing = null;
		editorRef = null;
		editorStatus = null;
		selectionMessage = '';
	}
	function toggleToolbar() {
		if (toolbarCollapsed) {
			toolbarCollapsed = false;
			return;
		}
		if (editing) {
			// Close the editor with the toolbar, respecting unsaved-change confirmation.
			if (editorRef) editorRef.cancelEdit();
			else closeEditor();
			if (editing) return;
		}
		layersPanelOpen = false;
		toolbarCollapsed = true;
	}
	function saved(state: SpatialState) {
		updateCanonical?.(state);
		invalidateGeoLayer('matric:school_locations_effective');
		invalidateGeoLayer('matric:school_footprints');
		invalidateGeoLayer('matric:school_boundaries');
		void refreshSpatialLayers?.();
	}
</script>

<section
	aria-label="School locations"
	class="flex h-full min-h-0 flex-col bg-card text-card-foreground"
>
	{#if imageryMessage}<p role="status" class="p-2 text-xs">{imageryMessage}</p>{/if}
	{#if usingImportedLocations}<p role="status" class="p-2 text-xs text-muted-foreground">
			Showing imported school footprints until corrected school layers are published.
		</p>{/if}
	{#if selectionMessage && editing}<p role="status" class="p-2 text-xs">{selectionMessage}</p>{/if}
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
						role="toolbar"
						aria-label="Map tools"
						bind:clientWidth={toolbarWidth}
						class="absolute end-2 top-2 z-20 flex max-w-[calc(100%-1rem)] items-start {toolbarCollapsed
							? ''
							: 'gap-0.5'} rounded-lg bg-background/85 p-0.5 text-xs shadow-md backdrop-blur"
					>
						<div
							class="grid transition-[grid-template-columns] duration-200 ease-out motion-reduce:transition-none {toolbarCollapsed
								? 'grid-cols-[0fr]'
								: 'grid-cols-[1fr]'}"
							inert={toolbarCollapsed}
						>
							<div
								class="flex min-w-0 flex-nowrap items-center justify-end gap-0.5 overflow-hidden transition-opacity duration-150 motion-reduce:transition-none"
								class:opacity-0={toolbarCollapsed}
							>
								<button
									type="button"
									aria-label="Map layers"
									title="Map layers"
									aria-expanded={layersPanelOpen || !!editing}
									onclick={toggleMapPanel}
									class={toolbarButtonClass(layersPanelOpen || !!editing)}
								>
									<Layers2 class="size-4" />
								</button>
								<button
									type="button"
									aria-label="Fit all school locations"
									title="Fit all school locations"
									onclick={() => fitSchools?.()}
									class={toolbarButtonClass()}><Maximize class="size-4" /></button
								>
								<button
									type="button"
									aria-label="Reset map to full view"
									title="Reset map to full view"
									onclick={() => resetView?.()}
									class={toolbarButtonClass()}><Home class="size-4" /></button
								>
								{#if canEdit}
									<span class="mx-0.5 h-6 w-px bg-border" aria-hidden="true"></span>
									<div
										class="grid transition-[grid-template-columns] duration-200 ease-out motion-reduce:transition-none {editing
											? 'grid-cols-[0fr]'
											: 'grid-cols-[1fr]'}"
									>
										<div class="min-w-0 overflow-hidden" inert={!!editing}>
											<div
												class="flex items-center transition-opacity duration-200 motion-reduce:transition-none"
												class:opacity-0={editing}
											>
												<button
													type="button"
													aria-label="Edit school map"
													title="Edit school map (E)"
													disabled={!selectedSchoolId || !mapReady}
													onclick={() => openEditor()}
													class={toolbarButtonClass(false, 'edit')}
													><Pencil class="size-4" /></button
												>
											</div>
										</div>
									</div>
									<div
										class="grid transition-[grid-template-columns] duration-200 ease-out motion-reduce:transition-none {editing
											? 'grid-cols-[1fr]'
											: 'grid-cols-[0fr]'}"
									>
										<div class="min-w-0 overflow-hidden" inert={!editing}>
											<div
												class="flex items-center gap-0.5 transition-opacity duration-200 motion-reduce:transition-none"
												class:opacity-0={!editing}
											>
												<button
													type="button"
													aria-label={editorStatus?.hasFootprint
														? 'Redraw school grounds'
														: 'Draw school grounds'}
													title={editorStatus?.hasFootprint
														? 'Redraw school grounds'
														: 'Draw school grounds'}
													aria-pressed={editorStatus?.activeTool === 'draw-footprint'}
													disabled={!editorStatus?.hasDraft || editorStatus.saving}
													onclick={() => chooseTool('draw-footprint')}
													class={toolbarButtonClass(
														editorStatus?.activeTool === 'draw-footprint',
														'edit'
													)}><PenLine class="size-4" /></button
												>
												<button
													type="button"
													aria-label="Edit school grounds"
													title="Edit school grounds"
													aria-pressed={editorStatus?.activeTool === 'footprint'}
													disabled={!editorStatus?.hasDraft ||
														!editorStatus.hasFootprint ||
														editorStatus.saving}
													onclick={() => chooseTool('footprint')}
													class={toolbarButtonClass(editorStatus?.activeTool === 'footprint')}
													><Spline class="size-4" /></button
												>
												<button
													type="button"
													aria-label={editorStatus?.hasMarker ? 'Move marker' : 'Add marker'}
													title={editorStatus?.hasMarker ? 'Move marker' : 'Add marker'}
													aria-pressed={editorStatus?.activeTool === 'marker'}
													disabled={!editorStatus?.hasDraft || editorStatus.saving}
													onclick={() => chooseTool('marker')}
													class={toolbarButtonClass(editorStatus?.activeTool === 'marker')}
													><MapPin class="size-4" /></button
												>
												<span class="mx-0.5 h-6 w-px bg-border" aria-hidden="true"></span>
												<button
													type="button"
													aria-label="Save school map"
													title="Save school map"
													disabled={!editorStatus?.hasDraft ||
														!editorStatus.dirty ||
														editorStatus.validationCount > 0 ||
														editorStatus.saving}
													onclick={() => editorRef?.saveDraft()}
													class={toolbarButtonClass(false, 'save')}><Save class="size-4" /></button
												>
												<button
													type="button"
													aria-label="Cancel"
													title="Cancel school map editing"
													disabled={editorStatus?.saving}
													onclick={() => editorRef?.cancelEdit()}
													class={toolbarButtonClass(false, 'cancel')}><X class="size-4" /></button
												>
											</div>
										</div>
									</div>
								{/if}
							</div>
						</div>
						<button
							type="button"
							aria-label={toolbarCollapsed ? 'Expand map toolbar' : 'Collapse map toolbar'}
							title={toolbarCollapsed ? 'Expand map toolbar' : 'Collapse map toolbar'}
							aria-expanded={!toolbarCollapsed}
							onclick={toggleToolbar}
							class={toolbarButtonClass()}
						>
							{#if toolbarCollapsed}
								<ChevronsLeft class="size-4" />
							{:else}
								<ChevronsRight class="size-4" />
							{/if}
						</button>
					</div>
					{#if featureCount === 0}<div
							role="status"
							class="pointer-events-none absolute inset-x-4 top-24 rounded-md border bg-background/95 p-3 text-sm shadow-sm"
						>
							No school locations are available yet. Use the directory to view school results.
						</div>{/if}
					{#if selectedLocationMissing && featureCount > 0}<div
							role="status"
							class="pointer-events-none absolute inset-x-4 top-24 rounded-md border bg-background/95 p-3 text-xs shadow-sm"
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
							<div class="flex items-start gap-2">
								<div class="min-w-0 flex-1">
									<div class="truncate text-sm font-semibold">{toTitleCase(popup.name)}</div>
									<div class="mt-0.5 flex items-center gap-1 text-xs text-muted-foreground">
										<MapPin class="size-3 shrink-0" />
										<span class="truncate">{toTitleCase(popup.province)}</span>
									</div>
								</div>
								<button
									type="button"
									aria-label={copiedPopupId === popup.id
										? 'School name copied'
										: 'Copy school name'}
									title="Copy school name"
									onclick={copySchoolName}
									class="pointer-events-auto -me-2 -mt-2 flex size-11 shrink-0 items-center justify-center rounded-md text-muted-foreground hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
								>
									{#if copiedPopupId === popup.id}
										<Check class="size-4" aria-hidden="true" />
									{:else}
										<Copy class="size-4" aria-hidden="true" />
									{/if}
								</button>
							</div>
							<span class="sr-only" aria-live="polite"
								>{copiedPopupId === popup.id ? 'School name copied' : ''}</span
							>

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
					{#if layersPanelOpen || (editing && olMap && canEdit)}
						<div
							class="absolute end-2 top-14 z-20 flex max-h-[min(26rem,calc(100%_-_4rem))] w-[min(26rem,calc(100%-1rem))] flex-col overflow-hidden rounded-lg border bg-background/85 text-sm shadow-lg backdrop-blur"
							style:width={toolbarWidth ? `max(20rem, ${toolbarWidth}px)` : undefined}
						>
							<div class="min-h-0 overflow-y-auto">
								<fieldset class="m-3 rounded-md border p-2 text-xs">
									<legend class="px-1 font-medium">Map layers</legend>
									<div class="flex flex-wrap gap-4">
										<label class="flex cursor-pointer items-center gap-2">
											<input
												type="radio"
												name="editor-map-background"
												value="street"
												checked={!imagerySelected}
												onchange={() => switchBackground?.(false)}
											/>
											Street map
										</label>
										<label class="flex items-center gap-2" class:opacity-50={!imageryConfig}>
											<input
												type="radio"
												name="editor-map-background"
												value="imagery"
												checked={imagerySelected}
												disabled={!imageryConfig}
												onchange={() => switchBackground?.(true)}
											/>
											Approved imagery{imageryConfig ? '' : ' (not configured)'}
										</label>
									</div>
								</fieldset>
								{#if canEdit && !selectedSchoolId}
									<p class="px-3 pb-3 text-xs text-muted-foreground">
										Select a school to access the school map editing options.
									</p>
								{/if}
								{#if editing && olMap && canEdit}
									<div>
										{#await import('./SchoolMapEditor.svelte')}
											<p role="status" class="p-3">Opening school map editor…</p>
										{:then { default: SchoolMapEditor }}
											<SchoolMapEditor
												bind:this={editorRef}
												map={olMap}
												emis={editing.emis}
												name={editing.name}
												onloaded={(state) => updateCanonical?.(state)}
												onsaved={saved}
												onstatus={(status) => (editorStatus = status)}
												onclose={closeEditor}
												initialTool={editing.initialTool}
											/>
										{:catch}
											<p role="alert" class="p-3">
												The editor could not be loaded. <button
													type="button"
													class="underline"
													onclick={closeEditor}>Close editor</button
												>
											</p>
										{/await}
									</div>
								{/if}
							</div>
						</div>
					{/if}
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
