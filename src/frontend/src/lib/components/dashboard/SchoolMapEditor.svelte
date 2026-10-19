<script lang="ts">
	import { onMount, onDestroy } from 'svelte';
	import { beforeNavigate } from '$app/navigation';
	import { Button } from '$lib/components/ui/button';
	import type OlMap from 'ol/Map';
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
		type EditTool
	} from './school-map-editing';

	let {
		map,
		emis,
		name,
		onloaded,
		onsaved,
		onclose
	}: {
		map: OlMap;
		emis: string;
		name: string;
		onloaded?: (state: SpatialState) => void;
		onsaved: (state: SpatialState) => void;
		onclose: () => void;
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
	let disposed = false;
	let interactions: ReturnType<typeof createSpatialInteractions> | null = null;
	let validation = $derived(draft ? validateSpatialDraft(draft) : []);
	let warnings = $derived([
		...(original?.warnings ?? []),
		...(draft && original ? spatialDraftWarnings(draft, original) : [])
	]);

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
	async function load() {
		loading = true;
		error = '';
		try {
			const state = await fetchSchoolSpatial(school.emis);
			if (disposed) return;
			onloaded?.(state);
			original = state;
			draft = spatialDraft(state);
			interactions = createSpatialInteractions(
				map,
				state,
				(boundary, point, markerEdited) => {
					if (!draft) return;
					draft.boundary = boundary;
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
	function tool(next: EditTool) {
		activeTool = next;
		interactions?.tool(next);
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
		void load();
	});
	onDestroy(() => {
		disposed = true;
		interactions?.destroy();
		if (typeof window !== 'undefined') window.removeEventListener('beforeunload', beforeUnload);
	});
</script>

<section
	aria-label="School map editor"
	class="max-h-80 space-y-3 overflow-auto border-t bg-card p-3 text-sm"
>
	<div class="flex items-start justify-between gap-3">
		<div>
			<h3 class="font-semibold">Edit school map</h3>
			<p>{school.name} <span class="text-muted-foreground">EMIS {school.emis}</span></p>
		</div>
		<Button
			type="button"
			variant="outline"
			class="min-h-11 min-w-11"
			disabled={saving}
			onclick={() => {
				if (canLeave()) onclose();
			}}>Cancel</Button
		>
	</div>
	<p class="text-xs text-muted-foreground">
		Editor-traced grounds, not a surveyed cadastral boundary. School selection is locked until Save
		or Cancel. Pan and zoom to find a school with no marker. Drawing and moving geometry require a
		pointer in this version. Keyboard users can pan and zoom the map, edit evidence, Save, and
		Cancel, but cannot edit vertices.
	</p>
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
			<div class="flex flex-wrap gap-2">
				<Button
					type="button"
					variant="outline"
					class="min-h-11 min-w-11"
					aria-pressed={activeTool === 'draw'}
					onclick={() => tool('draw')}>{draft.boundary ? 'Redraw grounds' : 'Draw grounds'}</Button
				>
				<Button
					type="button"
					variant="outline"
					class="min-h-11 min-w-11"
					disabled={!draft.boundary}
					aria-pressed={activeTool === 'grounds'}
					onclick={() => tool('grounds')}>Edit grounds</Button
				>
				<Button
					type="button"
					variant="outline"
					class="min-h-11 min-w-11"
					aria-pressed={activeTool === 'marker'}
					onclick={() => tool('marker')}
					>{original?.location || draft.location ? 'Move marker' : 'Add marker'}</Button
				>
			</div>
			{#if activeTool}<p role="status" class="text-xs">
					{activeTool === 'draw'
						? 'Click vertices, then double-click to finish the grounds. A new drawing replaces the draft grounds only.'
						: activeTool === 'grounds'
							? 'Drag grounds vertices. Alt-click a vertex to remove it.'
							: 'Click to add a missing marker, or drag the marker. Grounds stay unchanged.'}
				</p>{/if}
			<p class="text-xs">
				Revision {original?.revision} · Point source: {original?.locationSource} · Approximate grounds
				area: {area === null ? 'not drawn' : `${Math.round(area).toLocaleString()} m²`}
			</p>
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
			<Button
				type="button"
				class="min-h-11 min-w-11"
				disabled={!dirty || validation.length > 0}
				onclick={save}>{saving ? 'Saving…' : 'Save school map'}</Button
			>
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
						<p class="text-xs text-muted-foreground">{entry.sourceDescription}</p>
					</li>{/each}
			</ol>
		</details>
	{/if}
</section>
