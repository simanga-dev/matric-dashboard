<script lang="ts">
	import { untrack } from 'svelte';
	import { page } from '$app/state';
	import { afterNavigate, replaceState } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { SvelteURLSearchParams } from 'svelte/reactivity';
	import { EmptyState } from '$lib/components/common';
	import * as Card from '$lib/components/ui/card';
	import * as Table from '$lib/components/ui/table';
	import * as Select from '$lib/components/ui/select';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import StatCard from '$lib/components/dashboard/StatCard.svelte';
	import SubjectPassRateChart from '$lib/components/dashboard/SubjectPassRateChart.svelte';
	import type { SubjectBar } from '$lib/components/dashboard/subject-bar';
	import YearPills from '$lib/components/dashboard/YearPills.svelte';
	import { passRateColor } from '$lib/components/dashboard/school-hierarchy';
	import { subjectHref } from '$lib/components/dashboard/subject-url';
	import {
		fetchRegionSubjects,
		fetchSchoolById,
		fetchSchoolSubjects,
		fetchSchools
	} from '$lib/api/dashboard';
	import { cn, toTitleCase } from '$lib/utils';
	import type { RegionSubjects, School, SchoolSubjects } from '$lib/types/dashboard';
	import {
		ArrowDown,
		ArrowUp,
		BookMarked,
		Building2,
		ChevronsUpDown,
		Percent,
		Search,
		Users,
		X
	} from '@lucide/svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	const ALL = '__all__';
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

	let searchQuery = $state('');
	let searching = $state(false);
	let searched = $state(false);
	let searchError = $state<string | null>(null);
	let results = $state<School[]>([]);

	let provinces = $derived(
		[...new Set(data.schools.map((school) => school.province))].sort((a, b) => a.localeCompare(b))
	);
	let districts = $derived(
		province
			? [
					...new Set(
						data.schools
							.filter((school) => school.province === province)
							.map((school) => school.circuit)
					)
				].sort((a, b) => a.localeCompare(b))
			: []
	);

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

	let filterQuery = $state('');
	let filteredRows = $derived.by(() => {
		const needle = filterQuery.trim().toLowerCase();
		return needle ? rows.filter((row) => row.name.toLowerCase().includes(needle)) : rows;
	});

	type SortKey = 'name' | 'totalWrote' | 'achieved' | 'percentAchieved' | 'schoolCount';
	let sortKey = $state<SortKey>('name');
	let sortDir = $state<'asc' | 'desc'>('asc');

	let sortedRows = $derived.by(() => {
		const direction = sortDir === 'asc' ? 1 : -1;
		return [...filteredRows].sort((a, b) => {
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
		filteredRows.map((row) => ({
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
		searchQuery = '';
		results = [];
		searched = false;
		searchError = null;
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

	function selectProvince(value: string | undefined) {
		province = value === ALL || !value ? null : value;
		district = null;
		reload();
	}

	function selectDistrict(value: string | undefined) {
		district = value === ALL || !value ? null : value;
		reload();
	}

	function selectYear(value: number) {
		year = value;
		reload(value);
	}

	function selectSchool(school: School) {
		schoolId = school.id;
		schoolName = school.name;
		province = null;
		district = null;
		searchQuery = school.name;
		results = [];
		searched = false;
		searchError = null;
		reload();
	}

	function clearSchool() {
		schoolId = null;
		schoolName = null;
		searchQuery = '';
		reload();
	}

	async function searchSchools() {
		const query = searchQuery.trim();
		if (!query) {
			results = [];
			searched = false;
			searchError = null;
			return;
		}
		searching = true;
		searched = true;
		searchError = null;
		try {
			const list = await fetchSchools({ pageNumber: 1, pageSize: 10, search: query });
			results = list.items;
		} catch (e) {
			results = [];
			searchError = e instanceof Error ? e.message : 'Failed to search schools';
		} finally {
			searching = false;
		}
	}

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
			<div class="space-y-3 px-4 lg:px-6">
				<div class="flex flex-wrap items-center gap-2">
					<Select.Root
						type="single"
						value={province ?? ALL}
						onValueChange={selectProvince}
						disabled={isSchoolScope}
					>
						<Select.Trigger class="h-8 w-48" aria-label="Province">
							<span class={cn(!province && 'text-muted-foreground')}>
								{province ? toTitleCase(province) : 'All provinces'}
							</span>
						</Select.Trigger>
						<Select.Content>
							<Select.Item value={ALL}>All provinces</Select.Item>
							{#each provinces as option (option)}
								<Select.Item value={option}>{toTitleCase(option)}</Select.Item>
							{/each}
						</Select.Content>
					</Select.Root>

					<Select.Root
						type="single"
						value={district ?? ALL}
						onValueChange={selectDistrict}
						disabled={!province || isSchoolScope}
					>
						<Select.Trigger class="h-8 w-52" aria-label="District">
							<span class={cn(!district && 'text-muted-foreground')}>
								{district ? toTitleCase(district) : 'All districts'}
							</span>
						</Select.Trigger>
						<Select.Content>
							<Select.Item value={ALL}>All districts</Select.Item>
							{#each districts as option (option)}
								<Select.Item value={option}>{toTitleCase(option)}</Select.Item>
							{/each}
						</Select.Content>
					</Select.Root>

					{#if isSchoolScope}
						<Button variant="outline" size="sm" onclick={clearSchool}>
							<X class="size-3.5" />
							Clear school
						</Button>
					{/if}

					<form
						class="flex items-center gap-2"
						onsubmit={(event) => {
							event.preventDefault();
							void searchSchools();
						}}
					>
						<Input
							bind:value={searchQuery}
							placeholder="Or search for a school..."
							aria-label="Search schools"
							class="h-8 w-64"
						/>
						<Button
							type="submit"
							variant="outline"
							size="icon-sm"
							disabled={searching}
							aria-label="Search schools"
						>
							<Search class="size-3.5" />
						</Button>
					</form>

					<Input
						bind:value={filterQuery}
						placeholder="Filter subjects..."
						aria-label="Filter subjects by name"
						class="ms-auto h-8 w-56"
					/>
				</div>

				{#if searching}
					<p role="status" class="text-sm text-muted-foreground">Searching…</p>
				{:else if searchError}
					<p role="alert" class="text-sm text-destructive">{searchError}</p>
				{:else if results.length > 0}
					<ul class="divide-y rounded-md border">
						{#each results as result (result.id)}
							<li>
								<button
									type="button"
									class="flex w-full items-center justify-between gap-3 px-3 py-2 text-left text-sm transition-colors hover:bg-muted/50 focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
									onclick={() => selectSchool(result)}
								>
									<span class="min-w-0 flex-1 truncate font-medium">{toTitleCase(result.name)}</span
									>
									<span class="shrink-0 text-xs text-muted-foreground">
										{toTitleCase(result.province)} · {result.totalWrote.toLocaleString()} wrote
									</span>
								</button>
							</li>
						{/each}
					</ul>
				{:else if searched}
					<p class="text-sm text-muted-foreground">No schools match "{searchQuery.trim()}".</p>
				{/if}
			</div>

			<div
				class="grid grid-cols-1 gap-4 px-4 *:data-[slot=card]:bg-gradient-to-t *:data-[slot=card]:from-primary/5 *:data-[slot=card]:to-card *:data-[slot=card]:shadow-xs lg:px-6 @xl/main:grid-cols-2 @5xl/main:grid-cols-4 dark:*:data-[slot=card]:bg-card"
				aria-label="Scope summary"
			>
				<StatCard title="Subjects offered" value={rows.length.toLocaleString()} icon={BookMarked} />
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
			</div>

			<div class="px-4 lg:px-6">
				<Card.Root class="@container/card">
					<Card.Header>
						<div class="flex flex-wrap items-start justify-between gap-3">
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
							{#if availableYears.length > 0}
								<YearPills years={availableYears} selected={year} onSelect={selectYear} />
							{/if}
						</div>
					</Card.Header>
					<Card.Content>
						{#if loading}
							<p role="status" class="flex h-64 items-center justify-center text-muted-foreground">
								Loading subject results…
							</p>
						{:else if error}
							<div class="flex h-64 flex-col items-center justify-center gap-3">
								<p role="alert" class="text-sm text-destructive">{error}</p>
								<Button variant="outline" size="sm" onclick={retryLoad}>Try again</Button>
							</div>
						{:else if availableYears.length === 0}
							<EmptyState icon={BookMarked} message="No subject data for this scope." />
						{:else if filteredRows.length === 0}
							<p class="py-12 text-center text-sm text-muted-foreground">
								No subjects match "{filterQuery.trim()}".
							</p>
						{:else}
							<SubjectPassRateChart subjects={bars} headlineYear={year} />
						{/if}
					</Card.Content>
				</Card.Root>
			</div>

			{#if !loading && !error && rows.length > 0 && filteredRows.length > 0}
				<div class="px-4 lg:px-6">
					<Card.Root>
						<Card.Header>
							<Card.Title class="text-base">Subject results</Card.Title>
							<Card.Description>
								{filteredRows.length} of {rows.length} subjects · {totalWrote.toLocaleString()} wrote
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
