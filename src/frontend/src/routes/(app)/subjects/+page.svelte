<script lang="ts">
	import { onMount } from 'svelte';
	import { page } from '$app/state';
	import { browser } from '$app/environment';
	import { replaceState } from '$app/navigation';
	import { EmptyState } from '$lib/components/common';
	import * as Card from '$lib/components/ui/card';
	import * as Table from '$lib/components/ui/table';
	import * as Select from '$lib/components/ui/select';
	import { Separator } from '$lib/components/ui/separator';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import SubjectPerformanceBars, {
		type SubjectBar
	} from '$lib/components/dashboard/SubjectPerformanceBars.svelte';
	import { passRateColor } from '$lib/components/dashboard/school-hierarchy';
	import {
		fetchRegionSubjects,
		fetchSchoolById,
		fetchSchoolSubjects,
		fetchSchools
	} from '$lib/api/dashboard';
	import { cn, toTitleCase } from '$lib/utils';
	import type { RegionSubjects, School, SchoolSubjects } from '$lib/types/dashboard';
	import { BookMarked, MapPin, Search, X } from '@lucide/svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	const ALL = '__all__';
	const searchParams = page.url.searchParams;
	const initialSchoolId = searchParams.get('school');
	const initialYear = Number(searchParams.get('year')) || null;

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
						name: subject.name,
						totalWrote: subject.totalWrote,
						achieved: subject.achieved,
						percentAchieved: subject.percentAchieved,
						schoolCount: null
					}))
			: (region?.subjects ?? []).map((subject) => ({
					key: subject.code,
					name: subject.name,
					totalWrote: subject.totalWrote,
					achieved: subject.achieved,
					percentAchieved: subject.percentAchieved,
					schoolCount: subject.schoolCount
				}))
	);

	let bars = $derived<SubjectBar[]>(
		rows.map((row) => ({
			name: row.name,
			totalWrote: row.totalWrote,
			percentAchieved: row.percentAchieved
		}))
	);

	let totalWrote = $derived(rows.reduce((sum, row) => sum + row.totalWrote, 0));

	onMount(() => {
		if (schoolId) void loadSchool(schoolId, initialYear);
		else void loadRegion(initialYear);
		ready = true;
	});

	let ready = $state(false);

	// Keep the scope in the URL so the view can be shared or bookmarked.
	$effect(() => {
		if (!browser || !ready) return;
		const params = new URLSearchParams();
		if (schoolId) params.set('school', schoolId);
		else {
			if (province) params.set('province', province);
			if (district) params.set('district', district);
		}
		if (year) params.set('year', String(year));
		const query = params.toString();
		replaceState(query ? `/subjects?${query}` : '/subjects', page.state);
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
			const list = await fetchSchools(1, 10, query);
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

<svelte:head>
	<title>Subjects • MatricDashboard</title>
	<meta name="description" content="NSC subject performance by province, district or school." />
</svelte:head>

<div class="space-y-6">
	<div>
		<h1 class="text-2xl font-semibold tracking-tight">Subject performance</h1>
		<p class="mt-1 max-w-2xl text-sm text-muted-foreground">
			NSC results per subject for the whole country, a province, a district or a single school.
			Narrow the scope below.
		</p>
	</div>

	<Card.Root>
		<Card.Header>
			<Card.Title class="flex items-center gap-2 text-base">
				<MapPin class="size-4 text-primary" />
				Scope
			</Card.Title>
			<Card.Description
				>Drill from national down to a province, district or school.</Card.Description
			>
		</Card.Header>
		<Card.Content class="space-y-4">
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
			</div>

			<Separator />

			<div class="space-y-3">
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
						class="h-8 max-w-sm"
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

				{#if searching}
					<p role="status" class="text-sm text-muted-foreground">Searching...</p>
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
		</Card.Content>
	</Card.Root>

	<Card.Root>
		<Card.Header>
			<Card.Title class="flex items-center gap-2 text-base">
				<BookMarked class="size-4 text-primary" />
				{scopeName}
			</Card.Title>
			<Card.Description>
				{scopeKind}{year ? ` · ${year}` : ''}{schoolCount !== null
					? ` · ${schoolCount.toLocaleString()} schools`
					: ''}
			</Card.Description>
		</Card.Header>
		<Card.Content class="space-y-4">
			{#if loading}
				<p role="status" class="flex h-48 items-center justify-center text-muted-foreground">
					Loading subject results...
				</p>
			{:else if error}
				<div class="flex h-48 flex-col items-center justify-center gap-3">
					<p role="alert" class="text-sm text-destructive">{error}</p>
					<Button variant="outline" size="sm" onclick={retryLoad}>Try again</Button>
				</div>
			{:else if availableYears.length === 0}
				<EmptyState icon={BookMarked} message="No subject data for this scope." />
			{:else}
				<div class="flex flex-wrap items-center gap-2">
					<span class="text-xs font-medium text-muted-foreground">Year</span>
					{#each availableYears as availableYear (availableYear)}
						<button
							type="button"
							aria-pressed={year === availableYear}
							onclick={() => selectYear(availableYear)}
							class={cn(
								'rounded-full border px-3 py-1 text-xs font-medium tabular-nums transition-colors',
								year === availableYear
									? 'border-transparent bg-primary text-primary-foreground'
									: 'bg-background hover:bg-muted'
							)}
						>
							{availableYear}
						</button>
					{/each}
				</div>

				{#if rows.length === 0}
					<p class="py-12 text-center text-sm text-muted-foreground">
						No subject data for this scope/year.
					</p>
				{:else}
					<section class="rounded-lg border bg-muted/20 p-4">
						<div class="mb-3 flex flex-wrap items-baseline justify-between gap-2">
							<h3 class="text-sm font-semibold">Pass rate by subject</h3>
							<span class="text-xs text-muted-foreground tabular-nums">
								{rows.length} subjects · {totalWrote.toLocaleString()} wrote
							</span>
						</div>
						<SubjectPerformanceBars subjects={bars} headlineYear={year} />
					</section>

					<Table.Root>
						<Table.Header>
							<Table.Row>
								<Table.Head>Subject</Table.Head>
								<Table.Head class="text-right">Wrote</Table.Head>
								<Table.Head class="text-right">Achieved</Table.Head>
								<Table.Head class="text-right">Pass rate</Table.Head>
								{#if !isSchoolScope}
									<Table.Head class="text-right">Schools</Table.Head>
								{/if}
							</Table.Row>
						</Table.Header>
						<Table.Body>
							{#each rows as row (row.key)}
								<Table.Row>
									<Table.Cell class="font-medium">{row.name}</Table.Cell>
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
				{/if}
			{/if}
		</Card.Content>
	</Card.Root>
</div>
