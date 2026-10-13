<script lang="ts">
	import { page } from '$app/state';
	import { untrack } from 'svelte';
	import { afterNavigate, replaceState } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { routes } from '$lib/config';
	import OverviewWorkspace from '$lib/components/dashboard/OverviewWorkspace.svelte';
	import {
		hasOverviewState,
		overviewHistoryState,
		parseOverviewState,
		serializeOverviewState,
		type OverviewUrlState
	} from '$lib/components/dashboard/overview-url';
	import { ResizablePanelGroup } from '$lib/components/ui/resizable';
	import * as m from '$lib/paraglide/messages';

	const STORAGE_KEY = 'overview-state';

	const urlState = overviewHistoryState(page.url.search, page.state.overview);

	function storedState(): OverviewUrlState | null {
		if (typeof localStorage === 'undefined') return null;
		try {
			const raw = localStorage.getItem(STORAGE_KEY);
			if (!raw) return null;
			const value: unknown = JSON.parse(raw);
			if (!value || typeof value !== 'object') return null;
			const record = value as Record<string, unknown>;
			const strings = (input: unknown): string[] =>
				Array.isArray(input)
					? input.filter((item): item is string => typeof item === 'string')
					: [];
			return {
				school: typeof record.school === 'string' ? record.school : null,
				province: typeof record.province === 'string' ? record.province : null,
				expandedProvinces: strings(record.expandedProvinces),
				expandedCircuits: strings(record.expandedCircuits)
			};
		} catch {
			return null;
		}
	}

	// A shared link wins; otherwise restore the last session from local storage.
	const initial =
		page.state.overview ?? (hasOverviewState(urlState) ? urlState : (storedState() ?? urlState));

	let selectedSchoolId: string | null = $state(initial.school);
	let selectedProvince: string | null = $state(initial.province);
	let expandedProvinces = $state<Set<string>>(new Set(initial.expandedProvinces));
	let expandedCircuits = $state<Set<string>>(new Set(initial.expandedCircuits));
	let restoredSchoolId = $state<string | null>(
		untrack(
			() => page.state.overview?.school ?? (!hasOverviewState(urlState) ? initial.school : null)
		)
	);

	// The query string we last wrote (or adopted). Used to tell our own URL
	// updates apart from external ones, such as the header search deep-link.
	let lastQuery = serializeOverviewState(urlState);

	// replaceState throws if called before the client router is initialised
	// (e.g. during hydration), so only sync once the first navigation settles.
	let routerReady = $state(false);
	let revealRequest = $state(0);

	function workspaceState(): OverviewUrlState {
		return {
			school: selectedSchoolId,
			province: selectedProvince,
			expandedProvinces: [...expandedProvinces],
			expandedCircuits: [...expandedCircuits]
		};
	}

	function adoptNavigation() {
		adopt(
			overviewHistoryState(page.url.search, page.state.overview, workspaceState()),
			Boolean(page.state.overview) || !hasOverviewState(parseOverviewState(page.url.search))
		);
	}

	function adopt(state: OverviewUrlState, fromHistory: boolean) {
		restoredSchoolId = fromHistory ? state.school : null;
		lastQuery = serializeOverviewState(state);
		selectedSchoolId = state.school;
		selectedProvince = state.province;
		expandedProvinces = new Set(state.expandedProvinces);
		expandedCircuits = new Set(state.expandedCircuits);
	}

	afterNavigate(({ type }) => {
		if (routerReady) adoptNavigation();
		// Header search is an action even when its school and URL are unchanged.
		// History traversal must not replay that action or undo saved collapses.
		if (type === 'goto' && page.state.overviewReveal) revealRequest += 1;
		routerReady = true;
	});

	$effect(() => {
		if (selectedSchoolId) selectedProvince = null;
		if (restoredSchoolId !== selectedSchoolId) restoredSchoolId = null;
	});

	// Shallow Back/Forward updates page.state, while full navigations update page.url.
	$effect(() => {
		if (!routerReady) return;
		const parsed = overviewHistoryState(
			page.url.search,
			page.state.overview,
			untrack(workspaceState)
		);
		const query = serializeOverviewState(parsed);
		if (query === lastQuery) return;
		untrack(adoptNavigation);
	});

	// Mirror the shareable view state into the URL so a copied link restores it.
	$effect(() => {
		if (!routerReady) return;
		const state: OverviewUrlState = {
			school: selectedSchoolId,
			province: selectedProvince,
			expandedProvinces: [...expandedProvinces],
			expandedCircuits: [...expandedCircuits]
		};
		const query = serializeOverviewState(state);
		lastQuery = query;
		untrack(() => {
			const historyState = { ...page.state, overview: state };
			if (query) replaceState(resolve(`${routes.overview}?${query}`), historyState);
			else replaceState(resolve(routes.overview), historyState);
		});
	});

	// Remember the workspace so it also survives leaving the page and returning.
	$effect(() => {
		const state: OverviewUrlState = {
			school: selectedSchoolId,
			province: selectedProvince,
			expandedProvinces: [...expandedProvinces],
			expandedCircuits: [...expandedCircuits]
		};
		try {
			localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
		} catch {
			// storage can be unavailable (private browsing)
		}
	});
</script>

<svelte:head>
	<title>{m.meta_titleTemplate({ title: m.meta_overview_title() })}</title>
	<meta name="description" content={m.meta_overview_description()} />
</svelte:head>

<div class="workspace overflow-hidden rounded-b-xl border shadow-sm">
	<ResizablePanelGroup orientation="horizontal" class="explorer-panels">
		<OverviewWorkspace
			{restoredSchoolId}
			{revealRequest}
			bind:selectedSchoolId
			bind:selectedProvince
			bind:expandedProvinces
			bind:expandedCircuits
		/>
	</ResizablePanelGroup>
</div>

<style>
	.workspace {
		height: auto;
	}
	.workspace :global(.explorer-panels) {
		min-width: 0;
	}
	/* Wide layout is side-by-side, so let it fill the app shell. The shell
	   reserves only the header above the workspace. */
	@media (min-width: 1200px) {
		.workspace {
			height: calc(100dvh - var(--header-height, 3rem));
		}
	}
</style>
