import Feature from 'ol/Feature';
import GeoJSON from 'ol/format/GeoJSON';
import type Geometry from 'ol/geom/Geometry';
import type OlMap from 'ol/Map';
import VectorSource from 'ol/source/Vector';
import VectorLayer from 'ol/layer/Vector';
import { Draw, Modify, Snap } from 'ol/interaction';
import Collection from 'ol/Collection';
import { spatialMapStyle } from './school-map-style';
import { getArea, getDistance } from 'ol/sphere';
import type {
	SpatialBoundary,
	SpatialPoint,
	SpatialSave,
	SpatialState
} from '$lib/api/school-spatial-contract';

const format = new GeoJSON();
const projections = { dataProjection: 'EPSG:4326', featureProjection: 'EPSG:3857' };
export type EditTool = 'draw' | 'grounds' | 'marker';

/** State the map toolbar needs to render and disable editing actions. */
export interface SpatialEditorStatus {
	activeTool: EditTool | null;
	hasDraft: boolean;
	hasBoundary: boolean;
	hasMarker: boolean;
	dirty: boolean;
	saving: boolean;
	validationCount: number;
}

/** Imperative editing actions exposed to the map toolbar. */
export interface SpatialEditorHandle {
	selectTool(tool: EditTool): void;
	saveDraft(): void;
	cancelEdit(): void;
}
export function readSpatialGeometry(geometry: SpatialBoundary | SpatialPoint): Geometry {
	const result = format.readGeometry(geometry, projections);
	if (!result) throw new Error('The school map contains unsupported geometry.');
	return result;
}
export function writeSpatialGeometry(geometry: Geometry): SpatialBoundary | SpatialPoint {
	return format.writeGeometryObject(geometry, projections) as SpatialBoundary | SpatialPoint;
}
export function spatialDraft(state: SpatialState): SpatialSave {
	return {
		expectedRevision: state.revision,
		boundary: structuredClone(state.boundary),
		locationMode: state.hasLocationOverride ? 'override' : 'imported',
		location: state.hasLocationOverride ? structuredClone(state.location) : null,
		reason: '',
		sourceDescription: ''
	};
}

export function spatialDraftWarnings(draft: SpatialSave, original: SpatialState): string[] {
	const warnings: string[] = [];
	const point = draft.locationMode === 'override' ? draft.location : original.location;
	if (draft.boundary && point) {
		const grounds = readSpatialGeometry(draft.boundary);
		const location = readSpatialGeometry(point).getExtent();
		if (!grounds.intersectsCoordinate([location[0]!, location[1]!]))
			warnings.push(
				'The marker is outside the grounds. Explain the source discrepancy in your reason.'
			);
	}
	if (
		draft.locationMode === 'override' &&
		draft.location &&
		original.location &&
		getDistance(draft.location.coordinates, original.location.coordinates) > 1000
	)
		warnings.push(
			'The marker moved more than 1 km. Check the school identity and explain this correction.'
		);
	return warnings;
}

function ringSelfIntersects(ring: number[][]): boolean {
	const cross = (a: number[], b: number[], c: number[]) =>
		((b[0] ?? 0) - (a[0] ?? 0)) * ((c[1] ?? 0) - (a[1] ?? 0)) -
		((b[1] ?? 0) - (a[1] ?? 0)) * ((c[0] ?? 0) - (a[0] ?? 0));
	// Stop quadratic checks for large rings; the server validates all topology.
	if (ring.length > 1000) return false;
	for (let i = 0; i < ring.length - 1; i++) {
		for (let j = i + 2; j < ring.length - 1; j++) {
			if (i === 0 && j === ring.length - 2) continue;
			const a = ring[i]!,
				b = ring[i + 1]!,
				c = ring[j]!,
				d = ring[j + 1]!;
			if (cross(a, b, c) * cross(a, b, d) < 0 && cross(c, d, a) * cross(c, d, b) < 0) return true;
		}
	}
	return false;
}

