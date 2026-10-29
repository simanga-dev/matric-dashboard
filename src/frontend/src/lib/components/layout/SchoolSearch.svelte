<script lang="ts">
	import { onMount } from 'svelte';
	import {
		Search,
		Loader2,
		School as SchoolIcon,
		X,
		SearchX,
		ArrowRight,
		CornerDownLeft
	} from '@lucide/svelte';
	import { goto } from '$app/navigation';
	import { page } from '$app/state';
	import { resolve } from '$app/paths';
	import * as m from '$lib/paraglide/messages';
	import { fetchSchools } from '$lib/api/dashboard';
	import { routes, schoolPath } from '$lib/config';
	import type { School } from '$lib/types/dashboard';
	import { toTitleCase } from '$lib/utils';
	import { registerShortcutHandler, ShortcutAction } from '$lib/state/shortcuts.svelte';
	import {
		overviewHistoryState,
		revealOverviewSchool,
		serializeOverviewState
	} from '$lib/components/dashboard/overview-url';
	import { passRateTextClass } from '$lib/components/dashboard/school-hierarchy';
	import { schoolsQueryString } from '$lib/components/dashboard/schools-url';
	import { highlightParts } from './school-search';

	const PAGE_SIZE = 8;
	const DEBOUNCE_MS = 250;

	let { borderless = false }: { borderless?: boolean } = $props();
	let query = $state('');
	let searchedQuery = $state('');
	let results = $state<School[]>([]);
	let totalCount = $state(0);
	let status = $state<'idle' | 'loading' | 'done' | 'error'>('idle');
	let open = $state(false);
	let selectedIndex = $state(0);
	let input = $state<HTMLInputElement>();
	let debounceTimer: ReturnType<typeof setTimeout> | undefined;
	let controller: AbortController | undefined;

	const id = $props.id();
	const resultsId = `${id}-school-search-results`;
	const optionId = (index: number) => `${id}-school-search-option-${index}`;

	const hasQuery = $derived(query.trim().length > 0);
	const viewAllIndex = $derived(results.length);
	/** Results plus the trailing "see all" row, which is also keyboard-reachable. */
	const optionCount = $derived(results.length > 0 ? results.length + 1 : 0);
	const activeDescendant = $derived(open && optionCount > 0 ? optionId(selectedIndex) : undefined);
	onMount(() => {
		const unregister = registerShortcutHandler(ShortcutAction.FocusSearch, () => {
			input?.focus();
			input?.select();
		});
		return () => {
			unregister();
			clearTimeout(debounceTimer);
			controller?.abort();
		};
	});

	function resetResults() {
		controller?.abort();
		results = [];
		totalCount = 0;
		status = 'idle';
		open = false;
	}

	function handleInput() {
		clearTimeout(debounceTimer);
		const term = query.trim();
		if (!term) {
			resetResults();
			return;
		}

		status = 'loading';
		open = true;
		debounceTimer = setTimeout(() => void runSearch(term), DEBOUNCE_MS);
	}

	async function runSearch(term: string) {
		// Cancel the previous request so a slow, older response cannot replace newer results.
		controller?.abort();
		const current = new AbortController();
		controller = current;
		try {
			const data = await fetchSchools(
				{ pageNumber: 1, pageSize: PAGE_SIZE, search: term },
				undefined,
				current.signal
			);
			if (current.signal.aborted) return;
			results = data.items;
			totalCount = data.totalCount;
			searchedQuery = term;
			selectedIndex = 0;
			status = 'done';
		} catch {
			if (current.signal.aborted) return;
			results = [];
			totalCount = 0;
			searchedQuery = term;
			status = 'error';
		}
	}

	function moveSelection(step: number) {
		if (optionCount === 0) return;
		selectedIndex = (selectedIndex + step + optionCount) % optionCount;
		document.getElementById(optionId(selectedIndex))?.scrollIntoView?.({ block: 'nearest' });
	}

	function handleKeydown(e: KeyboardEvent) {
		if (e.key === 'Escape') {
			e.preventDefault();
			if (open) {
				open = false;
			} else if (query) {
				query = '';
				resetResults();
			} else {
				input?.blur();
			}
			return;
		}

		if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
			e.preventDefault();
			if (!open && hasQuery) {
				open = true;
				return;
			}
			moveSelection(e.key === 'ArrowDown' ? 1 : -1);
			return;
		}

		if (e.key === 'Enter' && open && status === 'done') {
			e.preventDefault();
			const school = results[selectedIndex];
			if (school) navigateToSchool(school);
			else if (selectedIndex === viewAllIndex && results.length > 0) openDirectory();
		}
	}

	function finishNavigation() {
		clearTimeout(debounceTimer);
		query = '';
		resetResults();
		input?.blur();
	}

	function openDirectory() {
		const queryString = schoolsQueryString({
			search: searchedQuery,
			province: null,
			sort: null,
			order: null,
			page: 1
		});
		finishNavigation();
		void goto(resolve(`${routes.schools}?${queryString}`));
	}

	function navigateToSchool(school: School) {
		finishNavigation();

		// On a school's own page the search is a jump to another school's page,
		// not a trip back to the explorer.
		if (page.route.id === '/(app)/schools/[id]') {
			if (school.id !== page.params.id) void goto(resolve(schoolPath(school.id)));
			return;
		}

		// Everywhere else, deep-link into the explorer so the school is selected
		// on the map and revealed in the hierarchy tree.
		const current = overviewHistoryState(page.url.search, page.state.overview);
		const queryString = serializeOverviewState(revealOverviewSchool(current, school));
		void goto(resolve(`${routes.overview}?${queryString}`), {
			state: { overviewReveal: (page.state.overviewReveal ?? 0) + 1 }
		});
	}

	function clearQuery() {
		clearTimeout(debounceTimer);
		query = '';
		resetResults();
		input?.focus();
	}

	function handleFocusOut(e: FocusEvent) {
		const next = e.relatedTarget;
		if (next instanceof Node && (e.currentTarget as HTMLElement).contains(next)) return;
		open = false;
	}

	function formatPassRate(rate: number): string {
		return `${rate.toFixed(1)}%`;
	}

	/** Keeps focus in the input while clicking inside the panel. */
	function keepFocus(e: MouseEvent) {
		e.preventDefault();
	}
