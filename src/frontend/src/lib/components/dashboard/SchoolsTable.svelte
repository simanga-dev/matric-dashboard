<script lang="ts">
	import { Card, CardContent, CardHeader, CardTitle } from '$lib/components/ui/card';
	import * as Table from '$lib/components/ui/table';
	import { Input } from '$lib/components/ui/input';
	import { Button } from '$lib/components/ui/button';
	import { Search, ChevronLeft, ChevronRight, ExternalLink } from '@lucide/svelte';
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { fetchSchools } from '$lib/api/dashboard';
	import { schoolPath } from '$lib/config';
	import type { SchoolList, SchoolSort } from '$lib/types/dashboard';
	import { toTitleCase } from '$lib/utils';
	import * as m from '$lib/paraglide/messages';

	interface Props {
		externalSearch?: string;
		title?: string;
		pageSize?: number;
		sortable?: boolean;
	}

	let {
		externalSearch,
		title = 'Schools Performance',
		pageSize = 10,
		sortable = false
	}: Props = $props();

	let schoolData = $state<SchoolList | null>(null);
	let loading = $state(true);
	let error = $state<string | null>(null);
	let searchQuery = $state(externalSearch ?? '');
	let page = $state(1);
	let sort = $state<SchoolSort>('name');

	// One effect drives every fetch: a page, query, or sort change reruns it,
	// and the initial mount is covered too. Calling loadData() explicitly as
	// well used to double every request.
	let request = $derived({ page, search: searchQuery, sort });

	$effect(() => {
		const { page: currentPage, search, sort: currentSort } = request;
		void loadData(currentPage, search, currentSort);
	});

	async function loadData(currentPage: number, query: string, order: SchoolSort) {
		loading = true;
		error = null;
		try {
			schoolData = await fetchSchools(
				currentPage,
				pageSize,
				query || undefined,
				undefined,
				undefined,
				order
			);
		} catch (e) {
			error = e instanceof Error ? e.message : 'Failed to load schools';
		} finally {
			loading = false;
		}
	}

	function handleSearch() {
		page = 1;
	}

	function handleSort(next: SchoolSort) {
		if (sort === next) return;
		sort = next;
		page = 1;
	}

	function goToPage(newPage: number) {
		page = newPage;
	}

	function formatPassRate(rate: number): string {
		return `${rate.toFixed(1)}%`;
	}

	const updatedDate = new Intl.DateTimeFormat(undefined, {
		day: 'numeric',
		month: 'short',
		year: 'numeric'
	});

	function formatUpdated(value: string | null | undefined): string {
		return value ? updatedDate.format(new Date(value)) : '—';
	}

	let items = $derived(schoolData?.items ?? []);
</script>

<Card>
	<CardHeader>
		<div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between sm:gap-4">
			<CardTitle>{title}</CardTitle>
			<div class="flex flex-wrap items-center gap-2">
				{#if sortable}
					<div
						class="flex items-center gap-1 rounded-lg border p-0.5"
						role="group"
						aria-label={m.schools_sortLabel()}
					>
						<Button
							variant={sort === 'name' ? 'secondary' : 'ghost'}
							size="sm"
							class="h-7 px-2 text-xs"
							aria-pressed={sort === 'name'}
							onclick={() => handleSort('name')}
						>
							{m.schools_sortName()}
						</Button>
						<Button
							variant={sort === 'updated' ? 'secondary' : 'ghost'}
							size="sm"
							class="h-7 px-2 text-xs"
							aria-pressed={sort === 'updated'}
							onclick={() => handleSort('updated')}
						>
							{m.schools_sortUpdated()}
						</Button>
					</div>
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
					<Table.Row>
						<Table.Head>School</Table.Head>
						{#if sort === 'updated'}
							<Table.Head class="hidden lg:table-cell">{m.schools_columnUpdated()}</Table.Head>
						{/if}
						<Table.Head class="hidden md:table-cell">Province</Table.Head>
						<Table.Head class="text-right">Wrote</Table.Head>
						<Table.Head class="text-right">Passed</Table.Head>
						<Table.Head class="text-right">Pass Rate</Table.Head>
						<Table.Head class="hidden text-right lg:table-cell">Bachelor</Table.Head>
					</Table.Row>
				</Table.Header>
				<Table.Body>
					{#each items as school (school.id)}
						<Table.Row
							class="cursor-pointer transition-colors hover:bg-muted/50"
							onclick={() => goto(resolve(schoolPath(school.id)))}
							onkeydown={(e) => {
								if (e.key === 'Enter' || e.key === ' ') {
									e.preventDefault();
									goto(resolve(schoolPath(school.id)));
								}
							}}
							role="link"
							tabindex={0}
						>
							<Table.Cell class="font-medium">
								<span class="inline-flex items-center gap-1.5">
									{toTitleCase(school.name)}
									<ExternalLink class="size-3 shrink-0 text-muted-foreground" />
								</span>
							</Table.Cell>
							{#if sort === 'updated'}
								<Table.Cell class="hidden text-sm text-muted-foreground lg:table-cell">
									{#if school.mapUpdatedAt}
										<time
											datetime={school.mapUpdatedAt}
											title={new Date(school.mapUpdatedAt).toLocaleString()}
										>
											{formatUpdated(school.mapUpdatedAt)}
										</time>
									{:else}
										{m.schools_mapNever()}
									{/if}
								</Table.Cell>
							{/if}
							<Table.Cell class="hidden md:table-cell">{toTitleCase(school.province)}</Table.Cell>
							<Table.Cell class="text-right tabular-nums"
								>{school.totalWrote.toLocaleString()}</Table.Cell
							>
							<Table.Cell class="text-right tabular-nums"
								>{school.totalPassed.toLocaleString()}</Table.Cell
							>
							<Table.Cell class="text-right font-medium tabular-nums"
								>{formatPassRate(school.passRate)}</Table.Cell
							>
							<Table.Cell class="hidden text-right tabular-nums lg:table-cell">
								{school.totalAchieved?.toLocaleString() ?? '-'}
							</Table.Cell>
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