export function validateSpatialDraft(draft: SpatialSave): string[] {
	const errors: string[] = [];
	if (!draft.reason.trim()) errors.push('Enter a reason for this correction.');
	if (!draft.sourceDescription.trim()) errors.push('Describe the imagery or field evidence used.');
	if (draft.reason.length > 2000 || draft.sourceDescription.length > 1000)
		errors.push('Reason must be at most 2,000 characters and source at most 1,000.');
	let coordinates = 0;
	let rings = 0;
	const validPosition = (position: number[]) => {
		coordinates++;
		return (
			position.length === 2 &&
			position.every(Number.isFinite) &&
			Math.abs(position[0] ?? Infinity) <= 180 &&
			Math.abs(position[1] ?? Infinity) <= 90
		);
	};
	if (
		draft.locationMode === 'override' &&
		(!draft.location || !validPosition(draft.location.coordinates))
	)
		errors.push('Place a valid school marker.');
	if (draft.boundary) {
		const polygons =
			draft.boundary.type === 'Polygon' ? [draft.boundary.coordinates] : draft.boundary.coordinates;
		if (!polygons.length || polygons.length > 100)
			errors.push('Grounds must contain between 1 and 100 polygons.');
		for (const polygon of polygons) {
			if (!polygon.length) errors.push('A polygon needs an outer ring.');
			for (const ring of polygon) {
				rings++;
				if (!ring.every(validPosition)) errors.push('Grounds contain invalid coordinates.');
				if (
					ring.length < 4 ||
					new Set(ring.map((point) => point.join(','))).size < 3 ||
					JSON.stringify(ring[0]) !== JSON.stringify(ring.at(-1))
				)
					errors.push('Each ring needs three distinct vertices and must be closed.');
				let area = 0;
				for (let i = 1; i < ring.length; i++) {
					const a = ring[i - 1],
						b = ring[i];
					if (a && b) area += (a[0] ?? 0) * (b[1] ?? 0) - (b[0] ?? 0) * (a[1] ?? 0);
				}
				if (Math.abs(area) < 1e-12) errors.push('Grounds must have a nonzero area.');
				if (ringSelfIntersects(ring)) errors.push('Grounds must not cross themselves.');
			}
		}
	}
	if (coordinates > 10000 || rings > 100)
		errors.push('Grounds exceed the limit of 10,000 coordinates or 100 rings.');
	if (new TextEncoder().encode(JSON.stringify(draft)).length > 1_000_000)
		errors.push('The draft exceeds the 1 MB limit.');
	return [...new Set(errors)];
}

/** Owns only detached draft features. Published map sources are never modified here. */
export function createSpatialInteractions(
	map: OlMap,
	state: SpatialState,
	changed: (
		boundary: SpatialBoundary | null,
		point: SpatialPoint | null,
		markerEdited: boolean
	) => void,
	toolChanged: (tool: EditTool | null) => void = () => {}
) {
	const source = new VectorSource<Feature<Geometry>>();
	let boundary = state.boundary ? new Feature(readSpatialGeometry(state.boundary)) : null;
	let marker = state.location ? new Feature(readSpatialGeometry(state.location)) : null;
	if (boundary) source.addFeature(boundary);
	if (marker) source.addFeature(marker);
	const layer = new VectorLayer({
		source,
		zIndex: 20,
		style: spatialMapStyle(true)
	});
	map.addLayer(layer);
	const observer =
		typeof MutationObserver === 'undefined'
			? null
			: new MutationObserver(() => layer.setStyle(spatialMapStyle(true)));
	observer?.observe(document.documentElement, {
		attributes: true,
		attributeFilter: ['class', 'data-theme-preset', 'data-theme-chart']
	});
	let active: Draw | Modify | null = null;
	let snap: Snap | null = null;
	let markerEdited = false;
	let boundaryEdited = false;
	const notify = () =>
		changed(
			boundaryEdited
				? boundary?.getGeometry()
					? (writeSpatialGeometry(boundary.getGeometry()!) as SpatialBoundary)
					: null
				: structuredClone(state.boundary),
			marker?.getGeometry() ? (writeSpatialGeometry(marker.getGeometry()!) as SpatialPoint) : null,
			markerEdited
		);
	function stop() {
		if (active) map.removeInteraction(active);
		if (snap) map.removeInteraction(snap);
		active = null;
		snap = null;
		toolChanged(null);
	}
	function tool(tool: EditTool) {
		stop();
		if (tool === 'draw' || (tool === 'marker' && !marker)) {
			const draw = new Draw({ type: tool === 'draw' ? 'Polygon' : 'Point' });
			draw.on('drawend', (event) => {
				if (tool === 'draw') {
					boundaryEdited = true;
					if (boundary) source.removeFeature(boundary);
					boundary = event.feature;
				} else {
					marker = event.feature;
					markerEdited = true;
				}
				source.addFeature(event.feature);
				notify();
				// Remove Draw after its event dispatch has completed.
				queueMicrotask(stop);
			});
			active = draw;
		} else {
			const feature = tool === 'marker' ? marker : boundary;
			if (!feature) return;
			const modify = new Modify({ features: new Collection([feature]) });
			modify.on('modifyend', () => {
				if (tool === 'marker') markerEdited = true;
				else boundaryEdited = true;
				notify();
			});
			active = modify;
		}
		map.addInteraction(active);
		snap = new Snap({ source });
		map.addInteraction(snap);
		toolChanged(tool);
	}
	const geometry = boundary?.getGeometry() ?? marker?.getGeometry();
	if (geometry)
		map
			.getView()
			.fit(geometry.getExtent(), { maxZoom: 18, padding: [70, 70, 70, 70], duration: 200 });
	return {
		tool,
		stop,
		area: () => (boundary?.getGeometry() ? getArea(boundary.getGeometry()!) : null),
		destroy: () => {
			observer?.disconnect();
			stop();
			map.removeLayer(layer);
		}
	};
}
