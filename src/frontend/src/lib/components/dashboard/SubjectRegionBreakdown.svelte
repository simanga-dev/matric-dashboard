<script lang="ts">
	import * as Table from '$lib/components/ui/table';
	import { passRateBarClass, passRateColor } from './school-hierarchy';
	import type { SubjectRegionBreakdown } from '$lib/types/dashboard';
	import { cn, toTitleCase } from '$lib/utils';
	import { ChevronRight } from '@lucide/svelte';

	/**
	 * Per-province or per-district results for one subject. Rows are ordered by
	 * pass rate (unavailable results last) and each row drills into its region.
	 */
	interface Props {
		rows: SubjectRegionBreakdown[];
		/** Region level shown in the first column and in accessible labels. */
		kind: 'province' | 'district';
		/** Currently selected region, highlighted in the table. */
		selected?: string | null;
		onSelect: (name: string) => void;
	}

	let { rows, kind, selected = null, onSelect }: Props = $props();

	let ordered = $derived(
		[...rows].sort((a, b) => {
			const left = a.percentAchieved ?? -1;
			const right = b.percentAchieved ?? -1;
			if (right !== left) return right - left;
			return a.name.localeCompare(b.name);
		})
	);

	let label = $derived(kind === 'province' ? 'Province' : 'District');

	function handleKey(event: KeyboardEvent, name: string) {
		if (event.key === 'Enter' || event.key === ' ') {
			event.preventDefault();
			onSelect(name);
		}
	}
</script>

<Table.Root>
	<Table.Header>
		<Table.Row>
			<Table.Head>{label}</Table.Head>
			<Table.Head class="hidden w-2/5 sm:table-cell">Pass rate</Table.Head>
			<Table.Head class="text-right">Wrote</Table.Head>
			<Table.Head class="hidden text-right md:table-cell">Schools</Table.Head>
			<Table.Head class="text-right sm:hidden">Rate</Table.Head>
			<Table.Head class="w-6"><span class="sr-only">Open</span></Table.Head>
		</Table.Row>
	</Table.Header>
	<Table.Body>
		{#each ordered as row (row.name)}
			{@const isSelected = row.name === selected}
			<Table.Row
				data-region-row={row.name}
				role="button"
				tabindex={0}
				aria-label={`Show ${toTitleCase(row.name)}`}
				aria-current={isSelected ? 'true' : undefined}
				class={cn(
					'cursor-pointer transition-colors hover:bg-muted/50 focus-visible:bg-muted/50 focus-visible:outline-none',
					isSelected && 'bg-primary/5'
				)}
				onclick={() => onSelect(row.name)}
				onkeydown={(event) => handleKey(event, row.name)}
			>
				<Table.Cell class={cn('font-medium', isSelected && 'text-primary')}>
					{toTitleCase(row.name)}
				</Table.Cell>
				<Table.Cell class="hidden sm:table-cell">
					<div class="flex items-center gap-2">
						<div class="h-2 flex-1 overflow-hidden rounded-full bg-muted" aria-hidden="true">
							{#if row.percentAchieved !== null}
								<div
									class={cn(
										'h-full rounded-full motion-safe:transition-[width] motion-safe:duration-500',
										passRateBarClass(row.percentAchieved)
									)}
									style:width={`${Math.min(100, Math.max(0, row.percentAchieved))}%`}
								></div>
							{/if}
						</div>
						<span class="w-12 text-right text-xs font-medium tabular-nums">
							{#if row.percentAchieved === null}
								<span class="text-muted-foreground">-</span>
							{:else}
								<span style:color={passRateColor(row.percentAchieved)}>
									{row.percentAchieved.toFixed(1)}%
								</span>
							{/if}
						</span>
					</div>
				</Table.Cell>
				<Table.Cell class="text-right tabular-nums">{row.totalWrote.toLocaleString()}</Table.Cell>
				<Table.Cell class="hidden text-right tabular-nums md:table-cell">
					{row.schoolCount.toLocaleString()}
				</Table.Cell>
				<Table.Cell class="text-right font-medium tabular-nums sm:hidden">
					{#if row.percentAchieved === null}
						<span class="text-muted-foreground">-</span>
					{:else}
						<span style:color={passRateColor(row.percentAchieved)}>
							{row.percentAchieved.toFixed(1)}%
						</span>
					{/if}
				</Table.Cell>
				<Table.Cell class="text-muted-foreground">
					<ChevronRight class="size-4" aria-hidden="true" />
				</Table.Cell>
			</Table.Row>
		{/each}
	</Table.Body>
</Table.Root>
