<script lang="ts">
	import { resolve } from '$app/paths';
	import { EmptyState } from '$lib/components/common';
	import * as Card from '$lib/components/ui/card';
	import * as Table from '$lib/components/ui/table';
	import { Input } from '$lib/components/ui/input';
	import { Button } from '$lib/components/ui/button';
	import { fetchSubjectSchools } from '$lib/api/dashboard';
	import { schoolPath } from '$lib/config';
	import { passRateColor } from './school-hierarchy';
	import type { SchoolSortOrder, SubjectSchoolList, SubjectSchoolSort } from '$lib/types/dashboard';
	import { cn, toTitleCase } from '$lib/utils';
	import {
		ArrowDown,
		ArrowUp,
		ChevronLeft,
		ChevronRight,
		ChevronsUpDown,
		School as SchoolIcon,
		Search
	} from '@lucide/svelte';

	/**
	 * Schools that wrote one subject in a scope and year. Search, paging and
	 * ordering are server-side so the whole result set is ordered, not just the
	 * loaded page. Changing the scope returns to the first page.
	 */
	interface Props {
		code: string;
		subjectName: string;
		province?: string | null;
		district?: string | null;
		year?: number | null;
		pageSize?: number;
	}

	let {
		code,
		subjectName,
		province = null,
		district = null,
		year = null,
		pageSize = 20
	}: Props = $props();

	let list = $state<SubjectSchoolList | null>(null);
	let loading = $state(true);
	let error = $state<string | null>(null);
	let searchQuery = $state('');
	let appliedSearch = $state('');
	let page = $state(1);
	let sort = $state<SubjectSchoolSort>('percentAchieved');
	let order = $state<SchoolSortOrder>('desc');
	let retry = $state(0);
	let controller: AbortController | null = null;

	// A new subject, scope or year is a new result set: start from page one.
	let scopeKey = $derived(`${code}|${province ?? ''}|${district ?? ''}|${year ?? ''}`);
	let lastScopeKey: string | null = null;
	$effect.pre(() => {
		const key = scopeKey;
		if (lastScopeKey !== null && key !== lastScopeKey) page = 1;
		lastScopeKey = key;
	});

	// Debounce typing so a request is not issued per keystroke.
	$effect(() => {
		const query = searchQuery.trim();
		const timer = setTimeout(() => {
			if (query === appliedSearch) return;
			appliedSearch = query;
			page = 1;
		}, 250);
		return () => clearTimeout(timer);
	});

	// One effect drives every fetch, including the first.
	$effect(() => {
		void retry;
		const query = {
			province,
			district,
			year,
			search: appliedSearch || undefined,
			pageNumber: page,
			pageSize,
			sort,
			order
		};
		void load(code, query);
		return () => controller?.abort();
	});

	async function load(subjectCode: string, query: Parameters<typeof fetchSubjectSchools>[1]) {
		controller?.abort();
		const requestController = new AbortController();
		controller = requestController;
		loading = true;
		error = null;
		try {
			const result = await fetchSubjectSchools(
				subjectCode,
				query,
				undefined,
				requestController.signal
			);
			if (requestController.signal.aborted) return;
			list = result;
		} catch (e) {
			if (requestController.signal.aborted) return;
			error = e instanceof Error ? e.message : 'Failed to load schools';
		} finally {
			if (!requestController.signal.aborted) loading = false;
		}
	}

	/** Applies the typed search immediately instead of waiting for the debounce. */
	function applySearch() {
		appliedSearch = searchQuery.trim();
		page = 1;
	}

	const descFirst: Record<SubjectSchoolSort, boolean> = {
		name: false,
		province: false,
		totalWrote: true,
		achieved: true,
		percentAchieved: true
	};

	function toggleSort(column: SubjectSchoolSort) {
		if (sort === column) order = order === 'asc' ? 'desc' : 'asc';
		else {
			sort = column;
			order = descFirst[column] ? 'desc' : 'asc';
		}
		page = 1;
	}

	function ariaSort(column: SubjectSchoolSort): 'ascending' | 'descending' | 'none' {
		if (sort !== column) return 'none';
		return order === 'asc' ? 'ascending' : 'descending';
	}

	interface Column {
		id: SubjectSchoolSort;
		label: string;
		class: string;
	}

	const sortable: Record<SubjectSchoolSort, Column> = {
		name: { id: 'name', label: 'School', class: '' },
		province: { id: 'province', label: 'Province / district', class: 'hidden md:table-cell' },
		totalWrote: { id: 'totalWrote', label: 'Wrote', class: 'text-right' },
		achieved: { id: 'achieved', label: 'Achieved', class: 'hidden text-right sm:table-cell' },
		percentAchieved: { id: 'percentAchieved', label: 'Pass rate', class: 'text-right' }
	};

	let items = $derived(list?.items ?? []);
	let hasSearch = $derived(appliedSearch.length > 0);
</script>

