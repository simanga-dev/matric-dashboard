<script lang="ts">
	import { Card, CardContent, CardHeader, CardTitle } from '$lib/components/ui/card';
	import * as Table from '$lib/components/ui/table';
	import { Input } from '$lib/components/ui/input';
	import { Button } from '$lib/components/ui/button';
	import { Search, ChevronLeft, ChevronRight } from '@lucide/svelte';
	import { onMount } from 'svelte';
	import { fetchSchools } from '$lib/api/dashboard';
	import type { School, SchoolList } from '$lib/types/dashboard';

	interface Props {
		externalSearch?: string;
	}

	let { externalSearch }: Props = $props();

	let schoolData: SchoolList | null = $state(null);
	let loading = $state(true);
	let error = $state<string | null>(null);
	let searchQuery = $state(externalSearch ?? '');
	let page = $state(1);
	const pageSize = 10;

	$effect(() => {
		loadData();
	});

	onMount(() => {
		loadData();
	});

	async function loadData() {
		loading = true;
		error = null;
		try {
			schoolData = await fetchSchools(page, pageSize, searchQuery || undefined);
		} catch (e) {
			error = e instanceof Error ? e.message : 'Failed to load schools';
		} finally {
			loading = false;
		}
	}

	function handleSearch() {
		page = 1;
		loadData();
	}

	function goToPage(newPage: number) {
		page = newPage;
		loadData();
	}

	function formatPassRate(rate: number): string {
		return `${rate.toFixed(1)}%`;
	}

	let items = $derived(schoolData?.items ?? []);
</script>

<Card>
	<CardHeader>
		<div class="flex items-center justify-between gap-4">
			<CardTitle>Schools Performance</CardTitle>
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
						<Table.Row>
							<Table.Cell class="font-medium">{school.name}</Table.Cell>
							<Table.Cell class="hidden md:table-cell">{school.province}</Table.Cell>
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
