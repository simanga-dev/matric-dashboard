<script lang="ts">
	import type { SchoolYear } from '$lib/types/dashboard';

	let { years }: { years: SchoolYear[] } = $props();

	const HEIGHT = 156;
	const PADDING = { top: 22, right: 18, bottom: 24, left: 30 };

	let width = $state(260);
	let svgWidth = $derived(Math.max(width, 200));
	let plotWidth = $derived(svgWidth - PADDING.left - PADDING.right);
	let plotHeight = $derived(HEIGHT - PADDING.top - PADDING.bottom);

	let sorted = $derived([...years].sort((a, b) => a.year - b.year));
	let minYear = $derived(sorted[0]?.year ?? 0);
	let maxYear = $derived(sorted.at(-1)?.year ?? 0);
	let rates = $derived(sorted.map((y) => y.passRate));
	let minRate = $derived(rates.length ? Math.max(0, Math.min(...rates) - 8) : 0);
	let maxRate = $derived(rates.length ? Math.min(100, Math.max(...rates) + 8) : 100);

	function xScale(year: number): number {
		return PADDING.left + ((year - minYear) / (maxYear - minYear || 1)) * plotWidth;
	}

	function yScale(rate: number): number {
		return PADDING.top + plotHeight - ((rate - minRate) / (maxRate - minRate || 1)) * plotHeight;
	}

	let points = $derived(sorted.map((y) => `${xScale(y.year)},${yScale(y.passRate)}`).join(' '));
	let areaPoints = $derived(
		sorted.length > 1
			? `${xScale(minYear)},${PADDING.top + plotHeight} ${points} ${xScale(maxYear)},${PADDING.top + plotHeight}`
			: ''
	);

	let yTicks = $derived.by(() => {
		const step = maxRate - minRate <= 30 ? 10 : 20;
		const ticks: number[] = [];
		for (let v = Math.ceil(minRate / step) * step; v <= maxRate; v += step) ticks.push(v);
		return ticks;
	});

	let yearTicks = $derived.by(() => {
		if (sorted.length <= 1) return sorted.map((y) => y.year);
		const step = Math.max(1, Math.round((maxYear - minYear) / 4));
		const ticks: number[] = [];
		for (let y = minYear; y <= maxYear; y += step) ticks.push(y);
		if (ticks.at(-1) !== maxYear) ticks.push(maxYear);
		return ticks;
	});
</script>

{#if sorted.length === 0}
	<p class="py-8 text-center text-xs text-muted-foreground">No yearly results available.</p>
{:else}
	<svg
		bind:clientWidth={width}
		viewBox="0 0 {svgWidth} {HEIGHT}"
		class="w-full"
		preserveAspectRatio="xMidYMid meet"
		role="img"
		aria-label={`NSC pass rate by year: ${sorted.map((y) => `${y.year} ${y.passRate.toFixed(1)} percent`).join(', ')}`}
	>
		<defs>
			<linearGradient id="schoolTrendGradient" x1="0" y1="0" x2="0" y2="1">
				<stop offset="0%" stop-color="hsl(var(--primary))" stop-opacity="0.22" />
				<stop offset="100%" stop-color="hsl(var(--primary))" stop-opacity="0.02" />
			</linearGradient>
		</defs>

		{#each yTicks as tick (tick)}
			<line
				x1={PADDING.left}
				y1={yScale(tick)}
				x2={svgWidth - PADDING.right}
				y2={yScale(tick)}
				stroke="hsl(var(--border))"
				stroke-width="1"
			/>
			<text
				x={PADDING.left - 6}
				y={yScale(tick) + 3.5}
				text-anchor="end"
				class="fill-muted-foreground text-[10px]"
			>
				{tick}
			</text>
		{/each}

		{#if areaPoints}<polygon points={areaPoints} fill="url(#schoolTrendGradient)" />{/if}
		{#if sorted.length > 1}
			<polyline {points} fill="none" stroke="hsl(var(--primary))" stroke-width="2" />
		{/if}

		{#each sorted as y (y.year)}
			<circle
				cx={xScale(y.year)}
				cy={yScale(y.passRate)}
				r={y.year === maxYear ? 4 : 3}
				fill="hsl(var(--primary))"
			/>
			<text
				x={xScale(y.year)}
				y={yScale(y.passRate) - 9}
				text-anchor="middle"
				class="fill-foreground text-[10px] font-medium tabular-nums"
			>
				{y.passRate.toFixed(1)}%
			</text>
		{/each}

		{#each yearTicks as tick (tick)}
			<text
				x={xScale(tick)}
				y={HEIGHT - 8}
				text-anchor="middle"
				class="fill-muted-foreground text-[10px]"
			>
				{tick}
			</text>
		{/each}
	</svg>
{/if}