{#snippet sortHeader(column: Column)}
	<Table.Head class={column.class} aria-sort={ariaSort(column.id)}>
		<button
			type="button"
			class={cn(
				'inline-flex items-center gap-1 font-medium transition-colors hover:text-foreground',
				column.class.includes('text-right') && 'flex-row-reverse'
			)}
			onclick={() => toggleSort(column.id)}
		>
			{column.label}
			{#if sort === column.id && order === 'asc'}
				<ArrowUp class="size-3" />
			{:else if sort === column.id}
				<ArrowDown class="size-3" />
			{:else}
				<ChevronsUpDown class="size-3 text-muted-foreground/50" />
			{/if}
		</button>
	</Table.Head>
{/snippet}

<Card.Root>
	<Card.Header>
		<div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between sm:gap-4">
			<div>
				<Card.Title class="flex items-center gap-2 text-base">
					<SchoolIcon class="size-4 text-primary" />
					Schools that wrote {subjectName}
				</Card.Title>
				<Card.Description>
					{list?.year ? `${list.year} results` : 'Latest results'}{list
						? ` · ${list.totalCount.toLocaleString()} schools`
						: ''}
				</Card.Description>
			</div>
			<form
				class="flex items-center gap-2"
				onsubmit={(event) => {
					event.preventDefault();
					applySearch();
				}}
			>
				<Input
					bind:value={searchQuery}
					placeholder="Search schools..."
					aria-label="Search schools that wrote {subjectName}"
					class="h-8 w-full sm:w-56"
				/>
				<Button
					type="submit"
					variant="outline"
					size="icon-sm"
					aria-label="Search schools"
					disabled={loading}
				>
					<Search class="size-3.5" />
				</Button>
			</form>
		</div>
	</Card.Header>
	<Card.Content>
		{#if loading && !list}
			<p role="status" class="flex h-48 items-center justify-center text-sm text-muted-foreground">
				Loading schools...
			</p>
		{:else if error}
			<div class="flex h-48 flex-col items-center justify-center gap-3">
				<p role="alert" class="text-sm text-destructive">{error}</p>
				<Button variant="outline" size="sm" onclick={() => retry++}>Try again</Button>
			</div>
		{:else if items.length === 0}
			<EmptyState
				icon={SchoolIcon}
				message={hasSearch
					? `No schools match "${appliedSearch}".`
					: `No schools wrote ${subjectName} in this scope.`}
			/>
		{:else}
			<div
				aria-busy={loading}
				class={cn('motion-safe:transition-opacity', loading && 'pointer-events-none opacity-60')}
			>
				<Table.Root>
					<Table.Header>
						<Table.Row>
							{@render sortHeader(sortable.name)}
							{@render sortHeader(sortable.province)}
							<Table.Head class="hidden text-right lg:table-cell">Quintile</Table.Head>
							{@render sortHeader(sortable.totalWrote)}
							{@render sortHeader(sortable.achieved)}
							{@render sortHeader(sortable.percentAchieved)}
						</Table.Row>
					</Table.Header>
					<Table.Body>
						{#each items as school (school.id)}
							<Table.Row>
								<Table.Cell class="max-w-64 font-medium">
									<a
										href={resolve(schoolPath(school.id))}
										class="block truncate underline-offset-4 hover:text-primary hover:underline focus-visible:underline focus-visible:outline-none"
									>
										{toTitleCase(school.name)}
									</a>
									<span class="block truncate text-xs font-normal text-muted-foreground md:hidden">
										{toTitleCase(school.district)}, {toTitleCase(school.province)}
									</span>
								</Table.Cell>
								<Table.Cell class="hidden md:table-cell">
									<span class="block">{toTitleCase(school.province)}</span>
									<span class="block text-xs text-muted-foreground">
										{toTitleCase(school.district)}
									</span>
								</Table.Cell>
								<Table.Cell class="hidden text-right tabular-nums lg:table-cell">
									{school.quintile > 0 ? school.quintile : '-'}
								</Table.Cell>
								<Table.Cell class="text-right tabular-nums">
									{school.totalWrote.toLocaleString()}
								</Table.Cell>
								<Table.Cell class="hidden text-right tabular-nums sm:table-cell">
									{school.achieved?.toLocaleString() ?? '-'}
								</Table.Cell>
								<Table.Cell class="text-right font-medium tabular-nums">
									{#if school.percentAchieved === null}
										<span class="text-muted-foreground">-</span>
									{:else}
										<span style:color={passRateColor(school.percentAchieved)}>
											{school.percentAchieved.toFixed(1)}%
										</span>
									{/if}
								</Table.Cell>
							</Table.Row>
						{/each}
					</Table.Body>
				</Table.Root>
			</div>

			<div class="flex flex-col gap-3 pt-4 sm:flex-row sm:items-center sm:justify-between">
				<p class="text-sm text-muted-foreground">
					Page {list?.pageNumber ?? 1} of {Math.max(1, list?.totalPages ?? 1)}
					({(list?.totalCount ?? 0).toLocaleString()} schools)
				</p>
				<div class="flex items-center gap-2 sm:justify-end">
					<Button
						variant="outline"
						size="sm"
						disabled={!list?.hasPreviousPage || loading}
						onclick={() => (page = Math.max(1, page - 1))}
					>
						<ChevronLeft class="size-4" />
						Previous
					</Button>
					<Button
						variant="outline"
						size="sm"
						disabled={!list?.hasNextPage || loading}
						onclick={() => (page += 1)}
					>
						Next
						<ChevronRight class="size-4" />
					</Button>
				</div>
			</div>
		{/if}
	</Card.Content>
</Card.Root>
