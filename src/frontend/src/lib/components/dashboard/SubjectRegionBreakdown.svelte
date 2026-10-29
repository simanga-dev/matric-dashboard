<script lang="ts">
	import type { SubjectRegionBreakdown } from '$lib/types/dashboard';
	import { passRateColor } from './school-hierarchy';
	import { cn, toTitleCase } from '$lib/utils';

	/**
	 * Per-province or per-district results for one subject, shown as a donut of
	 * learners who wrote plus a drill-in legend. Slice size is the region's share
	 * of learners who wrote, and the legend carries each region's pass rate.
	 */
	interface Props {
		rows: SubjectRegionBreakdown[];
		/** Region level shown in the legend label. */
		kind: 'province' | 'district';
		/** Currently selected region, highlighted in the legend. */
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
	let totalWrote = $derived(ordered.reduce((sum, row) => sum + Math.max(0, row.totalWrote), 0));

	const radius = 70;
	const circumference = 2 * Math.PI * radius;
	const center = 100;

	let slices = $derived.by(() => {
		let offset = 0;
		return ordered.map((row, index) => {
			const share = totalWrote > 0 ? Math.max(0, row.totalWrote) / totalWrote : 0;
			const length = share * circumference;
			const slice = {
				row,
				share,
				length,
				offset,
				alpha: 1 - index * (0.72 / Math.max(1, ordered.length - 1))
			};
			offset += length;
			return slice;
		});
	});

	let hovered = $state<string | null>(null);
	let hoveredSlice = $derived(slices.find((slice) => slice.row.name === hovered) ?? null);

	function slicePaint(alpha: number) {
		return `hsl(var(--primary) / ${alpha.toFixed(2)})`;
	}

	function formatShare(share: number) {
		const percent = share * 100;
		return percent > 0 && percent < 1 ? '<1%' : `${percent.toFixed(1)}%`;
	}

	function formatRate(rate: number | null) {
		return rate === null ? '-' : `${rate.toFixed(1)}%`;
	}

	function handleKey(event: KeyboardEvent, name: string) {
		if (event.key === 'Enter' || event.key === ' ') {
			event.preventDefault();
			onSelect(name);
		}
	}
</script>

<figure class="space-y-5" aria-label={`${label} share of learners who wrote`}>
	{#if totalWrote > 0}
		<div class="relative mx-auto w-56 max-w-full">
			<svg
				viewBox="0 0 200 200"
				class="block w-full"
				role="img"
				aria-label={`Share of learners who wrote by ${kind}: ${slices
					.map((slice) => `${toTitleCase(slice.row.name)} ${formatShare(slice.share)}`)
					.join(', ')}`}
			>
				<circle
					cx={center}
					cy={center}
					r={radius}
					fill="none"
					stroke="hsl(var(--muted))"
					stroke-width="28"
				/>
				{#each slices as slice (slice.row.name)}
					{#if slice.length > 0.01}
						<circle
							data-region-slice={slice.row.name}
							cx={center}
							cy={center}
							r={radius}
							fill="none"
							stroke={slicePaint(slice.alpha)}
							stroke-width="28"
							stroke-dasharray={`${Math.max(slice.length - 2, 0.2)} ${circumference}`}
							stroke-dashoffset={-slice.offset}
							transform={`rotate(-90 ${center} ${center})`}
							class="motion-safe:transition-opacity"
							opacity={hovered && hovered !== slice.row.name ? 0.45 : 1}
						>
							<title>
								{toTitleCase(slice.row.name)}: {formatShare(slice.share)} of learners wrote ·
								{formatRate(slice.row.percentAchieved)} pass rate
							</title>
						</circle>
					{/if}
				{/each}
				<text
					x={center}
					y={hoveredSlice ? 90 : 94}
					text-anchor="middle"
					class="fill-muted-foreground text-[9px]"
				>
					{hoveredSlice ? toTitleCase(hoveredSlice.row.name) : 'Learners who wrote'}
				</text>
				<text
					x={center}
					y={hoveredSlice ? 110 : 114}
					text-anchor="middle"
					class="fill-foreground text-lg font-semibold tabular-nums"
				>
					{hoveredSlice ? formatShare(hoveredSlice.share) : totalWrote.toLocaleString()}
				</text>
				{#if hoveredSlice}
					<text x={center} y={126} text-anchor="middle" class="fill-muted-foreground text-[9px]">
						{formatRate(hoveredSlice.row.percentAchieved)} pass rate
					</text>
				{:else}
					<text x={center} y={130} text-anchor="middle" class="fill-muted-foreground text-[9px]">
						{ordered.length}
						{ordered.length === 1 ? kind : `${kind}s`}
					</text>
				{/if}
			</svg>
		</div>
	{:else}
		<p class="py-8 text-center text-xs text-muted-foreground">No learner counts for this year.</p>
	{/if}

	<ul class="space-y-0.5" aria-label={`${label} breakdown`}>
		{#each slices as slice (slice.row.name)}
			{@const isSelected = slice.row.name === selected}
			<li>
				<button
					type="button"
					data-region-row={slice.row.name}
					aria-current={isSelected ? 'true' : undefined}
					aria-label={`Show ${toTitleCase(slice.row.name)}`}
					class={cn(
						'flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-left transition-colors hover:bg-primary/10 focus-visible:bg-primary/10 focus-visible:outline-none',
						isSelected && 'bg-primary/15'
					)}
					onmouseenter={() => (hovered = slice.row.name)}
					onmouseleave={() => (hovered = null)}
					onclick={() => onSelect(slice.row.name)}
					onkeydown={(event) => handleKey(event, slice.row.name)}
				>
					<span
						aria-hidden="true"
						class="size-2.5 shrink-0 rounded-sm"
						style:background={slicePaint(slice.alpha)}
					></span>
					<span
						class={cn(
							'min-w-0 flex-1 truncate text-sm',
							isSelected ? 'font-medium text-primary' : 'text-foreground'
						)}
					>
						{toTitleCase(slice.row.name)}
					</span>
					<span
						class="w-12 text-right text-xs font-medium tabular-nums"
						style:color={slice.row.percentAchieved === null
							? undefined
							: passRateColor(slice.row.percentAchieved)}
					>
						{formatRate(slice.row.percentAchieved)}
					</span>
					<span class="w-10 text-right text-xs text-muted-foreground tabular-nums">
						{formatShare(slice.share)}
					</span>
				</button>
			</li>
		{/each}
	</ul>
</figure>
