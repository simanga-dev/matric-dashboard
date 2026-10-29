<script lang="ts">
	import { Card, CardContent, CardHeader, CardTitle } from '$lib/components/ui/card';
	import * as Table from '$lib/components/ui/table';
	import { Input } from '$lib/components/ui/input';
	import { Button } from '$lib/components/ui/button';
	import * as Select from '$lib/components/ui/select';
	import { untrack } from 'svelte';
	import {
		Search,
		ChevronLeft,
		ChevronRight,
		ChevronsUpDown,
		ArrowDown,
		ArrowUp,
		ExternalLink
	} from '@lucide/svelte';
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { fetchSchools } from '$lib/api/dashboard';
	import { schoolPath } from '$lib/config';
	import type { School, SchoolList, SchoolSort, SchoolSortOrder } from '$lib/types/dashboard';
	import { cn, toTitleCase } from '$lib/utils';
	import type { SchoolsViewQuery } from './schools-url';
	import * as m from '$lib/paraglide/messages';
	import {
		createTable,
		createColumnHelper,
		tableFeatures,
		rowSortingFeature,
		functionalUpdate,
		FlexRender,
		type SortingState
	} from '@tanstack/svelte-table';

	interface Props {
		/** Search text from the route; followed when it changes. */
		externalSearch?: string;
		title?: string;
		pageSize?: number;
		/** Province filter from the route (exact name, e.g. "GAUTENG"); followed when it changes. */
		externalProvince?: string | null;
		/** Province options. When given, a province filter is shown. */
		provinces?: string[];
		/** Sort column from the route; followed when it changes. */
		externalSort?: SchoolSort | null;
		/** Sort direction from the route; followed when it changes. */
		externalOrder?: SchoolSortOrder | null;
		/** Page number from the route; followed when it changes. */
		externalPage?: number;
		/** Called whenever the applied search, province, sort or page changes, e.g. to sync the URL. */
		onQueryChange?: (query: SchoolsViewQuery) => void;
	}

	let {
		externalSearch,
		title = 'Schools Performance',
		pageSize = 10,
		externalProvince = null,
		provinces,
		externalSort = null,
		externalOrder = null,
		externalPage = 1,
		onQueryChange
	}: Props = $props();

	const ALL_PROVINCES = '__all__';

	let schoolData = $state<SchoolList | null>(null);
	let loading = $state(true);
	let error = $state<string | null>(null);
	// Snapshot the deep-link values once; the effects below follow later route changes.
	const initialSearch = untrack(() => externalSearch ?? '');
	let searchQuery = $state(initialSearch);
	let appliedSearch = $state(initialSearch);
	// Writable deriveds follow the route value and accept local overrides until it changes.
	let province = $derived<string | null>(externalProvince);
	let page = $derived(externalPage);
	let sorting = $state<SortingState>(untrack(() => toSorting(externalSort, externalOrder)));
	let controller: AbortController | null = null;

	function toSorting(sort: SchoolSort | null, order: SchoolSortOrder | null): SortingState {
		return sort ? [{ id: sort, desc: order === 'desc' }] : [];
	}

	// Follow route-level deep links such as /schools?search=pretoria.
	$effect(() => {
		const incoming = externalSearch ?? '';
		searchQuery = incoming;
		appliedSearch = incoming;
	});

	$effect(() => {
		const next = toSorting(externalSort, externalOrder);
		const current = untrack(() => sorting[0]);
		// Only replace the state on a real change so an unchanged route does not refetch.
		if (current?.id !== next[0]?.id || current?.desc !== next[0]?.desc) sorting = next;
	});

	// Debounce typing so a directory request is not issued per keystroke.
	$effect(() => {
		const query = searchQuery;
		const timer = setTimeout(() => {
			if (query === appliedSearch) return;
			appliedSearch = query;
			page = 1;
		}, 250);
		return () => clearTimeout(timer);
	});

	const features = tableFeatures({ rowSortingFeature });
	const columnHelper = createColumnHelper<typeof features, School>();

	const nameColumn = columnHelper.accessor('name', { header: 'School' });
	const mapColumn = columnHelper.accessor('mapUpdatedAt', {
		header: () => m.schools_columnUpdated(),
		sortDescFirst: true,
		sortUndefined: 'last'
	});
	const provinceColumn = columnHelper.accessor('province', {
		header: 'Province',
		sortDescFirst: false
	});
	const wroteColumn = columnHelper.accessor('totalWrote', {
		header: 'Wrote',
		sortDescFirst: true
	});
	const passedColumn = columnHelper.accessor('totalPassed', {
		header: 'Passed',
		sortDescFirst: true
	});
	const rateColumn = columnHelper.accessor('passRate', {
		header: 'Pass Rate',
		sortDescFirst: true
	});
	const bachelorColumn = columnHelper.accessor('totalAchieved', {
		header: 'Bachelor',
		enableSorting: false
	});

	const columns = columnHelper.columns([
		nameColumn,
		mapColumn,
		provinceColumn,
		wroteColumn,
		passedColumn,
		rateColumn,
		bachelorColumn
	]);

	const headerClass: Record<string, string> = {
		province: 'hidden md:table-cell',
		totalWrote: 'text-right',
		totalPassed: 'text-right',
		passRate: 'text-right',
		totalAchieved: 'hidden text-right lg:table-cell',
		mapUpdatedAt: 'hidden lg:table-cell'
	};

	const cellClass: Record<string, string> = {
		province: 'hidden md:table-cell',
		totalWrote: 'text-right tabular-nums',
		totalPassed: 'text-right tabular-nums',
		passRate: 'text-right font-medium tabular-nums',
		totalAchieved: 'hidden text-right tabular-nums lg:table-cell',
		mapUpdatedAt: 'hidden text-sm text-muted-foreground lg:table-cell'
	};

	let items = $derived(schoolData?.items ?? []);

	// Sorting is server-owned so every page of the whole directory is ordered,
	// not just the rows currently loaded.
	const table = createTable({
		features,
		columns,
		get data() {
			return items;
		},
		getRowId: (row) => row.id,
		manualSorting: true,
		enableSortingRemoval: true,
		get state() {
			return { sorting };
		},
		onSortingChange: (updater) => {
			sorting = functionalUpdate(updater, sorting);
			page = 1;
		}
	});

	// One effect drives every fetch: page, applied search, or column sort changes
	// rerun it, and the initial mount is covered too.
	let request = $derived({
		page,
		search: appliedSearch,
		province,
		sort: sorting[0]?.id as SchoolSort | undefined,
		order: (sorting[0] ? (sorting[0].desc ? 'desc' : 'asc') : undefined) as
			| SchoolSortOrder
			| undefined
	});

	$effect(() => {
		const { page: currentPage, search, province: currentProvince, sort, order } = request;
		void loadData(currentPage, search, currentProvince, sort, order);
		return () => controller?.abort();
	});

	// Report the applied view so the route can mirror it in the URL.
	$effect(() => {
		const query: SchoolsViewQuery = {
			search: request.search,
			province: request.province,
			sort: request.sort ?? null,
			order: request.order ?? null,
			page: request.page
		};
		untrack(() => onQueryChange?.(query));
	});

	async function loadData(
		currentPage: number,
		query: string,
		provinceFilter: string | null,
		sort: SchoolSort | undefined,
		order: SchoolSortOrder | undefined
	) {
		controller?.abort();
		const requestController = new AbortController();
		controller = requestController;
		loading = true;
		error = null;
		try {
			const result = await fetchSchools(
				{
					pageNumber: currentPage,
					pageSize,
					search: query || undefined,
					province: provinceFilter ?? undefined,
					sort,
					order
				},
				undefined,
				requestController.signal
			);
			if (requestController.signal.aborted) return;
			schoolData = result;
		} catch (e) {
			if (requestController.signal.aborted) return;
			error = e instanceof Error ? e.message : 'Failed to load schools';
		} finally {
			if (!requestController.signal.aborted) loading = false;
		}
	}

	/** Applies the current query immediately instead of waiting for the debounce. */
	function handleSearch() {
		appliedSearch = searchQuery;
		page = 1;
	}

	function goToPage(newPage: number) {
		page = newPage;
	}

	function selectProvince(value: string | undefined) {
		province = !value || value === ALL_PROVINCES ? null : value;
		page = 1;
	}

	// Keep a deep-linked province selectable before (or without) the option list.
	let provinceOptions = $derived(
		province && provinces && !provinces.includes(province) ? [province, ...provinces] : provinces
	);

	function formatPassRate(rate: number): string {
		return `${rate.toFixed(1)}%`;
	}

	const updatedDate = new Intl.DateTimeFormat(undefined, {
		day: 'numeric',
		month: 'short',
		year: 'numeric'
	});

	function formatUpdated(value: string): string {
		return updatedDate.format(new Date(value));
	}

	function ariaSort(column: {
		getCanSort: () => boolean;
		getIsSorted: () => false | 'asc' | 'desc';
	}): 'ascending' | 'descending' | 'none' | undefined {
		if (!column.getCanSort()) return undefined;
		const sorted = column.getIsSorted();
		return sorted === 'asc' ? 'ascending' : sorted === 'desc' ? 'descending' : 'none';
	}
