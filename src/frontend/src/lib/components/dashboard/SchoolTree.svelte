<script lang="ts">
	import { Skeleton } from '$lib/components/ui/skeleton';
	import { fetchSchools } from '$lib/api/dashboard';
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { schoolPath } from '$lib/config';
	import {
		ChevronRight,
		School as SchoolIcon,
		Network,
		MapPin,
		Search,
		X,
		AlertCircle,
		ExternalLink
	} from '@lucide/svelte';
	import { onMount, tick, untrack, type Snippet } from 'svelte';
	import type { School } from '$lib/types/dashboard';
	import { toTitleCase } from '$lib/utils';
	import { circuitKey, groupSchools, loadSchoolHierarchy, passRateColor } from './school-hierarchy';

	let {
		selectedSchoolId = $bindable(null),
		selectedProvince = $bindable(null),
		selectedDistrict = $bindable(null),
		expandedProvinces = $bindable(new Set<string>()),
		expandedCircuits = $bindable(new Set<string>()),
		restoredSchoolId = null,
		revealRequest = 0,
		headerAction
	}: {
		selectedSchoolId?: string | null;
		selectedProvince?: string | null;
		selectedDistrict?: string | null;
		expandedProvinces?: Set<string>;
		expandedCircuits?: Set<string>;
		restoredSchoolId?: string | null;
		revealRequest?: number;
		headerAction?: Snippet;
	} = $props();

	let schools: School[] = $state([]);
	let loading = $state(true);
	let error = $state(false);
	let search = $state('');
	let scrollArea: HTMLDivElement | null = $state(null);
	let disposed = false;
	let grouped = $derived(groupSchools(schools, search));
	let matchCount = $derived(
		[...grouped.values()].reduce(
			(total, circuits) =>
				total + [...circuits.values()].reduce((sum, items) => sum + items.length, 0),
			0
		)
	);

	// While filtering, every matching branch is revealed. This is transient and
	// never overwrites the user's shareable expansion state.
	let searchOpen = $derived.by(() => {
		if (!search.trim() || !grouped.size) return null;
		return {
			provinces: new Set(grouped.keys()),
			circuits: new Set(
				[...grouped].flatMap(([province, circuits]) =>
					[...circuits.keys()].map((circuit) => circuitKey(province, circuit))
				)
			)
		};
	});
	let openProvinces = $derived(searchOpen?.provinces ?? expandedProvinces);
	let openCircuits = $derived(searchOpen?.circuits ?? expandedCircuits);
	let selectionRequest = $derived({ id: selectedSchoolId, revealRequest, restoredSchoolId });

	async function loadSchools() {
		loading = true;
		error = false;
		try {
			const result = await loadSchoolHierarchy(fetchSchools, () => disposed);
			if (!disposed) schools = result;
		} catch {
			if (!disposed) error = true;
		} finally {
			if (!disposed) loading = false;
		}
	}

	onMount(() => {
		void loadSchools();
		return () => {
			disposed = true;
		};
	});

	function expandAll() {
		expandedProvinces = new Set(grouped.keys());
		expandedCircuits = new Set(
			[...grouped].flatMap(([province, circuits]) =>
				[...circuits.keys()].map((circuit) => circuitKey(province, circuit))
			)
		);
	}

	// A new selection or explicit header-search action reveals and scrolls once.
	// Filter edits and disclosure toggles do not replay the action.
	$effect(() => {
		const selected = schools.find((school) => String(school.id) === selectionRequest.id);
		if (!selected) return;
		// A history snapshot includes intentional collapses. Only new selections
		// reveal their branch; Back/Forward must restore the saved expansion state.
		if (selectionRequest.id === selectionRequest.restoredSchoolId) return;

		untrack(() => {
			if (search && !groupSchools([selected], search).size) search = '';
			expandedProvinces = new Set([...expandedProvinces, selected.province]);
			expandedCircuits = new Set([
				...expandedCircuits,
				circuitKey(selected.province, selected.circuit)
			]);
		});

		void tick().then(() => {
			scrollArea
				?.querySelector<HTMLElement>('.school-row.selected')
				?.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
		});
	});

	function toggle(set: Set<string>, key: string) {
		return set.has(key) ? new Set([...set].filter((item) => item !== key)) : new Set([...set, key]);
	}

	function selectProvince(province: string) {
		selectedProvince = province;
		selectedDistrict = null;
		selectedSchoolId = null;
	}

	function selectDistrict(province: string, district: string) {
		selectedProvince = province;
		selectedDistrict = district;
		selectedSchoolId = null;
	}

	function selectSchool(school: School) {
		selectedSchoolId = String(school.id);
		selectedProvince = null;
		selectedDistrict = null;
	}
