<script lang="ts">
	import { untrack } from 'svelte';
	import { page } from '$app/state';
	import { afterNavigate, replaceState } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { SvelteURLSearchParams } from 'svelte/reactivity';
	import { EmptyState } from '$lib/components/common';
	import * as Card from '$lib/components/ui/card';
	import * as Table from '$lib/components/ui/table';
	import { Button } from '$lib/components/ui/button';
	import { Skeleton } from '$lib/components/ui/skeleton';
	import StatCard from '$lib/components/dashboard/StatCard.svelte';
	import SubjectPassRateChart from '$lib/components/dashboard/SubjectPassRateChart.svelte';
	import type { SubjectBar } from '$lib/components/dashboard/subject-bar';
	import YearPills from '$lib/components/dashboard/YearPills.svelte';
	import { passRateColor } from '$lib/components/dashboard/school-hierarchy';
	import { subjectHref } from '$lib/components/dashboard/subject-url';
	import { fetchRegionSubjects, fetchSchoolById, fetchSchoolSubjects } from '$lib/api/dashboard';
	import { toTitleCase } from '$lib/utils';
	import type { RegionSubjects, SchoolSubjects } from '$lib/types/dashboard';
	import {
		ArrowDown,
		ArrowUp,
		BookMarked,
		Building2,
		ChevronsUpDown,
		Percent,
		Users
	} from '@lucide/svelte';
	const searchParams = page.url.searchParams;
	const initialSchoolId = searchParams.get('school');

	// A school selection takes precedence over any province/district in the URL.
	let schoolId = $state<string | null>(initialSchoolId);
	let schoolName = $state<string | null>(null);
	let province = $state<string | null>(initialSchoolId ? null : searchParams.get('province'));
	let district = $state<string | null>(initialSchoolId ? null : searchParams.get('district'));
	let year = $state<number | null>(null);

	let region = $state<RegionSubjects | null>(null);
	let schoolSubjects = $state<SchoolSubjects | null>(null);
	let loading = $state(true);
	let error = $state<string | null>(null);
	let requestId = 0;

	let isSchoolScope = $derived(schoolId !== null);
	let availableYears = $derived(
		isSchoolScope ? (schoolSubjects?.availableYears ?? []) : (region?.availableYears ?? [])
	);

	let scopeKind = $derived(
		isSchoolScope
			? 'School'
			: region?.scope === 'district'
				? 'District'
				: region?.scope === 'province'
					? 'Province'
					: 'National'
	);

	let scopeName = $derived(
		isSchoolScope
			? toTitleCase(schoolName ?? 'School')
			: toTitleCase(region?.name ?? 'South Africa')
	);

	let schoolCount = $derived(isSchoolScope ? null : (region?.schoolCount ?? null));

	interface SubjectRow {
		key: string;
		code: string;
		name: string;
		totalWrote: number;
		achieved: number | null;
		percentAchieved: number | null;
		schoolCount: number | null;
	}

	let rows = $derived<SubjectRow[]>(
		isSchoolScope
			? (schoolSubjects?.subjects ?? [])
					.filter((subject) => subject.totalWrote > 0)
					.map((subject) => ({
						key: subject.subjectId,
						code: subject.code,
						name: subject.name,
						totalWrote: subject.totalWrote,
						achieved: subject.achieved,
						percentAchieved: subject.percentAchieved,
						schoolCount: null
					}))
			: (region?.subjects ?? []).map((subject) => ({
					key: subject.code,
					code: subject.code,
					name: subject.name,
					totalWrote: subject.totalWrote,
					achieved: subject.achieved,
					percentAchieved: subject.percentAchieved,
					schoolCount: subject.schoolCount
				}))
	);

	// Subject links keep the current region and year. A school is not a scope
	// on the subject page, so school-scoped rows link to the national view.
	function rowHref(code: string) {
		return isSchoolScope ? subjectHref(code) : subjectHref(code, { province, district, year });
	}

	type SortKey = 'name' | 'totalWrote' | 'achieved' | 'percentAchieved' | 'schoolCount';
	let sortKey = $state<SortKey>('name');
	let sortDir = $state<'asc' | 'desc'>('asc');

	let sortedRows = $derived.by(() => {
		const direction = sortDir === 'asc' ? 1 : -1;
		return [...rows].sort((a, b) => {
			if (sortKey === 'name') return a.name.localeCompare(b.name) * direction;
			const left = a[sortKey];
			const right = b[sortKey];
			// Unavailable values stay at the bottom in either direction.
			if (left === null && right === null) return a.name.localeCompare(b.name);
			if (left === null) return 1;
			if (right === null) return -1;
			return (left - right) * direction || a.name.localeCompare(b.name);
		});
	});

	function toggleSort(key: SortKey) {
		if (sortKey === key) sortDir = sortDir === 'asc' ? 'desc' : 'asc';
		else {
			sortKey = key;
			sortDir = key === 'name' ? 'asc' : 'desc';
		}
	}

	function ariaSort(key: SortKey): 'ascending' | 'descending' | 'none' {
		if (sortKey !== key) return 'none';
		return sortDir === 'asc' ? 'ascending' : 'descending';
	}

	let bars = $derived<SubjectBar[]>(
		rows.map((row) => ({
			name: row.name,
			totalWrote: row.totalWrote,
			percentAchieved: row.percentAchieved,
			href: resolve(rowHref(row.code))
		}))
	);

	let totalWrote = $derived(rows.reduce((sum, row) => sum + row.totalWrote, 0));

	// Learner-weighted across subjects with a reported achievement count.
	let overallPassRate = $derived.by(() => {
		let wrote = 0;
		let achieved = 0;
		for (const row of rows) {
			if (row.achieved === null) continue;
			wrote += row.totalWrote;
			achieved += row.achieved;
		}
		return wrote > 0 ? (achieved / wrote) * 100 : null;
	});

	// Query-only navigation reuses this page, so adopt the URL on every
	// navigation, including the first one after hydration.
	afterNavigate(() => {
		const params = page.url.searchParams;
		schoolId = params.get('school');
		schoolName = null;
		province = schoolId ? null : params.get('province');
		district = schoolId ? null : params.get('district');
		year = Number(params.get('year')) || null;
		region = null;
		schoolSubjects = null;
		reload(year);
		ready = true;
	});

	let ready = $state(false);

	// Keep the scope in the URL so the view can be shared or bookmarked.
	$effect(() => {
		if (!ready) return;
		const params = new SvelteURLSearchParams();
		if (schoolId) params.set('school', schoolId);
		else {
			if (province) params.set('province', province);
			if (district) params.set('district', district);
		}
		if (year) params.set('year', String(year));
		const query = params.toString();
		// Do not subscribe to router state here. Navigation is adopted above;
		// our own shallow URL writes must not reload results or replay old scope.
		untrack(() => replaceState(resolve(query ? `/subjects?${query}` : '/subjects'), page.state));
	});

	function reload(requestedYear?: number | null) {
		if (schoolId) void loadSchool(schoolId, requestedYear);
		else void loadRegion(requestedYear);
	}

	async function loadRegion(requestedYear?: number | null) {
		const token = ++requestId;
		loading = true;
		error = null;
		try {
			const result = await fetchRegionSubjects(province, district, requestedYear ?? null);
			if (token !== requestId) return;
			region = result;
			schoolSubjects = null;
			year = result.year;
		} catch (e) {
			if (token !== requestId) return;
			region = null;
			schoolSubjects = null;
			year = null;
			error = e instanceof Error ? e.message : 'Failed to load subject results';
		} finally {
			if (token === requestId) loading = false;
		}
	}

	async function loadSchool(id: string, requestedYear?: number | null) {
		const token = ++requestId;
		loading = true;
		error = null;
		try {
			const result = await fetchSchoolSubjects(id, requestedYear ?? undefined);
			if (token !== requestId) return;
			schoolSubjects = result;
			region = null;
			year = result.year;
			if (!schoolName) {
				const detail = await fetchSchoolById(id);
				if (token !== requestId) return;
				if (detail) schoolName = detail.name;
			}
		} catch (e) {
			if (token !== requestId) return;
			schoolSubjects = null;
			year = null;
			error = e instanceof Error ? e.message : 'Failed to load subject results';
		} finally {
			if (token === requestId) loading = false;
		}
	}

	function selectYear(value: number) {
		year = value;
		reload(value);
	}

	// Placeholder bar heights for the loading chart, descending like the real one.
	const skeletonBars = [96, 90, 88, 85, 82, 78, 76, 74, 70, 66, 58];

	function retryLoad() {
		reload(year);
	}
