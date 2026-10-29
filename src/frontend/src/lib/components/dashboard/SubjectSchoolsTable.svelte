<script lang="ts">
	import { resolve } from '$app/paths';
	import { EmptyState } from '$lib/components/common';
	import * as Card from '$lib/components/ui/card';
	import * as Table from '$lib/components/ui/table';
	import { Button } from '$lib/components/ui/button';
	import { fetchSubjectSchools } from '$lib/api/dashboard';
	import { schoolPath } from '$lib/config';
	import { passRateColor } from './school-hierarchy';
	import type {
		SchoolSortOrder,
		SubjectSchool,
		SubjectSchoolList,
		SubjectSchoolSort
	} from '$lib/types/dashboard';
	import { cn, toTitleCase } from '$lib/utils';
	import { ArrowDown, ArrowUp, ChevronsUpDown, School as SchoolIcon } from '@lucide/svelte';

	/**
	 * Schools that wrote one subject in a scope and year, ordered server-side.
	 * Ten rows at a time; "Show more" appends the next page. Changing the scope
	 * returns to the first page.
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
		pageSize = 10
	}: Props = $props();

	let list = $state<SubjectSchoolList | null>(null);
	let items = $state<SubjectSchool[]>([]);
	let loading = $state(true);
	let error = $state<string | null>(null);
	let page = $state(1);
	let sort = $state<SubjectSchoolSort>('percentAchieved');
	let order = $state<SchoolSortOrder>('desc');
	let retry = $state(0);
	let controller: AbortController | null = null;

	// A new subject, scope or year is a new result set: start from the first page.
	let scopeKey = $derived(`${code}|${province ?? ''}|${district ?? ''}|${year ?? ''}`);
	let lastScopeKey: string | null = null;
	$effect.pre(() => {
		const key = scopeKey;
		if (lastScopeKey !== null && key !== lastScopeKey) page = 1;
		lastScopeKey = key;
	});

	// One effect drives the first page and each "show more" page.
	$effect(() => {
		void retry;
		const query = {
			province,
			district,
			year,
			pageNumber: page,
			pageSize,
			sort,
			order
		};
		void load(code, query, page === 1);
		return () => controller?.abort();
	});

	async function load(
		subjectCode: string,
		query: Parameters<typeof fetchSubjectSchools>[1],
		replace: boolean
	) {
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
			items = replace ? result.items : [...items, ...result.items];
		} catch (e) {
			if (requestController.signal.aborted) return;
			error = e instanceof Error ? e.message : 'Failed to load schools';
		} finally {
			if (!requestController.signal.aborted) loading = false;
		}
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

	const sortable: Record<'name' | 'totalWrote' | 'percentAchieved', Column> = {
		name: { id: 'name', label: 'School', class: '' },
		totalWrote: { id: 'totalWrote', label: 'Wrote', class: 'w-20 text-right' },
		percentAchieved: { id: 'percentAchieved', label: 'Pass rate', class: 'w-24 text-right' }
	};
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
			<EmptyState icon={SchoolIcon} message={`No schools wrote ${subjectName} in this scope.`} />
		{:else}
			<div
				aria-busy={loading}
				class={cn('motion-safe:transition-opacity', loading && 'pointer-events-none opacity-60')}
			>
				<Table.Root>
					<Table.Header>
						<Table.Row>
							{@render sortHeader(sortable.name)}
							{@render sortHeader(sortable.totalWrote)}
							{@render sortHeader(sortable.percentAchieved)}
						</Table.Row>
					</Table.Header>
					<Table.Body>
						{#each items as school (school.id)}
							<Table.Row class="group">
								<Table.Cell class="max-w-72 font-medium transition-colors group-hover:bg-primary/5">
									<a
										href={resolve(schoolPath(school.id))}
										class="block truncate underline-offset-4 hover:text-primary hover:underline focus-visible:underline focus-visible:outline-none"
									>
										{toTitleCase(school.name)}
									</a>
									<span
										class="mt-0.5 flex items-center gap-1.5 text-xs font-normal text-muted-foreground"
									>
										<span class="min-w-0 truncate">
											{toTitleCase(school.district)}, {toTitleCase(school.province)}
										</span>
										{#if school.quintile !== null && school.quintile > 0}
											<span
												class="shrink-0 rounded border px-1 text-[10px] leading-4 tabular-nums"
												aria-label={`Quintile ${school.quintile}`}
											>
												Q{school.quintile}
											</span>
										{/if}
									</span>
								</Table.Cell>
								<Table.Cell
									class="text-right tabular-nums transition-colors group-hover:bg-primary/5"
								>
									{school.totalWrote.toLocaleString()}
								</Table.Cell>
								<Table.Cell
									class="text-right font-medium tabular-nums transition-colors group-hover:bg-primary/5"
								>
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

			<div class="flex flex-col items-center gap-2 pt-4">
				{#if list?.hasNextPage}
					<Button
						variant="outline"
						size="sm"
						class="w-full"
						disabled={loading}
						onclick={() => (page += 1)}
					>
						Show more
					</Button>
				{/if}
				<p class="text-xs text-muted-foreground">
					Showing {items.length.toLocaleString()} of {(list?.totalCount ?? 0).toLocaleString()} schools
				</p>
			</div>
		{/if}
	</Card.Content>
</Card.Root>
