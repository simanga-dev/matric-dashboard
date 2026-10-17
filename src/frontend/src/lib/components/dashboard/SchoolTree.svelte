<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { schoolPath } from '$lib/config';
	import { Skeleton } from '$lib/components/ui/skeleton';
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
	import { onDestroy, tick, untrack, type Snippet } from 'svelte';
	import type { School, SchoolList } from '$lib/types/dashboard';
	import { scheduleSchoolTreeSearch } from './school-tree-search';
	import { schoolDetailCache } from './school-detail-cache';
	import {
		circuitKey,
		countSchools,
		groupSortedSchools,
		passRateTextClass,
		sortSchoolsByName,
		titleCase
	} from './school-hierarchy';
	import {
		filterSchoolDirectory,
		flattenSchoolTree,
		indexSchoolDirectory,
		schoolTreeWindow,
		type SchoolTreeRow
	} from './school-tree-model';

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

	let directory = $derived(indexSchoolDirectory(initialSchools));
	let search = $state('');
	let query = $derived(search.trim());
	let localMatches = $derived(query ? filterSchoolDirectory(directory, query) : []);
	let needsServer = $derived(!!query && localMatches.length === 0);
	let searchPage = $state(1);
	let searchRetry = $state(0);
	let searchPending = $state(false);
	let searchError = $state(false);
	let searchResult = $state<(SchoolList & { query: string }) | null>(null);
	let matchingResult = $derived(searchResult?.query === query ? searchResult : null);
	let searchLoading = $derived(needsServer && (searchPending || (!matchingResult && !searchError)));
	let grouped = $derived(
		query
			? groupSortedSchools(
					localMatches.length ? localMatches : sortSchoolsByName(matchingResult?.items ?? [])
				)
			: directory.groups
	);
	let matchCount = $derived(countSchools(grouped));
	let rootOpen = $state(true);
	let rows = $derived(
		flattenSchoolTree(grouped, rootOpen, expandedProvinces, expandedCircuits, !!query)
	);
	let rowIndices = $derived(new Map(rows.map((row, index) => [row.key, index])));
	let scrollArea: HTMLDivElement | null = $state(null);
	let searchInput: HTMLInputElement | null = $state(null);
	let scrollTop = $state(0);
	let viewportHeight = $state(600);
	let rowHeight = $state(44);
	let focusedKey = $state('country');
	let focusInside = $state(false);
	let viewWindow = $derived(schoolTreeWindow(rows.length, scrollTop, viewportHeight, rowHeight));
	let tabKey = $derived.by(() => {
		const focusedIndex = rowIndices.get(focusedKey);
		if (focusInside && focusedIndex !== undefined) return focusedKey;
		// Outside the tree, keep the Tab entry on screen rather than on an
		// unmounted row. Use the viewport, not the invisible overscan rows.
		const first = Math.min(rows.length - 1, Math.max(0, Math.floor(scrollTop / rowHeight)));
		const end = Math.ceil((scrollTop + viewportHeight) / rowHeight);
		return focusedIndex !== undefined && focusedIndex >= first && focusedIndex < end
			? focusedKey
			: (rows[first]?.key ?? 'country');
	});
	let visibleRows = $derived.by(() => {
		const indices = Array.from(
			{ length: viewWindow.end - viewWindow.start },
			(_, i) => i + viewWindow.start
		);
		// Keep a focused row mounted if the user scrolls away from it.
		const focusedIndex = rowIndices.get(tabKey);
		if (focusInside && focusedIndex !== undefined && !indices.includes(focusedIndex))
			indices.push(focusedIndex);
		return indices.sort((a, b) => a - b).map((index) => ({ row: rows[index]!, index }));
	});
	let currentRow = $derived(rows[Math.min(rows.length - 1, Math.floor(scrollTop / rowHeight))]);
	let currentProvince = $derived(
		currentRow?.kind === 'school'
			? currentRow.school.province
			: currentRow?.kind === 'province' || currentRow?.kind === 'circuit'
				? currentRow.province
				: null
	);
	let currentCircuit = $derived(
		currentRow?.kind === 'school'
			? currentRow.school.circuit
			: currentRow?.kind === 'circuit'
				? currentRow.circuit
				: null
	);
	let selectionRequest = $derived({ id: selectedSchoolId, revealRequest, restoredSchoolId });
	let revealText = $state(false);
	// Browse scroll position, restored when the filter is cleared.
	let browseScrollTop = 0;
	let prefetchTimer: ReturnType<typeof setTimeout> | undefined;

	async function setFilter(value: string) {
		const previous = query;
		const next = value.trim();
		search = value;
		searchPage = 1;
		if (next) {
			if (!previous) browseScrollTop = scrollTop;
			scrollTop = 0;
			if (scrollArea) scrollArea.scrollTop = 0;
		} else if (previous) {
			scrollTop = browseScrollTop;
			await tick();
			if (scrollArea) scrollArea.scrollTop = browseScrollTop;
		}
	}

	$effect(() => {
		const term = needsServer ? query : '';
		const page = searchPage;
		void searchRetry;
		searchError = false;
		searchPending = !!term;
		if (!term) {
			searchResult = null;
			return;
		}
		return scheduleSchoolTreeSearch(
			term,
			page,
			(result) => {
				const previous = page > 1 && searchResult?.query === term ? searchResult.items : [];
				const items = [
					...new Map([...previous, ...result.items].map((school) => [school.id, school])).values()
				];
				searchResult = { ...result, items, query: term };
				searchPending = false;
			},
			() => {
				searchError = true;
				searchPending = false;
			}
		);
	});

	// Stagger movement once typing settles, without hiding already-painted
	// results. Opacity stays unchanged throughout the animation.
	$effect(() => {
		void query;
		void matchingResult;
		revealText = false;
		const start = setTimeout(() => (revealText = true), 180);
		const stop = setTimeout(() => (revealText = false), 950);
		return () => {
			clearTimeout(start);
			clearTimeout(stop);
		};
	});

	$effect(() => {
		const selected = directory.byId.get(selectionRequest.id ?? '');
		if (!selected || selectionRequest.id === selectionRequest.restoredSchoolId) return;
		untrack(() => {
			if (
				query &&
				!localMatches.some((school) => school.id === selected.id) &&
				!matchingResult?.items.some((school) => school.id === selected.id)
			)
				setFilter('');
			rootOpen = true;
			expandedProvinces = new Set([...expandedProvinces, selected.province]);
			expandedCircuits = new Set([
				...expandedCircuits,
				circuitKey(selected.province, selected.circuit)
			]);
		});
		void tick().then(() => {
			const index = rowIndices.get(`school:${selected.id}`);
			if (index !== undefined) revealRow(index);
		});
	});

	onDestroy(() => clearTimeout(prefetchTimer));

	function prefetch(id: string) {
		clearTimeout(prefetchTimer);
		prefetchTimer = setTimeout(() => {
			schoolDetailCache.prefetch(id);
		}, 80);
	}

	function toggle(set: Set<string>, key: string) {
		return set.has(key) ? new Set([...set].filter((item) => item !== key)) : new Set([...set, key]);
	}

	function toggleRow(row: SchoolTreeRow) {
		if (row.kind === 'country') rootOpen = !rootOpen;
		else if (row.kind === 'province') expandedProvinces = toggle(expandedProvinces, row.province);
		else if (row.kind === 'circuit') expandedCircuits = toggle(expandedCircuits, row.key);
	}

	function selectRow(row: SchoolTreeRow) {
		if (row.kind === 'school') {
			selectedSchoolId = String(row.school.id);
			selectedProvince = null;
			selectedDistrict = null;
		} else {
			selectScope(
				row.kind === 'country' ? null : row.province,
				row.kind === 'circuit' ? row.circuit : null
			);
		}
	}

	function selectScope(province: string | null, district: string | null) {
		selectedSchoolId = null;
		selectedProvince = province;
		selectedDistrict = district;
	}

	function isSelected(row: SchoolTreeRow) {
		if (row.kind === 'school') return selectedSchoolId === String(row.school.id);
		if (selectedSchoolId) return false;
		if (row.kind === 'country') return !selectedProvince && !selectedDistrict;
		if (row.kind === 'province') return selectedProvince === row.province && !selectedDistrict;
		return selectedProvince === row.province && selectedDistrict === row.circuit;
	}

	function label(row: SchoolTreeRow) {
		if (row.kind === 'country') return 'South Africa';
		if (row.kind === 'province') return row.province ? titleCase(row.province) : 'Unknown province';
		if (row.kind === 'circuit') return row.circuit ? titleCase(row.circuit) : 'Unknown circuit';
		return titleCase(row.school.name);
	}

	function expandAll() {
		rootOpen = true;
		expandedProvinces = new Set(grouped.keys());
		expandedCircuits = new Set(
			[...grouped].flatMap(([province, circuits]) =>
				[...circuits.keys()].map((circuit) => circuitKey(province, circuit))
			)
		);
	}

	function revealRow(index: number) {
		if (!scrollArea) return;
		const top = index * rowHeight;
		const bottom = top + rowHeight;
		const next =
			top < scrollArea.scrollTop
				? top
				: bottom > scrollArea.scrollTop + viewportHeight
					? bottom - viewportHeight
					: scrollArea.scrollTop;
		scrollArea.scrollTop = next;
		scrollTop = scrollArea.scrollTop;
	}

	async function focusRow(index: number) {
		const row = rows[index];
		if (!row) return;
		focusedKey = row.key;
		revealRow(index);
		await tick();
		scrollArea?.querySelector<HTMLElement>(`[data-row-index="${index}"]`)?.focus();
	}

	function handleSearchKeydown(event: KeyboardEvent) {
		if (event.key !== 'ArrowDown' || !matchCount) return;
		event.preventDefault();
		// Filtering should land on the first result, not the country/province headers.
		const firstSchool = query ? rows.findIndex((row) => row.kind === 'school') : 0;
		void focusRow(Math.max(0, firstSchool));
	}

	function handleKeydown(event: KeyboardEvent) {
		const element = (event.target as HTMLElement).closest<HTMLElement>('[data-row-index]');
		if (!element) return;
		const index = Number(element.dataset.rowIndex);
		const row = rows[index];
		if (!row) return;
		if (event.key === 'ArrowDown') {
			event.preventDefault();
			void focusRow(Math.min(rows.length - 1, index + 1));
		} else if (event.key === 'ArrowUp') {
			event.preventDefault();
			// Wrap back to the filter box from the first row.
			if (index === 0) searchInput?.focus();
			else void focusRow(index - 1);
		} else if (event.key === 'Home') {
			event.preventDefault();
			void focusRow(0);
		} else if (event.key === 'End') {
			event.preventDefault();
			void focusRow(rows.length - 1);
		} else if (event.key === 'ArrowRight' && row.kind !== 'school') {
			event.preventDefault();
			if (!row.expanded) toggleRow(row);
			else if (rows[index + 1]?.parent === row.key) void focusRow(index + 1);
		} else if (event.key === 'ArrowLeft') {
			event.preventDefault();
			if (row.kind !== 'school' && row.expanded && !query) toggleRow(row);
			else if (row.parent) void focusRow(rowIndices.get(row.parent) ?? 0);
		} else if ((event.key === 'Enter' || event.key === ' ') && event.target === element) {
			event.preventDefault();
			selectRow(row);
		}
	}