</script>

<Card>
	<CardHeader>
		<div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between sm:gap-4">
			<CardTitle>{title}</CardTitle>
			<div class="flex flex-wrap items-center gap-2">
				{#if provinceOptions}
					<Select.Root
						type="single"
						value={province ?? ALL_PROVINCES}
						onValueChange={selectProvince}
					>
						<Select.Trigger class="h-8 w-full sm:w-44" aria-label="Province">
							<span class={cn(!province && 'text-muted-foreground')}>
								{province ? toTitleCase(province) : 'All provinces'}
							</span>
						</Select.Trigger>
						<Select.Content>
							<Select.Item value={ALL_PROVINCES}>All provinces</Select.Item>
							{#each provinceOptions as option (option)}
								<Select.Item value={option}>{toTitleCase(option)}</Select.Item>
							{/each}
						</Select.Content>
					</Select.Root>
				{/if}
				<Input
					bind:value={searchQuery}
					placeholder="Search schools..."
					class="h-8 w-48"
					onkeydown={(e) => e.key === 'Enter' && handleSearch()}
				/>
				<Button
					variant="outline"
					size="icon"
					class="h-8 w-8"
					onclick={handleSearch}
					disabled={loading}
				>
					<Search class="size-3" />
				</Button>
			</div>
		</div>
	</CardHeader>
	<CardContent>
		{#if loading}
			<div class="flex h-48 items-center justify-center text-muted-foreground">Loading...</div>
		{:else if error}
			<div class="flex h-48 items-center justify-center text-destructive">{error}</div>
		{:else}
			<Table.Root>
				<Table.Header>
					{#each table.getHeaderGroups() as headerGroup (headerGroup.id)}
						<Table.Row>
							{#each headerGroup.headers as header (header.id)}
								<Table.Head
									class={headerClass[header.column.id] ?? ''}
									aria-sort={ariaSort(header.column)}
								>
									{#if header.column.getCanSort()}
										<button
											type="button"
											class="inline-flex items-center gap-1 font-medium transition-colors hover:text-foreground"
											onclick={(event) => header.column.getToggleSortingHandler()?.(event)}
										>
											<FlexRender {header} />
											{#if header.column.getIsSorted() === 'asc'}
												<ArrowUp class="size-3" />
											{:else if header.column.getIsSorted() === 'desc'}
												<ArrowDown class="size-3" />
											{:else}
												<ChevronsUpDown class="size-3 text-muted-foreground/50" />
											{/if}
										</button>
									{:else}
										<FlexRender {header} />
									{/if}
								</Table.Head>
							{/each}
						</Table.Row>
					{/each}
				</Table.Header>
				<Table.Body>
					{#each table.getRowModel().rows as row (row.id)}
						<Table.Row
							class="cursor-pointer transition-colors hover:bg-muted/50"
							onclick={() => goto(resolve(schoolPath(row.original.id)))}
							onkeydown={(e) => {
								if (e.key === 'Enter' || e.key === ' ') {
									e.preventDefault();
									goto(resolve(schoolPath(row.original.id)));
								}
							}}
							role="link"
							tabindex={0}
						>
							{#each row.getAllCells() as cell (cell.id)}
								<Table.Cell class={cellClass[cell.column.id] ?? ''}>
									{#if cell.column.id === 'name'}
										<span class="inline-flex items-center gap-1.5">
											{toTitleCase(String(cell.getValue()))}
											<ExternalLink class="size-3 shrink-0 text-muted-foreground" />
										</span>
									{:else if cell.column.id === 'province'}
										{toTitleCase(String(cell.getValue() ?? ''))}
									{:else if cell.column.id === 'mapUpdatedAt'}
										{#if cell.getValue()}
											<time
												datetime={String(cell.getValue())}
												title={new Date(String(cell.getValue())).toLocaleString()}
											>
												{formatUpdated(String(cell.getValue()))}
											</time>
										{:else}
											{m.schools_mapImported()}
										{/if}
									{:else if cell.column.id === 'passRate'}
										{formatPassRate(Number(cell.getValue() ?? 0))}
									{:else if cell.column.id === 'totalWrote' || cell.column.id === 'totalPassed'}
										{Number(cell.getValue() ?? 0).toLocaleString()}
									{:else}
										{cell.getValue<number>()?.toLocaleString() ?? '-'}
									{/if}
								</Table.Cell>
							{/each}
						</Table.Row>
					{/each}
				</Table.Body>
			</Table.Root>

			<div class="flex items-center justify-between gap-4 pt-4">
				<div class="text-sm text-muted-foreground">
					Page {schoolData?.pageNumber ?? 1} of {schoolData?.totalPages ?? 1}
					({schoolData?.totalCount ?? 0} schools)
				</div>
				<div class="flex items-center gap-2">
					<Button
						variant="outline"
						size="sm"
						disabled={!schoolData?.hasPreviousPage || loading}
						onclick={() => goToPage(page - 1)}
					>
						<ChevronLeft class="size-4" />
						Previous
					</Button>
					<Button
						variant="outline"
						size="sm"
						disabled={!schoolData?.hasNextPage || loading}
						onclick={() => goToPage(page + 1)}
					>
						Next
						<ChevronRight class="size-4" />
					</Button>
				</div>
			</div>
		{/if}
	</CardContent>
</Card>