</script>

{#snippet sortIcon(key: SortKey)}
	{#if sortKey === key && sortDir === 'asc'}
		<ArrowUp class="size-3" />
	{:else if sortKey === key}
		<ArrowDown class="size-3" />
	{:else}
		<ChevronsUpDown class="size-3 text-muted-foreground/50" />
	{/if}
{/snippet}

<svelte:head>
	<title>Subjects • MatricDashboard</title>
	<meta name="description" content="NSC subject performance by province, district or school." />
</svelte:head>

<div class="flex flex-1 flex-col">
	<div class="@container/main flex flex-1 flex-col gap-2">
		<div class="flex flex-col gap-4 py-4 md:gap-6 md:py-6">
			<div
				class="grid grid-cols-1 gap-4 px-4 *:data-[slot=card]:bg-gradient-to-t *:data-[slot=card]:from-primary/5 *:data-[slot=card]:to-card *:data-[slot=card]:shadow-xs lg:px-6 @xl/main:grid-cols-2 @5xl/main:grid-cols-4 dark:*:data-[slot=card]:bg-card"
				aria-label="Scope summary"
			>
				{#if loading}
					{#each { length: 4 }, index (index)}
						<Card.Root class="@container/card" aria-hidden="true">
							<Card.Header class="gap-3">
								<div class="flex items-center justify-between">
									<Skeleton class="h-4 w-28" />
									<Skeleton class="size-4 rounded-full" />
								</div>
								<Skeleton class="h-8 w-24" />
							</Card.Header>
						</Card.Root>
					{/each}
				{:else}
					<StatCard
						title="Subjects offered"
						value={rows.length.toLocaleString()}
						icon={BookMarked}
					/>
					<StatCard title="Subject entries" value={totalWrote.toLocaleString()} icon={Users} />
					{#if schoolCount !== null}
						<StatCard title="Schools" value={schoolCount.toLocaleString()} icon={Building2} />
					{/if}
					<StatCard
						title="Overall subject pass rate"
						value={overallPassRate === null ? '–' : `${overallPassRate.toFixed(1)}%`}
						valueColor={overallPassRate === null ? undefined : passRateColor(overallPassRate)}
						icon={Percent}
					/>
				{/if}
			</div>

			<div class="px-4 lg:px-6">
				<Card.Root class="@container/card">
					<Card.Header>
						<div class="flex flex-wrap items-start justify-between gap-3">
							{#if loading}
								<div class="space-y-2" aria-hidden="true">
									<Skeleton class="h-5 w-40" />
									<Skeleton class="h-4 w-64" />
								</div>
							{:else}
								<div>
									<Card.Title class="flex items-center gap-2 text-base">
										<BookMarked class="size-4 text-primary" />
										{scopeName}
									</Card.Title>
									<Card.Description>
										Subject pass rate · {scopeKind}{year ? ` · ${year}` : ''}{schoolCount !== null
											? ` · ${schoolCount.toLocaleString()} schools`
											: ''}
									</Card.Description>
								</div>
							{/if}
							{#if loading}
								<div class="flex gap-2" aria-hidden="true">
									{#each { length: 6 }, index (index)}
										<Skeleton class="h-6 w-14 rounded-full" />
									{/each}
								</div>
							{:else if availableYears.length > 0}
								<YearPills years={availableYears} selected={year} onSelect={selectYear} />
							{/if}
						</div>
					</Card.Header>
					<Card.Content>
						{#if loading}
							<div role="status" aria-label="Loading subject results">
								<div class="mb-3 flex gap-4">
									<Skeleton class="h-3 w-24" />
									<Skeleton class="h-3 w-24" />
								</div>
								<div class="ms-9 flex h-56 items-end gap-2 pt-6">
									{#each skeletonBars as height, index (index)}
										<Skeleton class="min-w-16 flex-1 rounded-b-none" style="height: {height}%" />
									{/each}
								</div>
								<div class="ms-9 mt-2 flex gap-2">
									{#each skeletonBars, index (index)}
										<div class="flex min-w-16 flex-1 justify-center">
											<Skeleton class="h-3 w-3/4" />
										</div>
									{/each}
								</div>
							</div>
						{:else if error}
							<div class="flex h-64 flex-col items-center justify-center gap-3">
								<p role="alert" class="text-sm text-destructive">{error}</p>
								<Button variant="outline" size="sm" onclick={retryLoad}>Try again</Button>
							</div>
						{:else if availableYears.length === 0}
							<EmptyState icon={BookMarked} message="No subject data for this scope." />
						{:else}
							<SubjectPassRateChart subjects={bars} headlineYear={year} />
						{/if}
					</Card.Content>
				</Card.Root>
			</div>

			{#if loading}
				<div class="px-4 lg:px-6" aria-hidden="true">
					<Card.Root>
						<Card.Header class="gap-2">
							<Skeleton class="h-5 w-36" />
							<Skeleton class="h-4 w-48" />
						</Card.Header>
						<Card.Content class="space-y-3">
							<Skeleton class="h-8 w-full" />
							{#each { length: 6 }, index (index)}
								<div class="flex items-center gap-6">
									<Skeleton class="h-4 flex-1" />
									<Skeleton class="h-4 w-20" />
									<Skeleton class="h-4 w-20" />
									<Skeleton class="h-4 w-16" />
									<Skeleton class="h-4 w-16" />
								</div>
							{/each}
						</Card.Content>
					</Card.Root>
				</div>
			{:else if !error && rows.length > 0}
				<div class="px-4 lg:px-6">
					<Card.Root>
						<Card.Header>
							<Card.Title class="text-base">Subject results</Card.Title>
							<Card.Description>
								{rows.length} subjects · {totalWrote.toLocaleString()} wrote
							</Card.Description>
						</Card.Header>
						<Card.Content>
							<Table.Root>
								<Table.Header>
									<Table.Row>
										<Table.Head class="" aria-sort={ariaSort('name')}>
											<button
												type="button"
												class="inline-flex items-center gap-1 font-medium transition-colors hover:text-foreground"
												onclick={() => toggleSort('name')}
											>
												Subject{@render sortIcon('name')}
											</button>
										</Table.Head>
										<Table.Head class="text-right" aria-sort={ariaSort('totalWrote')}>
											<button
												type="button"
												class="inline-flex items-center gap-1 font-medium transition-colors hover:text-foreground"
												onclick={() => toggleSort('totalWrote')}
											>
												Wrote{@render sortIcon('totalWrote')}
											</button>
										</Table.Head>
										<Table.Head class="text-right" aria-sort={ariaSort('achieved')}>
											<button
												type="button"
												class="inline-flex items-center gap-1 font-medium transition-colors hover:text-foreground"
												onclick={() => toggleSort('achieved')}
											>
												Achieved{@render sortIcon('achieved')}
											</button>
										</Table.Head>
										<Table.Head class="text-right" aria-sort={ariaSort('percentAchieved')}>
											<button
												type="button"
												class="inline-flex items-center gap-1 font-medium transition-colors hover:text-foreground"
												onclick={() => toggleSort('percentAchieved')}
											>
												Pass rate{@render sortIcon('percentAchieved')}
											</button>
										</Table.Head>
										{#if !isSchoolScope}
											<Table.Head class="text-right" aria-sort={ariaSort('schoolCount')}>
												<button
													type="button"
													class="inline-flex items-center gap-1 font-medium transition-colors hover:text-foreground"
													onclick={() => toggleSort('schoolCount')}
												>
													Schools{@render sortIcon('schoolCount')}
												</button>
											</Table.Head>
										{/if}
									</Table.Row>
								</Table.Header>
								<Table.Body>
									{#each sortedRows as row (row.key)}
										<Table.Row>
											<Table.Cell class="font-medium">
												<a
													href={resolve(rowHref(row.code))}
													class="underline-offset-4 hover:text-primary hover:underline focus-visible:underline focus-visible:outline-none"
												>
													{row.name}
												</a>
											</Table.Cell>
											<Table.Cell class="text-right tabular-nums">
												{row.totalWrote.toLocaleString()}
											</Table.Cell>
											<Table.Cell class="text-right tabular-nums">
												{row.achieved?.toLocaleString() ?? '-'}
											</Table.Cell>
											<Table.Cell class="text-right font-medium tabular-nums">
												{#if row.percentAchieved === null}
													<span class="text-muted-foreground">-</span>
												{:else}
													<span style:color={passRateColor(row.percentAchieved)}>
														{row.percentAchieved.toFixed(1)}%
													</span>
												{/if}
											</Table.Cell>
											{#if !isSchoolScope}
												<Table.Cell class="text-right tabular-nums">
													{row.schoolCount?.toLocaleString() ?? '-'}
												</Table.Cell>
											{/if}
										</Table.Row>
									{/each}
								</Table.Body>
							</Table.Root>
						</Card.Content>
					</Card.Root>
				</div>
			{/if}
		</div>
	</div>
</div>