</script>

<div class="relative" onfocusout={handleFocusOut}>
	<div
		class="flex items-center gap-1.5 rounded-lg bg-muted/50 px-3 py-1.5 transition-colors focus-within:bg-accent focus-within:text-accent-foreground focus-within:ring-2 focus-within:ring-ring"
		class:border={!borderless}
	>
		{#if status === 'loading'}
			<Loader2 class="size-4 shrink-0 animate-spin text-muted-foreground" aria-hidden="true" />
		{:else}
			<Search class="size-4 shrink-0 text-muted-foreground" aria-hidden="true" />
		{/if}
		<input
			bind:this={input}
			type="text"
			bind:value={query}
			oninput={handleInput}
			onkeydown={handleKeydown}
			onfocus={() => {
				if (hasQuery && status !== 'idle') open = true;
			}}
			placeholder={m.search_schools_placeholder()}
			aria-label={m.search_schools_label()}
			autocomplete="off"
			spellcheck="false"
			class="w-40 bg-transparent text-sm outline-none placeholder:text-muted-foreground md:w-56"
			role="combobox"
			aria-expanded={open}
			aria-controls={resultsId}
			aria-autocomplete="list"
			aria-activedescendant={activeDescendant}
		/>
		{#if query}
			<button
				type="button"
				onclick={clearQuery}
				onmousedown={keepFocus}
				aria-label={m.search_schools_clear()}
				class="-mr-1 inline-flex size-5 shrink-0 items-center justify-center rounded text-muted-foreground transition-colors hover:bg-background hover:text-foreground"
			>
				<X class="size-3.5" />
			</button>
		{:else}
			<kbd
				class="pointer-events-none hidden h-5 shrink-0 items-center rounded border bg-background px-1 font-mono text-[10px] font-medium text-muted-foreground sm:inline-flex"
				aria-hidden="true"
			>
				/
			</kbd>
		{/if}
	</div>

	<span class="sr-only" role="status" aria-live="polite">
		{#if open && status === 'done'}
			{m.search_schools_resultsAnnouncement({ count: totalCount })}
		{/if}
	</span>

	{#if open}
		<div
			class="absolute top-full left-0 z-50 mt-1.5 w-[min(26rem,calc(100vw-2rem))] overflow-hidden rounded-xl border bg-popover text-popover-foreground shadow-xl motion-safe:animate-in motion-safe:fade-in motion-safe:slide-in-from-top-1"
		>
			{#if status === 'loading' && results.length === 0}
				<div class="space-y-1 p-1.5" aria-label={m.search_schools_searching()}>
					{#each [0, 1, 2] as row (row)}
						<div class="flex items-center gap-3 px-2.5 py-2">
							<div class="size-8 shrink-0 animate-pulse rounded-md bg-muted"></div>
							<div class="flex-1 space-y-1.5">
								<div class="h-3.5 w-2/3 animate-pulse rounded bg-muted"></div>
								<div class="h-3 w-1/3 animate-pulse rounded bg-muted"></div>
							</div>
						</div>
					{/each}
				</div>
			{:else if status === 'error'}
				<div class="px-4 py-6 text-center text-sm text-muted-foreground">
					{m.search_schools_error()}
				</div>
			{:else if status === 'done' && results.length === 0}
				<div class="flex flex-col items-center gap-1.5 px-4 py-7 text-center">
					<SearchX class="mb-1 size-6 text-muted-foreground" aria-hidden="true" />
					<p class="text-sm font-medium">{m.search_schools_emptyTitle({ query: searchedQuery })}</p>
					<p class="text-xs text-muted-foreground">{m.search_schools_emptyHint()}</p>
				</div>
			{:else}
				<ul
					id={resultsId}
					role="listbox"
					aria-label={m.search_schools_label()}
					class="max-h-[min(24rem,60dvh)] overflow-y-auto p-1.5 transition-opacity"
					class:opacity-60={status === 'loading'}
				>
					{#each results as school, i (school.id)}
						<!-- svelte-ignore a11y_click_events_have_key_events (the combobox input owns keyboard selection via aria-activedescendant) -->
						<li
							id={optionId(i)}
							role="option"
							aria-selected={i === selectedIndex}
							class="flex cursor-pointer items-center gap-3 rounded-lg px-2.5 py-2"
							class:bg-accent={i === selectedIndex}
							class:text-accent-foreground={i === selectedIndex}
							onclick={() => navigateToSchool(school)}
							onmousedown={keepFocus}
							onmousemove={() => (selectedIndex = i)}
						>
							<div
								class="flex size-8 shrink-0 items-center justify-center rounded-md border bg-background text-muted-foreground"
							>
								<SchoolIcon class="size-4" aria-hidden="true" />
							</div>
							<div class="min-w-0 flex-1">
								<div class="truncate text-sm">
									{#each highlightParts(toTitleCase(school.name), searchedQuery) as part, p (p)}
										{#if part.match}<mark class="bg-transparent font-semibold text-foreground"
												>{part.text}</mark
											>{:else}<span class="text-foreground/80">{part.text}</span>{/if}
									{/each}
								</div>
								<div class="truncate text-xs text-muted-foreground">
									{toTitleCase(school.province)}{#if school.circuit}
										<span aria-hidden="true"> · </span>{toTitleCase(school.circuit)}{/if}
									<span aria-hidden="true"> · </span><span class="font-mono tabular-nums"
										>{school.id}</span
									>
								</div>
							</div>
							<span
								class="shrink-0 text-right text-xs font-medium tabular-nums {passRateTextClass(
									school.passRate
								)}"
							>
								{m.search_schools_passRate({ rate: formatPassRate(school.passRate) })}
							</span>
						</li>
					{/each}
					<!-- svelte-ignore a11y_click_events_have_key_events (the combobox input owns keyboard selection via aria-activedescendant) -->
					<li
						id={optionId(viewAllIndex)}
						role="option"
						aria-selected={selectedIndex === viewAllIndex}
						class="mt-1 flex cursor-pointer items-center justify-between gap-3 rounded-lg border-t px-2.5 py-2 text-sm text-muted-foreground"
						class:bg-accent={selectedIndex === viewAllIndex}
						class:text-accent-foreground={selectedIndex === viewAllIndex}
						onclick={openDirectory}
						onmousedown={keepFocus}
						onmousemove={() => (selectedIndex = viewAllIndex)}
					>
						<span>{m.search_schools_viewAll({ count: totalCount.toLocaleString() })}</span>
						<ArrowRight class="size-4" aria-hidden="true" />
					</li>
				</ul>
				<div
					class="hidden items-center gap-4 border-t bg-muted/40 px-3 py-2 text-[11px] text-muted-foreground sm:flex"
					aria-hidden="true"
				>
					<span class="inline-flex items-center gap-1">
						<kbd class="rounded border bg-background px-1 font-mono">↑</kbd>
						<kbd class="rounded border bg-background px-1 font-mono">↓</kbd>
						{m.search_schools_navigate()}
					</span>
					<span class="inline-flex items-center gap-1">
						<kbd class="inline-flex items-center rounded border bg-background px-1 py-0.5"
							><CornerDownLeft class="size-2.5" /></kbd
						>
						{m.search_schools_open()}
					</span>
					<span class="inline-flex items-center gap-1">
						<kbd class="rounded border bg-background px-1 font-mono">Esc</kbd>
						{m.search_schools_close()}
					</span>
				</div>
			{/if}
		</div>
	{/if}
</div>
