<script lang="ts">
	import { onMount, onDestroy } from 'svelte';
	import { beforeNavigate } from '$app/navigation';
	import { Button } from '$lib/components/ui/button';
	import type OlMap from 'ol/Map';
	import { fromLonLat } from 'ol/proj';
	import {
		fetchSchoolSpatial,
		fetchSchoolSpatialHistory,
		saveSchoolSpatial
	} from '$lib/api/school-spatial';
	import type { SpatialHistory, SpatialSave, SpatialState } from '$lib/api/school-spatial-contract';
	import {
		createSpatialInteractions,
		spatialDraft,
		spatialDraftWarnings,
		validateSpatialDraft,
		type EditTool,
		type SpatialEditorStatus
	} from './school-map-editing';
	import { parseMapCoordinates } from './school-map-coordinates';

	let {
		map,
		emis,
		name,
		onloaded,
		onsaved,
		onclose,
		onstatus
	}: {
		map: OlMap;
		emis: string;
		name: string;
		onloaded?: (state: SpatialState) => void;
		onsaved: (state: SpatialState) => void;
		onclose: () => void;
		onstatus?: (status: SpatialEditorStatus) => void;
	} = $props();
	// Capture identity before any request. External tree changes cannot retarget a draft.
	const school = captureSchool();
	function captureSchool() {
		return { emis, name };
	}
	let draft = $state<SpatialSave | null>(null);
	let original = $state<SpatialState | null>(null);
	let dirty = $state(false);
	let loading = $state(true);
	let saving = $state(false);
	let error = $state('');
	let historyError = $state('');
	let history = $state<SpatialHistory[]>([]);
	let area = $state<number | null>(null);
	let activeTool = $state<EditTool | null>(null);
	let coordinateInput = $state('');
	let coordinateError = $state('');
	let coordinateStatus = $state('');
	let disposed = false;
	let interactions: ReturnType<typeof createSpatialInteractions> | null = null;
	let validation = $derived(draft ? validateSpatialDraft(draft) : []);
	let warnings = $derived([
		...(original?.warnings ?? []),
		...(draft && original ? spatialDraftWarnings(draft, original) : [])
	]);
	$effect(() => {
		onstatus?.({
			activeTool,
			hasDraft: Boolean(draft),
			hasBoundary: Boolean(draft?.boundary),
			hasFootprint: Boolean(original?.footprint || draft?.footprint),
			hasMarker: Boolean(original?.location || draft?.location),
			dirty,
			saving,
			validationCount: validation.length
		});
	});

	function canLeave() {
		return !saving && (!dirty || window.confirm('Discard unsaved school map changes?'));
	}
	beforeNavigate((navigation) => {
		if (saving || (dirty && !navigation.willUnload && !canLeave())) navigation.cancel();
	});
	function beforeUnload(event: BeforeUnloadEvent) {
		if (dirty || saving) {
			event.preventDefault();
			event.returnValue = '';
		}
	}
	function handleEscape(event: KeyboardEvent) {
		if (event.key !== 'Escape' || event.repeat || event.defaultPrevented || saving) return;
		// Let a focused select close its own dropdown before leaving the editor.
		if (event.target instanceof HTMLSelectElement) return;
		if (
			event.target instanceof Element &&
			event.target.closest('[role="dialog"], [role="menu"], [role="listbox"]')
		)
			return;
		event.preventDefault();
		if (canLeave()) onclose();
	}
	async function load() {
		loading = true;
		error = '';
		try {
			const state = await fetchSchoolSpatial(school.emis);
			if (disposed) return;
			onloaded?.(state);
			original = state;
			draft = spatialDraft(state);
			if (!coordinateInput) {
				const [longitude, latitude] = state.location?.coordinates ?? [];
				coordinateInput =
					typeof longitude === 'number' && typeof latitude === 'number'
						? `${latitude.toFixed(6)}, ${longitude.toFixed(6)}`
						: '';
			}
			interactions = createSpatialInteractions(
				map,
				state,
				(boundary, point, markerEdited, footprint, footprintEdited) => {
					if (!draft) return;
					draft.boundary = boundary;
					if (footprintEdited && footprint) draft.footprint = footprint;
					if (markerEdited) {
						draft.locationMode = 'override';
						draft.location = point;
					}
					dirty = true;
					area = interactions?.area() ?? null;
				},
				(tool) => (activeTool = tool)
			);
			area = interactions.area();
			void fetchSchoolSpatialHistory(school.emis)
				.then((entries) => {
					if (!disposed) history = entries;
				})
				.catch(() => {
					if (!disposed) historyError = 'Revision history is unavailable.';
				});
		} catch (cause) {
			if (!disposed)
				error =
					cause instanceof Error ? cause.message : 'School map service is unavailable. Try again.';
		} finally {
			if (!disposed) loading = false;
		}
	}
	function chooseTool(next: EditTool) {
		activeTool = next;
		interactions?.tool(next);
	}
	function locate(): boolean {
		const coordinate = parseMapCoordinates(coordinateInput);
		if (!coordinate) {
			coordinateError =
				'Could not read coordinates. Paste latitude, longitude or a full Google Maps URL.';
			coordinateStatus = '';
			return false;
		}
		coordinateError = '';
		coordinateStatus = `Centered on ${coordinate.latitude.toFixed(6)}, ${coordinate.longitude.toFixed(6)}.`;
		map.getView().animate({
			center: fromLonLat([coordinate.longitude, coordinate.latitude]),
			zoom: 18,
			duration: 300
		});
		return true;
	}
	function submitCoordinates(event: SubmitEvent) {
		event.preventDefault();
		locate();
	}
	function locateAndDraw() {
		if (locate()) chooseTool('draw-footprint');
	}
	/** Selects a drawing or marker tool from the map toolbar. */
	export function selectTool(next: EditTool) {
		chooseTool(next);
	}
	/** Publishes the current draft from the map toolbar. */
	export function saveDraft() {
		void save();
	}
	/** Closes the editor from the map toolbar, confirming unsaved work. */
	export function cancelEdit() {
		if (canLeave()) onclose();
	}
	async function save() {
		if (!draft || saving || !dirty || validation.length) return;
		saving = true;
		error = '';
		interactions?.stop();
		activeTool = null;
		try {
			const canonical = await saveSchoolSpatial(
				school.emis,
				structuredClone($state.snapshot(draft))
			);
			if (disposed) return;
			dirty = false;
			onsaved(canonical);
			onclose();
		} catch (cause) {
			if (!disposed)
				error =
					cause instanceof Error
						? cause.message
						: 'Save failed. Your draft is retained. Try again.';
		} finally {
			if (!disposed) saving = false;
		}
	}
	onMount(() => {
		window.addEventListener('beforeunload', beforeUnload);
		window.addEventListener('keydown', handleEscape);
		void load();
	});
	onDestroy(() => {
		disposed = true;
		interactions?.destroy();
		if (typeof window !== 'undefined') {
			window.removeEventListener('beforeunload', beforeUnload);
			window.removeEventListener('keydown', handleEscape);
		}
	});