</script>

<section
	aria-label="School browser"
	class="flex h-full min-h-0 flex-col overflow-hidden bg-card text-card-foreground"
	style:--tree-row={`${rowHeight}px`}
>
	<div
		class="row-measure pointer-events-none invisible absolute"
		bind:clientHeight={rowHeight}
	></div>
	<header class="flex min-h-14 shrink-0 items-center gap-2 bg-muted/60 px-3">
		<Network class="size-4 shrink-0 text-muted-foreground" />
		<h2 class="min-w-0 flex-1 text-sm font-semibold">School browser</h2>
		<span class="rounded-md bg-muted/70 px-2 py-1 text-xs text-muted-foreground tabular-nums"
			>{initialSchools.length.toLocaleString()}</span
		>
		{@render headerAction?.()}
	</header>
	<div class="space-y-3 p-3 pb-2">
		<div class="relative">
			<Search
				class="pointer-events-none absolute start-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground"
			/>
			<input
				bind:this={searchInput}
				aria-label="Filter schools by name, EMIS, province or circuit"
				placeholder="Find school, EMIS or area…"
				value={search}
				oninput={(event) => setFilter(event.currentTarget.value)}
				onkeydown={handleSearchKeydown}
				class="min-h-[44px] w-full rounded-lg border border-transparent bg-muted/50 ps-9 pe-12 text-xs outline-none focus-visible:border-ring/30 focus-visible:ring-2 focus-visible:ring-ring"
			/>
			{#if search}<button
					type="button"
					aria-label="Clear school filter"
					onclick={() => setFilter('')}
					class="absolute end-0 top-1/2 flex size-11 -translate-y-1/2 items-center justify-center rounded-md hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring"
					><X class="size-4" /></button
				>{/if}
		</div>
		<div class="flex flex-wrap items-center justify-between gap-1 text-xs">
			<span class="text-muted-foreground">Browse hierarchy</span>
			<div class="flex gap-1">
				<button
					type="button"
					disabled={!matchCount || !!query}
					onclick={expandAll}
					class="min-h-11 rounded-md px-2 py-1 text-muted-foreground hover:bg-muted hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-50"
					>Expand all</button
				>
				<button
					type="button"
					disabled={!matchCount || !!query}
					onclick={() => {
						expandedProvinces = new Set();
						expandedCircuits = new Set();
						scrollTop = 0;
						if (scrollArea) scrollArea.scrollTop = 0;
					}}
					class="min-h-11 rounded-md px-2 py-1 text-muted-foreground hover:bg-muted hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-50"
					>Collapse all</button
				>
			</div>
		</div>
		{#if query && !searchError}
			<p role="status" class="text-xs text-muted-foreground">
				{searchLoading
					? 'Searching schools…'
					: `${matchCount.toLocaleString()}${needsServer && matchingResult ? ` of ${matchingResult.totalCount.toLocaleString()}` : ''} matches`}
			</p>
		{/if}
	</div>
	<nav
		aria-label="Current tree location"
		class="flex min-h-8 shrink-0 items-center gap-0.5 border-b border-border/30 px-2 text-xs text-muted-foreground pointer-coarse:min-h-11"
	>
		<button
			type="button"
			aria-label="Country overview"
			onclick={() => selectScope(null, null)}
			class="flex size-8 shrink-0 items-center justify-center rounded hover:bg-muted hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring pointer-coarse:size-11"
			><Globe class="size-3.5" /></button
		>
		{#if currentProvince}
			<ChevronRight class="size-3 shrink-0" />
			<button
				type="button"
				aria-label={`Province overview for ${titleCase(currentProvince)}`}
				title={titleCase(currentProvince)}
				onclick={() => selectScope(currentProvince, null)}
				class="min-h-8 min-w-0 truncate rounded px-1.5 text-start hover:bg-muted hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring pointer-coarse:min-h-11 pointer-coarse:min-w-11"
				>{titleCase(currentProvince)}</button
			>
		{/if}
		{#if currentCircuit}
			<ChevronRight class="size-3 shrink-0" />
			<button
				type="button"
				aria-label={`District overview for ${titleCase(currentCircuit)} in ${titleCase(currentProvince ?? '')}`}
				title={titleCase(currentCircuit)}
				onclick={() => selectScope(currentProvince, currentCircuit)}
				class="min-h-8 min-w-0 truncate rounded px-1.5 text-start hover:bg-muted hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring pointer-coarse:min-h-11 pointer-coarse:min-w-11"
				>{titleCase(currentCircuit)}</button
			>
		{/if}
	</nav>
	<div
		bind:this={scrollArea}
		tabindex="-1"
		bind:clientHeight={viewportHeight}
		onscroll={(event) => {
			scrollTop = event.currentTarget.scrollTop;
		}}
		class="tree-scroll relative min-h-0 flex-1 overflow-y-auto px-2"
		aria-busy={searchLoading}
	>
		{#if searchLoading && !matchCount}
			<div role="status" class="space-y-3 px-2 py-3">
				<span class="sr-only">Loading matching schools…</span>
				{#each [0, 1, 2, 3, 4, 5, 6, 7] as row (row)}
					<div
						class="flex items-center gap-2"
						style:padding-inline-start={`${Math.min(row, 3) * 12}px`}
					>
						<Skeleton class="size-4 shrink-0 rounded-sm" /><Skeleton
							class={`h-4 rounded-md ${row % 2 ? 'w-3/5' : 'w-4/5'}`}
						/>
					</div>
				{/each}
			</div>
		{:else if !matchCount && !searchError}
			<div role="status" class="px-4 py-8 text-center">
				<Search class="mx-auto mb-3 size-6 text-muted-foreground" />
				<p class="text-sm font-medium">{query ? 'No matching schools' : 'No schools available'}</p>
				<p class="mt-1 text-xs text-muted-foreground">
					{query
						? 'Try a school name, EMIS number or another area.'
						: 'The directory has no school records yet.'}
				</p>
			</div>
		{:else if matchCount}
			<ul
				role="tree"
				aria-label="School hierarchy"
				class="relative"
				class:tree-reveal={revealText}
				style:height={`${viewWindow.totalHeight}px`}
				onkeydown={handleKeydown}
				onfocusin={(event) => {
					const element = (event.target as HTMLElement).closest<HTMLElement>('[data-row-index]');
					const row = element ? rows[Number(element.dataset.rowIndex)] : null;
					if (row) {
						focusedKey = row.key;
						focusInside = true;
						if (row.kind === 'school') prefetch(row.school.id);
					}
				}}
				onfocusout={(event) => {
					if (!event.currentTarget.contains(event.relatedTarget as Node | null))
						focusInside = false;
				}}
				onpointerover={(event) => {
					const element = (event.target as HTMLElement).closest<HTMLElement>('[data-school-id]');
					if (element?.dataset.schoolId && !element.contains(event.relatedTarget as Node | null))
						prefetch(element.dataset.schoolId);
				}}
				onpointerout={(event) => {
					const element = (event.target as HTMLElement).closest('[data-school-id]');
					if (element && !element.contains(event.relatedTarget as Node | null))
						clearTimeout(prefetchTimer);
				}}
			>
				{#each visibleRows as { row, index } (row.key)}
					<li
						role="treeitem"
						aria-label={label(row)}
						aria-level={row.level}
						aria-posinset={row.position}
						aria-setsize={row.siblings}
						aria-selected={isSelected(row)}
						aria-expanded={row.kind === 'school' ? undefined : row.expanded}
						tabindex={row.key === tabKey ? 0 : -1}
						data-row-index={index}
						data-school-id={row.kind === 'school' ? row.school.id : undefined}
						class="tree-row"
						class:school-row={row.kind === 'school'}
						class:selected={isSelected(row)}
						style:top={`${index * rowHeight}px`}
						style:padding-inline-start={`${(row.level - 1) * 12}px`}
						style:--row-delay={`${Math.min(180, Math.max(0, index - viewWindow.start) * 25)}ms`}
					>
						{#if row.kind !== 'school'}
							<button
								type="button"
								tabindex="-1"
								class="disclosure"
								disabled={!!query}
								aria-label={`${row.expanded ? 'Collapse' : 'Expand'} ${label(row)}${row.kind === 'circuit' ? ` in ${titleCase(row.province)}` : ''}`}
								onclick={() => toggleRow(row)}
								><ChevronRight
									class={`size-3.5 transition-transform motion-reduce:transition-none ${row.expanded ? 'rotate-90' : ''}`}
								/></button
							>
						{/if}
						<button
							type="button"
							tabindex="-1"
							class="node"
							class:font-medium={row.kind !== 'school'}
							aria-pressed={isSelected(row)}
							onclick={() => selectRow(row)}
							title={row.kind === 'school'
								? `${label(row)} · EMIS ${row.school.id} · ${row.school.passRate.toFixed(1)}% pass rate`
								: label(row)}
						>
							{#if row.kind === 'country'}<Globe class="size-3.5 shrink-0 text-primary" />
							{:else if row.kind === 'province'}<MapPin
									class="size-3.5 shrink-0 text-muted-foreground"
								/>
							{:else if row.kind === 'school'}<SchoolIcon
									class="size-3.5 shrink-0 {passRateTextClass(row.school.passRate)}"
								/>{/if}
							<span class="row-label truncate">{label(row)}</span>
							{#if row.kind !== 'school'}<span
									class="ms-auto text-xs text-muted-foreground tabular-nums"
									>{row.count.toLocaleString()}</span
								>{/if}
						</button>
						{#if row.kind === 'school'}
							<button
								type="button"
								tabindex={row.key === tabKey ? 0 : -1}
								class="row-action"
								aria-label={`View ${label(row)}`}
								title="View school"
								onclick={() => {
									if (row.kind === 'school') void goto(resolve(schoolPath(row.school.id)));
								}}><ExternalLink class="size-4" /></button
							>
						{/if}
					</li>
				{/each}
			</ul>
		{/if}
		{#if searchError}
			<div role="alert" class="space-y-2 px-4 py-4 text-sm">
				<p>Unable to search schools.</p>
				<button
					type="button"
					onclick={() => searchRetry++}
					class="min-h-11 rounded-md border px-3 py-2 text-xs focus-visible:ring-2 focus-visible:ring-ring"
					>Try again</button
				>
			</div>
		{/if}
		{#if needsServer && matchingResult?.hasNextPage}
			<button
				type="button"
				disabled={searchLoading || searchError}
				onclick={() => searchPage++}
				class="my-3 min-h-[44px] w-full rounded-md border px-3 py-2 text-xs hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-50"
				>Load more schools</button
			>
		{/if}
	</div>
</section>

<style>
	.row-measure {
		height: max(2.75rem, 44px);
	}
	.tree-scroll {
		overflow-anchor: none;
		scrollbar-gutter: stable;
	}
	.tree-row {
		position: absolute;
		inset-inline: 0;
		display: flex;
		align-items: center;
		height: var(--tree-row);
		border-radius: var(--radius);
	}
	.node {
		display: flex;
		min-width: 44px;
		height: 100%;
		flex: 1;
		align-items: center;
		gap: 0.5rem;
		border-radius: var(--radius);
		padding: 0.5rem;
		text-align: start;
		font-size: max(0.75rem, 12px);
	}
	.disclosure {
		display: flex;
		height: 100%;
		width: 44px;
		justify-content: center;
		flex-shrink: 0;
		align-items: center;
		border-radius: var(--radius);
		padding: 0.5rem;
	}
	.tree-row:hover {
		background: hsl(var(--muted));
	}
	.disclosure:disabled {
		cursor: default;
	}
	.tree-row:focus-visible,
	.node:focus-visible,
	.disclosure:focus-visible,
	.row-action:focus-visible {
		outline: 2px solid hsl(var(--ring));
		outline-offset: -2px;
	}
	.selected {
		background: color-mix(in oklab, hsl(var(--primary)) 10%, hsl(var(--card)));
		color: hsl(var(--foreground));
	}
	.selected .node {
		font-weight: 600;
	}
	.tree-row.selected:hover {
		background: color-mix(in oklab, hsl(var(--primary)) 16%, hsl(var(--card)));
	}
	.row-action {
		display: flex;
		align-items: center;
		justify-content: center;
		width: 44px;
		height: 100%;
		flex-shrink: 0;
		border-radius: 0.375rem;
		padding: 0.375rem;
		color: hsl(var(--muted-foreground));
		transition: opacity 150ms ease;
	}
	.row-action:hover {
		color: hsl(var(--card-foreground));
	}
	@media (hover: hover) {
		.row-action {
			opacity: 0;
		}
		.tree-row:hover .row-action,
		.tree-row:focus-within .row-action {
			opacity: 1;
		}
	}
	@media (prefers-reduced-motion: no-preference) {
		.tree-reveal .row-label,
		.tree-reveal .node > :global(svg) {
			animation: tree-text-enter 450ms ease-in-out var(--row-delay) both;
		}
	}
	@keyframes tree-text-enter {
		from {
			translate: 0 5px;
		}
		to {
			translate: 0 0;
		}
	}
</style>