</script>

<section
	aria-label="School browser"
	class="flex h-full min-h-0 flex-col overflow-hidden bg-card text-card-foreground"
>
	<header class="flex h-11 shrink-0 items-center gap-3 border-b bg-muted/30 px-4">
		<Network class="size-4 shrink-0 text-primary" />
		<div class="min-w-0 flex-1">
			<h2 class="text-sm font-semibold">School browser</h2>
		</div>
		{#if !loading && !error}<span
				class="rounded-md border bg-background px-2 py-1 text-xs tabular-nums"
				>{schools.length.toLocaleString()}</span
			>{/if}
		{@render headerAction?.()}
	</header>
	<div class="space-y-2 border-b p-3">
		<div class="relative">
			<Search class="pointer-events-none absolute top-2.5 left-3 size-4 text-muted-foreground" />
			<input
				aria-label="Filter schools by name, EMIS, province or circuit"
				placeholder="Find school, EMIS or area…"
				bind:value={search}
				disabled={loading || error}
				class="h-9 w-full rounded-md border bg-background pr-9 pl-9 text-xs outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-50"
			/>
			{#if search}<button
					type="button"
					aria-label="Clear school filter"
					onclick={() => (search = '')}
					class="absolute top-1 right-1 rounded p-1.5 hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring"
					><X class="size-4" /></button
				>{/if}
		</div>
		<div class="flex flex-wrap items-center justify-between gap-1 text-xs">
			<span class="text-muted-foreground">Browse hierarchy</span>
			<div class="flex gap-1">
				<button
					type="button"
					disabled={loading || error || !matchCount}
					onclick={expandAll}
					class="rounded px-2 py-1 hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-50"
					>Expand all</button
				>
				<button
					type="button"
					disabled={loading || error || !matchCount}
					onclick={() => {
						expandedProvinces = new Set();
						expandedCircuits = new Set();
					}}
					class="rounded px-2 py-1 hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-50"
					>Collapse all</button
				>
			</div>
		</div>
	</div>
	<div bind:this={scrollArea} class="min-h-0 flex-1 overflow-y-auto p-2" aria-busy={loading}>
		{#if loading}
			<div role="status" class="space-y-1 p-1">
				<span class="sr-only">Loading school directory…</span>
				{#each [{ indent: false, width: 'w-24' }, { indent: true, width: 'w-28' }, { indent: true, width: 'w-20' }, { indent: true, width: 'w-32' }, { indent: false, width: 'w-28' }, { indent: true, width: 'w-24' }, { indent: true, width: 'w-28' }] as row, i (i)}
					<div class="flex items-center gap-2 rounded-md px-2 py-2" class:ml-4={row.indent}>
						<Skeleton class="size-3.5 shrink-0 rounded-sm" />
						<Skeleton class="h-3.5 rounded-full {row.width}" />
						<Skeleton class="ml-auto h-3 w-6 shrink-0 rounded-full" />
					</div>
				{/each}
			</div>
		{:else if error}
			<div role="alert" class="space-y-3 p-4 text-sm">
				<AlertCircle class="size-5 text-destructive" />
				<p>Unable to load the school directory.</p>
				<button
					type="button"
					onclick={loadSchools}
					class="rounded-md border px-3 py-2 text-xs hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring"
					>Try again</button
				>
			</div>
		{:else if !matchCount}
			<div role="status" class="px-4 py-8 text-center">
				<Search class="mx-auto mb-3 size-6 text-muted-foreground" />
				<p class="text-sm font-medium">{search ? 'No matching schools' : 'No schools available'}</p>
				<p class="mt-1 text-xs text-muted-foreground">
					{search
						? 'Try a school name, EMIS number or another area.'
						: 'The directory has no school records yet.'}
				</p>
			</div>
		{:else}
			<ul class="space-y-1">
				{#each [...grouped] as [province, circuits] (province)}
					<li>
						<div class="branch" class:selected={selectedProvince === province}>
							<button
								type="button"
								class="disclosure"
								aria-label={`${openProvinces.has(province) ? 'Collapse' : 'Expand'} ${toTitleCase(province)}`}
								aria-expanded={openProvinces.has(province)}
								onclick={() => (expandedProvinces = toggle(expandedProvinces, province))}
								><ChevronRight
									class={`size-3.5 transition-transform ${openProvinces.has(province) ? 'rotate-90' : ''}`}
								/></button
							>
							<button
								type="button"
								class="node font-medium"
								aria-pressed={selectedProvince === province}
								onclick={() => selectProvince(province)}
								title={toTitleCase(province)}
								><MapPin class="size-3.5 shrink-0 text-muted-foreground" /><span class="truncate"
									>{province ? toTitleCase(province) : 'Unknown province'}</span
								><span class="ml-auto text-[11px] text-muted-foreground tabular-nums"
									>{[...circuits.values()].reduce((sum, items) => sum + items.length, 0)}</span
								></button
							>
						</div>
						{#if openProvinces.has(province)}
							<ul class="ml-4 border-l border-border pl-1">
								{#each [...circuits] as [circuit, schoolList] (circuit)}
									{@const key = circuitKey(province, circuit)}
									<li>
										<div
											class="branch"
											class:selected={selectedProvince === province && selectedDistrict === circuit}
										>
											<button
												type="button"
												class="disclosure"
												aria-label={`${openCircuits.has(key) ? 'Collapse' : 'Expand'} ${circuit ? toTitleCase(circuit) : 'Unknown circuit'} in ${toTitleCase(province)}`}
												aria-expanded={openCircuits.has(key)}
												onclick={() => (expandedCircuits = toggle(expandedCircuits, key))}
												><ChevronRight
													class={`size-3 shrink-0 transition-transform ${openCircuits.has(key) ? 'rotate-90' : ''}`}
												/></button
											>
											<button
												type="button"
												class="node text-muted-foreground"
												aria-pressed={selectedProvince === province && selectedDistrict === circuit}
												onclick={() => selectDistrict(province, circuit)}
												title={toTitleCase(circuit)}
												><span class="truncate"
													>{circuit ? toTitleCase(circuit) : 'Unknown circuit'}</span
												><span class="ml-auto text-[11px] tabular-nums">{schoolList.length}</span
												></button
											>
										</div>
										{#if openCircuits.has(key)}
											<ul class="ml-3 border-l border-border pl-1">
												{#each schoolList as school (school.id)}<li>
														<div
															class="school-row"
															class:selected={selectedSchoolId === String(school.id)}
														>
															<button
																type="button"
																class="node"
																aria-pressed={selectedSchoolId === String(school.id)}
																onclick={() => selectSchool(school)}
																title={`${toTitleCase(school.name)} · EMIS ${school.id} · ${school.passRate.toFixed(1)}% pass rate`}
																><SchoolIcon
																	class="size-3.5 shrink-0"
																	color={passRateColor(school.passRate)}
																/><span class="truncate">{toTitleCase(school.name)}</span></button
															>
															<button
																type="button"
																class="row-action"
																aria-label={`View ${toTitleCase(school.name)}`}
																title="View school"
																onclick={() => goto(resolve(schoolPath(school.id)))}
															>
																<ExternalLink class="size-4" />
															</button>
														</div>
													</li>{/each}
											</ul>
										{/if}
									</li>
								{/each}
							</ul>
						{/if}
					</li>
				{/each}
			</ul>
		{/if}
	</div>
</section>

<style>
	.branch {
		display: flex;
		align-items: center;
		border-radius: 0.375rem;
	}
	.node {
		display: flex;
		min-width: 0;
		flex: 1;
		align-items: center;
		gap: 0.5rem;
		border-radius: 0.375rem;
		padding: 0.5rem;
		text-align: left;
		font-size: 0.75rem;
	}
	.disclosure {
		flex-shrink: 0;
		border-radius: 0.375rem;
		padding: 0.5rem;
	}
	.node:hover,
	.disclosure:hover {
		background: hsl(var(--muted));
	}
	.node:focus-visible,
	.disclosure:focus-visible {
		outline: 2px solid hsl(var(--ring));
		outline-offset: -2px;
	}
	.selected {
		background: color-mix(in oklab, hsl(var(--primary)) 18%, transparent);
		color: hsl(var(--primary));
	}
	.school-row {
		display: flex;
		align-items: center;
		border-radius: 0.375rem;
	}
	.school-row:hover {
		background: hsl(var(--muted));
	}
	.school-row .node:hover {
		background: transparent;
	}
	.school-row.selected .node {
		font-weight: 600;
	}
	.school-row.selected:hover,
	.branch.selected:hover {
		background: color-mix(in oklab, hsl(var(--primary)) 28%, transparent);
	}
	.row-action {
		flex-shrink: 0;
		border-radius: 0.375rem;
		padding: 0.375rem;
		color: hsl(var(--muted-foreground));
		transition: opacity 0.15s ease;
	}
	.row-action:hover {
		color: hsl(var(--card-foreground));
	}
	.row-action:focus-visible {
		outline: 2px solid hsl(var(--ring));
		outline-offset: -2px;
	}
	@media (hover: hover) {
		.row-action {
			opacity: 0;
		}
		.school-row:hover .row-action,
		.school-row:focus-within .row-action {
			opacity: 1;
		}
	}
</style>
