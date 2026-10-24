<script lang="ts">
	import { onMount } from 'svelte';
	import { page } from '$app/state';
	import { afterNavigate, replaceState } from '$app/navigation';
	import { resolve } from '$app/paths';
	import SchoolsTable from '$lib/components/dashboard/SchoolsTable.svelte';
	import { Button } from '$lib/components/ui/button';
	import { fetchRegionStats } from '$lib/api/dashboard';
	import { loadSchoolDirectory } from '$lib/components/dashboard/school-directory';
	import { passRateColor } from '$lib/components/dashboard/school-hierarchy';
	import {
		parseSchoolsQuery,
		schoolsQueryString,
		type SchoolsViewQuery
	} from '$lib/components/dashboard/schools-url';
	import type { RegionStats } from '$lib/types/dashboard';
	import { toTitleCase } from '$lib/utils';
	import * as m from '$lib/paraglide/messages';

	// The URL describes the view (search, province, sort, page), so deep links
	// such as /schools?search=pretoria keep working when opened or refreshed.
	// Real navigations are adopted below; the table's own changes are written
	// back with shallow URL updates that do not re-trigger navigation.
	let view = $state<SchoolsViewQuery>(parseSchoolsQuery(page.url.searchParams));
	let ready = false;

	afterNavigate(() => {
		view = parseSchoolsQuery(page.url.searchParams);
		ready = true;
	});

	let statsProvince = $state<string | null>(parseSchoolsQuery(page.url.searchParams).province);

	function handleQueryChange(query: SchoolsViewQuery) {
		statsProvince = query.province;
		if (!ready) return;
		const search = schoolsQueryString(query);
		replaceState(resolve(search ? `/schools?${search}` : '/schools'), page.state);
	}

	// Province options come from the cached school directory. It loads after
	// first paint so it never blocks the table; on failure the filter still
	// shows any province already in the URL.
	let provinces = $state<string[]>([]);
	onMount(() => {
		let cancelled = false;
		loadSchoolDirectory(fetch, window.location.origin)
			.then((schools) => {
				if (cancelled) return;
				provinces = [...new Set(schools.map((school) => school.province))].sort((a, b) =>
					a.localeCompare(b)
				);
			})
			.catch(() => {});
		return () => {
			cancelled = true;
		};
	});

	let stats = $state<RegionStats | null>(null);
	// Province the shown stats belong to, so a pending province change can be dimmed.
	let statsLoadedFor = $state<string | null | undefined>(undefined);
	let statsError = $state(false);
	let statsRetry = $state(0);

	$effect(() => {
		const province = statsProvince;
		void statsRetry;
		let cancelled = false;
		statsError = false;
		fetchRegionStats(province)
			.then((result) => {
				if (cancelled) return;
				stats = result;
				statsLoadedFor = province;
			})
			.catch(() => {
				if (cancelled) return;
				stats = null;
				statsError = true;
			});
		return () => {
			cancelled = true;
		};
	});

	let statsStale = $derived(stats !== null && statsLoadedFor !== statsProvince);
</script>

<svelte:head>
	<title>{m.meta_titleTemplate({ title: m.meta_schools_title() })}</title>
	<meta name="description" content={m.meta_schools_description()} />
</svelte:head>

<div class="mx-auto w-full max-w-7xl space-y-4 lg:space-y-6">
	<section aria-label={m.meta_schools_title()} aria-busy={stats === null && !statsError}>
		{#if statsError}
			<div
				class="flex flex-col items-start gap-2 rounded-lg border p-4 sm:flex-row sm:items-center sm:justify-between"
			>
				<p role="alert" class="text-sm text-destructive">{m.schools_kpiError()}</p>
				<Button variant="outline" size="sm" class="w-full sm:w-auto" onclick={() => statsRetry++}>
					{m.schools_kpiRetry()}
				</Button>
			</div>
		{:else}
			<dl
				class="grid grid-cols-1 gap-3 motion-safe:transition-opacity sm:grid-cols-3 {statsStale
					? 'opacity-60'
					: ''}"
			>
				<div class="rounded-lg border bg-muted/20 p-4">
					<dt class="text-xs text-muted-foreground">{m.schools_kpiSchools()}</dt>
					<dd class="mt-1 text-2xl font-semibold tabular-nums">
						{stats ? stats.schoolCount.toLocaleString() : '-'}
					</dd>
				</div>
				<div class="rounded-lg border bg-muted/20 p-4">
					<dt class="text-xs text-muted-foreground">{m.schools_kpiWrote()}</dt>
					<dd class="mt-1 text-2xl font-semibold tabular-nums">
						{stats ? stats.totalWrote.toLocaleString() : '-'}
					</dd>
				</div>
				<div class="rounded-lg border bg-muted/20 p-4">
					<dt class="text-xs text-muted-foreground">{m.schools_kpiPassRate()}</dt>
					<dd class="mt-1 text-2xl font-semibold tabular-nums">
						{#if stats}
							<span style:color={passRateColor(stats.passRate)}>{stats.passRate.toFixed(1)}%</span>
						{:else}
							-
						{/if}
					</dd>
				</div>
			</dl>
			{#if stats}
				<p class="mt-2 text-xs text-muted-foreground">
					{m.schools_kpiScope({ name: toTitleCase(stats.name), year: stats.year })}
				</p>
			{/if}
		{/if}
	</section>

	<SchoolsTable
		title={m.schools_allTitle()}
		pageSize={25}
		externalSearch={view.search || undefined}
		externalProvince={view.province}
		externalSort={view.sort}
		externalOrder={view.order}
		externalPage={view.page}
		{provinces}
		onQueryChange={handleQueryChange}
	/>
</div>
