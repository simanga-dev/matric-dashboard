<script lang="ts">
	import { onMount } from 'svelte';
	import { page } from '$app/state';
	import { EmptyState } from '$lib/components/common';
	import * as Card from '$lib/components/ui/card';
	import * as Table from '$lib/components/ui/table';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { fetchSchoolById, fetchSchoolSubjects, fetchSchools } from '$lib/api/dashboard';
	import { passRateColor } from '$lib/components/dashboard/school-hierarchy';
	import { cn, toTitleCase } from '$lib/utils';
	import type { School, SchoolSubjects } from '$lib/types/dashboard';
	import { BookMarked, Search } from '@lucide/svelte';

	const initialSchoolId = page.url.searchParams.get('school');

	let searchQuery = $state('');
	let searching = $state(false);
	let searched = $state(false);
	let searchError = $state<string | null>(null);
	let results = $state<School[]>([]);

	let selectedSchoolId = $state<string | null>(initialSchoolId);
	let selectedSchoolName = $state<string | null>(null);
	let subjects = $state<SchoolSubjects | null>(null);
	let loading = $state(initialSchoolId !== null);
	let error = $state<string | null>(null);
	let year = $state<number | null>(null);
	let requestId = 0;

	let availableYears = $derived(subjects?.availableYears ?? []);
	let rows = $derived((subjects?.subjects ?? []).filter((subject) => subject.totalWrote > 0));

	onMount(() => {
		if (initialSchoolId) void selectSchoolById(initialSchoolId);
	});

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

	function selectSchool(school: School) {
		selectedSchoolId = school.id;
		selectedSchoolName = school.name;
		searchQuery = school.name;
		results = [];
		searched = false;
		searchError = null;
		void loadSubjects(school.id);
	}

	async function selectSchoolById(id: string) {
		selectedSchoolId = id;
		selectedSchoolName = null;
		void loadSubjects(id);
		try {
			const detail = await fetchSchoolById(id);
			if (detail && selectedSchoolId === id) selectedSchoolName = detail.name;
		} catch {
			// Keep the EMIS identifier as the heading; loadSubjects shows the error.
		}
	}

	async function loadSubjects(id: string, requestedYear?: number) {
		const token = ++requestId;
		loading = true;
		error = null;
		try {
			const result = await fetchSchoolSubjects(id, requestedYear);
			if (token !== requestId) return;
			subjects = result;
			year = result.year;
		} catch (e) {
			if (token !== requestId) return;
			subjects = null;
			year = null;
			error = e instanceof Error ? e.message : 'Failed to load subject results';
		} finally {
			if (token === requestId) loading = false;
		}
	}

	function selectYear(value: number) {
		if (!selectedSchoolId) return;
		year = value;
		void loadSubjects(selectedSchoolId, value);
	}

	function retryLoad() {
		if (selectedSchoolId) void loadSubjects(selectedSchoolId, year ?? undefined);
	}
</script>

<svelte:head>
	<title>Subjects • MatricDashboard</title>
	<meta name="description" content="NSC subject performance by school and academic year." />
</svelte:head>

<div class="space-y-6">
	<div>
		<h1 class="text-2xl font-semibold tracking-tight">Subject performance</h1>
		<p class="mt-1 max-w-2xl text-sm text-muted-foreground">
			Per-subject NSC results for a school and academic year. Search for a school to begin.
		</p>
	</div>

	<Card.Root>
		<Card.Header>
			<Card.Title class="flex items-center gap-2 text-base">
				<Search class="size-4 text-primary" />
				Find a school
			</Card.Title>
			<Card.Description
				>Search by school name, then pick a school to load its subjects.</Card.Description
			>
		</Card.Header>
		<Card.Content class="space-y-4">
			<form
				class="flex items-center gap-2"
				onsubmit={(event) => {
					event.preventDefault();
					void searchSchools();
				}}
			>
				<Input
					bind:value={searchQuery}
					placeholder="Search schools..."
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
								<span class="min-w-0 flex-1 truncate font-medium">{toTitleCase(result.name)}</span>
								<span class="shrink-0 text-xs text-muted-foreground">
									{toTitleCase(result.province)} · {result.totalWrote.toLocaleString()} wrote
								</span>
							</button>
						</li>
					{/each}
				</ul>
			{:else if searched}
				<p class="text-sm text-muted-foreground">No schools match "{searchQuery.trim()}".</p>
			{:else}
				<p class="text-sm text-muted-foreground">Type a school name and press Enter to search.</p>
			{/if}
		</Card.Content>
	</Card.Root>

	<Card.Root>
		<Card.Header>
			<Card.Title class="flex items-center gap-2 text-base">
				<BookMarked class="size-4 text-primary" />
				{selectedSchoolName ? toTitleCase(selectedSchoolName) : 'Subjects'}
			</Card.Title>
			<Card.Description>
				{#if selectedSchoolId}
					EMIS {selectedSchoolId}{year ? ` · ${year}` : ''}
				{:else}
					Select a school to see its subject results.
				{/if}
			</Card.Description>
		</Card.Header>
		<Card.Content class="space-y-4">
			{#if !selectedSchoolId}
				<EmptyState icon={Search} message="No school selected yet." />
			{:else if loading}
				<p role="status" class="flex h-48 items-center justify-center text-muted-foreground">
					Loading subject results...
				</p>
			{:else if error}
				<div class="flex h-48 flex-col items-center justify-center gap-3">
					<p role="alert" class="text-sm text-destructive">{error}</p>
					<Button variant="outline" size="sm" onclick={retryLoad}>Try again</Button>
				</div>
			{:else if availableYears.length === 0}
				<EmptyState icon={BookMarked} message="No subject data for this school/year." />
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
						No subject data for this school/year.
					</p>
				{:else}
					<Table.Root>
						<Table.Header>
							<Table.Row>
								<Table.Head>Subject</Table.Head>
								<Table.Head class="text-right">Wrote</Table.Head>
								<Table.Head class="text-right">Achieved</Table.Head>
								<Table.Head class="text-right">Pass rate</Table.Head>
							</Table.Row>
						</Table.Header>
						<Table.Body>
							{#each rows as subject (subject.subjectId)}
								<Table.Row>
									<Table.Cell class="font-medium">{subject.name}</Table.Cell>
									<Table.Cell class="text-right tabular-nums">
										{subject.totalWrote.toLocaleString()}
									</Table.Cell>
									<Table.Cell class="text-right tabular-nums">
										{subject.achieved?.toLocaleString() ?? '-'}
									</Table.Cell>
									<Table.Cell class="text-right font-medium tabular-nums">
										{#if subject.percentAchieved === null}
											<span class="text-muted-foreground">-</span>
										{:else}
											<span style:color={passRateColor(subject.percentAchieved)}>
												{subject.percentAchieved.toFixed(1)}%
											</span>
										{/if}
									</Table.Cell>
								</Table.Row>
							{/each}
						</Table.Body>
					</Table.Root>
				{/if}
			{/if}
		</Card.Content>
	</Card.Root>
</div>
