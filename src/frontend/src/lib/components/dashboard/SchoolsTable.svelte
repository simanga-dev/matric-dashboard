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
	import type { SchoolList } from '$lib/types/dashboard';
	import { toTitleCase } from '$lib/utils';

	interface Props {
		externalSearch?: string;
		title?: string;
		pageSize?: number;
	}

	let { externalSearch, title = 'Schools Performance', pageSize = 10 }: Props = $props();

	let schoolData = $state<SchoolList | null>(null);
	let loading = $state(true);
	let error = $state<string | null>(null);
	let searchQuery = $state(externalSearch ?? '');
	let page = $state(1);

	// One effect drives every fetch: a page or query change reruns it, and the
	// initial mount is covered too. Calling loadData() explicitly as well used
	// to double every request.
	let request = $derived({ page, search: searchQuery });

	$effect(() => {
		const { page: currentPage, search } = request;
		void loadData(currentPage, search);
	});

	async function loadData(currentPage: number, query: string) {
		loading = true;
		error = null;
		try {
			schoolData = await fetchSchools(currentPage, pageSize, query || undefined);
		} catch (e) {
			error = e instanceof Error ? e.message : 'Failed to load schools';
		} finally {
			loading = false;
		}
	}

	function handleSearch() {
		page = 1;
	}

	function goToPage(newPage: number) {
		page = newPage;
	}

	function formatPassRate(rate: number): string {
		return `${rate.toFixed(1)}%`;
	}

	let items = $derived(schoolData?.items ?? []);
</script>

<Card>
	<CardHeader>
		<div class="flex items-center justify-between gap-4">
			<CardTitle>{title}</CardTitle>
			<div class="flex items-center gap-2">
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
