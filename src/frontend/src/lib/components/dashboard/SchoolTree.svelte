<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { schoolPath } from '$lib/config';
	import {
		ChevronRight,
		School as SchoolIcon,
		Network,
		MapPin,
		Globe,
		Search,
		X,
		ExternalLink
	} from '@lucide/svelte';
	import { tick, untrack, type Snippet } from 'svelte';
	import type { School } from '$lib/types/dashboard';
	import { toTitleCase } from '$lib/utils';
	import {
		circuitKey,
		groupSchools,
		groupSortedSchools,
		passRateColor,
		sortSchoolsByName
	} from './school-hierarchy';

	let {
		initialSchools = [],
		selectedSchoolId = $bindable(null),
		selectedProvince = $bindable(null),
		selectedDistrict = $bindable(null),
		expandedProvinces = $bindable(new Set<string>()),
		expandedCircuits = $bindable(new Set<string>()),
		restoredSchoolId = null,
		revealRequest = 0,
		headerAction
	}: {
		initialSchools?: School[];
		selectedSchoolId?: string | null;
		selectedProvince?: string | null;
		selectedDistrict?: string | null;
		expandedProvinces?: Set<string>;
		expandedCircuits?: Set<string>;
		restoredSchoolId?: string | null;
		revealRequest?: number;
		headerAction?: Snippet;
	} = $props();

	// The directory is provided by the server load, so the tree renders on the
	// first paint without a client fetch or skeleton.
	let schools = $derived(initialSchools);
	let search = $state('');
	let scrollArea: HTMLDivElement | null = $state(null);
	let rowHeight = $state(34);
	let sortedSchools = $derived(sortSchoolsByName(schools));
	let grouped = $derived(groupSortedSchools(sortedSchools, search));
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
	// The "South Africa" root aggregates every province (and district).
	let countrySelected = $derived(!selectedSchoolId && !selectedProvince && !selectedDistrict);
	// Provinces are children of "South Africa"; collapsing the root hides them.
	let rootOpen = $state(true);
	let provincesVisible = $derived(rootOpen || search.trim() !== '');
	let selectionRequest = $derived({ id: selectedSchoolId, revealRequest, restoredSchoolId });

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
			rootOpen = true;
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

	/** Selects the whole country: the panel and map show national aggregates. */
	function selectCountry() {
		selectedProvince = null;
		selectedDistrict = null;
		selectedSchoolId = null;
	}

	/** Root disclosure: show or hide the province children. */
	function toggleRoot() {
		rootOpen = !rootOpen;
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
	style:--tree-row={`${rowHeight}px`}
>
	<header class="flex min-h-14 shrink-0 items-center gap-2 bg-muted/60 px-3">
		<Network class="size-4 shrink-0 text-muted-foreground" />
		<div class="min-w-0 flex-1">
			<h2 class="text-sm font-semibold">School browser</h2>
		</div>
		<span class="rounded-md bg-muted/70 px-2 py-1 text-xs text-muted-foreground tabular-nums"
			>{schools.length.toLocaleString()}</span
		>
		{@render headerAction?.()}
	</header>
	<div class="space-y-3 p-3 pb-2">
		<div class="relative">
			<Search
				class="pointer-events-none absolute start-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground"
			/>
			<input
				aria-label="Filter schools by name, EMIS, province or circuit"
				placeholder="Find school, EMIS or area…"
				bind:value={search}
				class="min-h-[44px] w-full rounded-lg border border-transparent bg-muted/50 ps-9 pe-9 text-xs outline-none focus-visible:border-ring/30 focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-50"
			/>
			{#if search}<button
					type="button"
					aria-label="Clear school filter"
					onclick={() => (search = '')}
					class="absolute end-1 top-1/2 -translate-y-1/2 rounded-md p-1.5 hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring"
					><X class="size-4" /></button
				>{/if}
		</div>
		<div class="flex flex-wrap items-center justify-between gap-1 text-xs">
			<span class="text-muted-foreground">Browse hierarchy</span>
			<div class="flex gap-1">
				<button
					type="button"
					disabled={!matchCount}
					onclick={expandAll}
					class="min-h-8 rounded-md px-2 py-1 text-muted-foreground hover:bg-muted hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-50 pointer-coarse:min-h-[44px]"
					>Expand all</button
				>
				<button
					type="button"
					disabled={!matchCount}
					onclick={() => {
						expandedProvinces = new Set();
						expandedCircuits = new Set();
					}}
					class="min-h-8 rounded-md px-2 py-1 text-muted-foreground hover:bg-muted hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-50 pointer-coarse:min-h-[44px]"
					>Collapse all</button
				>
			</div>
		</div>
	</div>
	<div bind:this={scrollArea} class="min-h-0 flex-1 overflow-y-auto px-2 pb-2">
		{#if !matchCount}
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
			<ul class="space-y-1 pt-2">
				<li>
					<div
						class="branch branch-root"
						class:selected={countrySelected}
						bind:clientHeight={rowHeight}
					>
						<button
							type="button"
							class="disclosure"
							aria-label={`${rootOpen ? 'Collapse' : 'Expand'} South Africa`}
							aria-expanded={rootOpen}
							onclick={toggleRoot}
							><ChevronRight
								class={`size-3.5 transition-transform ${rootOpen ? 'rotate-90' : ''}`}
							/></button
						>
						<button
							type="button"
							class="node font-medium"
							aria-pressed={countrySelected}
							onclick={selectCountry}
							title="South Africa"
							><Globe class="size-3.5 shrink-0 text-primary" /><span class="truncate"
								>South Africa</span
							><span class="ml-auto text-[11px] text-muted-foreground tabular-nums"
								>{matchCount.toLocaleString()}</span
							></button
						>
					</div>
					{#if provincesVisible}
						<ul class="ms-4 border-s border-border/40 ps-1">
							{#each [...grouped] as [province, circuits] (province)}
								<li>
									<div
										class="branch branch-province"
										class:selected={selectedProvince === province}
									>
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
											><MapPin class="size-3.5 shrink-0 text-muted-foreground" /><span
												class="truncate"
												>{province ? toTitleCase(province) : 'Unknown province'}</span
											><span class="ml-auto text-[11px] text-muted-foreground tabular-nums"
												>{[...circuits.values()].reduce(
													(sum, items) => sum + items.length,
													0
												)}</span
											></button
										>
									</div>
									{#if openProvinces.has(province)}
										<ul class="ms-4 border-s border-border/40 ps-1">
											{#each [...circuits] as [circuit, schoolList] (circuit)}
												{@const key = circuitKey(province, circuit)}
												<li>
													<div
														class="branch branch-circuit"
														class:selected={selectedProvince === province &&
															selectedDistrict === circuit}
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
															aria-pressed={selectedProvince === province &&
																selectedDistrict === circuit}
															onclick={() => selectDistrict(province, circuit)}
															title={toTitleCase(circuit)}
															><span class="truncate"
																>{circuit ? toTitleCase(circuit) : 'Unknown circuit'}</span
															><span class="ml-auto text-[11px] tabular-nums"
																>{schoolList.length}</span
															></button
														>
													</div>
													{#if openCircuits.has(key)}
														<ul class="ms-3 border-s border-border/40 ps-1">
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
																			/><span class="truncate">{toTitleCase(school.name)}</span
																			></button
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
				</li>
			</ul>
		{/if}
	</div>
</section>

<style>
	.branch {
		display: flex;
		align-items: center;
		border-radius: var(--radius);
	}
	/* Ancestor rows pin to the top of the scroll area so the current
	   province and district stay visible while schools scroll past. The
	   offset is derived from the measured row height so the stacked
	   headers line up exactly. */
	.branch-root,
	.branch-province,
	.branch-circuit {
		position: sticky;
		z-index: 1;
		background: hsl(var(--card));
	}
	.branch-root {
		top: 0;
		z-index: 3;
	}
	.branch-province {
		top: var(--tree-row, 2.25rem);
		z-index: 2;
	}
	.branch-circuit {
		top: calc(var(--tree-row, 2.25rem) * 2);
	}
	.node {
		display: flex;
		min-width: 0;
		flex: 1;
		align-items: center;
		gap: 0.5rem;
		border-radius: var(--radius);
		padding: 0.5rem;
		text-align: start;
		font-size: max(0.75rem, 12px);
	}
	.disclosure {
		flex-shrink: 0;
		border-radius: var(--radius);
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
		background: color-mix(in oklab, hsl(var(--primary)) 10%, hsl(var(--card)));
		color: hsl(var(--foreground));
	}
	.school-row {
		display: flex;
		align-items: center;
		border-radius: var(--radius);
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
		background: color-mix(in oklab, hsl(var(--primary)) 16%, hsl(var(--card)));
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