</script>

<section aria-label="School map editor" class="space-y-3 p-3 text-sm">
	<div>
		<h3 class="font-semibold">Edit school map</h3>
		<p>{school.name} <span class="text-muted-foreground">EMIS {school.emis}</span></p>
	</div>
	<p class="text-xs text-muted-foreground">
		Use the map toolbar to draw or adjust the school grounds and marker, then Save or Cancel.
		Editor-traced grounds are not a surveyed cadastral boundary. School selection is locked while
		editing. Pan and zoom to find a school with no marker. Drawing and moving geometry require a
		pointer in this version. Keyboard users can pan and zoom the map, edit evidence, and use the
		toolbar Save and Cancel, but cannot edit vertices.
	</p>
	<form class="space-y-2" onsubmit={submitCoordinates}>
		<label for="coordinate-input" class="block text-xs font-medium"
			>Paste coordinates or a Google Maps link</label
		>
		<div class="flex flex-col gap-2">
			<input
				id="coordinate-input"
				aria-label="Paste coordinates or Google Maps link"
				autocomplete="off"
				spellcheck="false"
				bind:value={coordinateInput}
				placeholder="-30.86536, 29.85241"
				class="min-h-11 min-w-0 flex-1 rounded-md border border-input bg-background px-3 py-2 focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
			/>
			<div class="flex gap-2">
				<Button type="submit" variant="outline" class="min-h-11">Locate</Button>
				<Button type="button" class="min-h-11" disabled={loading || !draft} onclick={locateAndDraw}
					>Locate and draw</Button
				>
			</div>
		</div>
	</form>
	{#if coordinateError}<p role="alert" class="text-xs text-destructive">{coordinateError}</p>{/if}
	{#if coordinateStatus}<p role="status" class="text-xs text-muted-foreground">
			{coordinateStatus}
		</p>{/if}
	{#if loading}<p role="status">Loading current geometry…</p>{/if}
	{#if error}<p role="alert" class="text-destructive">{error}</p>
		{#if !draft && !loading}<Button
				type="button"
				variant="outline"
				class="min-h-11 min-w-11"
				onclick={load}>Retry</Button
			>{/if}{/if}
	{#if draft}
		<fieldset disabled={saving} class="space-y-3">
			{#if activeTool}<p role="status" class="text-xs">
					{activeTool === 'draw-footprint'
						? 'Click around the school grounds, then double-click to close the shape. This replaces the circle footprint.'
						: activeTool === 'footprint'
							? 'Drag the school grounds shape to move it, or drag a vertex to reshape it. The marker stays unchanged.'
							: 'Click to add a missing marker, or drag the marker. Grounds stay unchanged.'}
				</p>{/if}
			<p class="text-xs">
				Revision {original?.revision} · Point source: {original?.locationSource} · Approximate grounds
				area: {area === null ? 'not drawn' : `${Math.round(area).toLocaleString()} m²`}
			</p>
			{#if history[0]}<p class="text-xs text-muted-foreground">
					Last updated by {history[0].actorName} on
					{new Date(history[0].changedAt).toLocaleString()}.
				</p>{/if}
			{#if area !== null && (area < 100 || area > 2_000_000)}<p role="status" class="text-xs">
					Check this unusually small or large grounds area.
				</p>{/if}
			{#each warnings as warning, index (index)}<p class="text-xs">{warning}</p>{/each}
			<label class="block"
				>Reason for correction <textarea
					class="mt-1 block min-h-11 w-full rounded-md border border-input bg-background p-2 focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
					required
					maxlength="2000"
					rows="2"
					bind:value={draft.reason}
					oninput={() => (dirty = true)}
				></textarea></label
			>
			<label class="block"
				>Source or evidence <input
					class="mt-1 block min-h-11 w-full rounded-md border border-input bg-background p-2 focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
					required
					maxlength="1000"
					bind:value={draft.sourceDescription}
					oninput={() => (dirty = true)}
					placeholder="Imagery provider and date, or field evidence"
				/></label
			>
			{#if dirty && validation.length}<ul class="list-inside list-disc text-xs text-destructive">
					{#each validation as message (message)}<li>{message}</li>{/each}
				</ul>{/if}
		</fieldset>
		<details>
			<summary
				class="min-h-11 cursor-pointer rounded-md py-3 focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
				>Revision history</summary
			>
			{#if historyError}<p role="status">{historyError}</p>{:else if history.length === 0}<p>
					No saved revisions.
				</p>{/if}
			<ol class="space-y-2 py-2">
				{#each history as entry (entry.revision)}<li>
						<strong>Revision {entry.revision}</strong> · {new Date(
							entry.changedAt
						).toLocaleString()}
						<p>{entry.reason}</p>
						<p class="text-xs text-muted-foreground">
							by {entry.actorName} · {entry.sourceDescription}
						</p>
					</li>{/each}
			</ol>
		</details>
	{/if}
</section>
