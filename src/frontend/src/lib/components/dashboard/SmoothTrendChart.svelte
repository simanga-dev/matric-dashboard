<script lang="ts">
	import { buildTrendPaths, normalizeTrend, type TrendPoint } from './trend-path';

	let { years, height = 156 }: { years: TrendPoint[]; height?: number } = $props();
	const id = $props.id();
	const gradientId = `${id}-trend-gradient`;
	const padding = { top: 24, right: 18, bottom: 28, left: 18 };
	let width = $state(260);
	let svg: SVGSVGElement | undefined = $state();
	let hoveredYear = $state<number | null>(null);
	let focusedYear = $state<number | null>(null);
	let data = $derived(normalizeTrend(years));
	let svgWidth = $derived(Math.max(width, 200));
	let plotWidth = $derived(svgWidth - padding.left - padding.right);
	let baseline = $derived(height - padding.bottom);
	let plotHeight = $derived(baseline - padding.top);
	let minYear = $derived(data[0]?.year ?? 0);
	let maxYear = $derived(data.at(-1)?.year ?? 0);
	let minRate = $derived(
		data.length ? Math.max(0, Math.min(...data.map((point) => point.passRate)) - 8) : 0
	);
	let maxRate = $derived(
		data.length ? Math.min(100, Math.max(...data.map((point) => point.passRate)) + 8) : 100
	);

	function xScale(year: number) {
		return minYear === maxYear
			? svgWidth / 2
			: padding.left + ((year - minYear) / (maxYear - minYear)) * plotWidth;
	}
	function yScale(rate: number) {
		return baseline - ((rate - minRate) / (maxRate - minRate || 1)) * plotHeight;
	}
	let paths = $derived(
		buildTrendPaths(
			data.map((point) => ({ x: xScale(point.year), y: yScale(point.passRate) })),
			baseline
		)
	);
	let active = $derived(data.find((point) => point.year === (hoveredYear ?? focusedYear)));
	let gridTicks = $derived.by(() => {
		const step = maxRate - minRate <= 30 ? 10 : 20;
		const ticks: number[] = [];
		for (let value = Math.ceil(minRate / step) * step; value <= maxRate; value += step)
			ticks.push(value);
		return ticks;
	});
	let yearTicks = $derived.by(() => {
		const ticks: number[] = [];
		for (const point of data) {
			const previous = ticks.at(-1);
			if (previous === undefined || xScale(point.year) - xScale(previous) >= 56) {
				ticks.push(point.year);
			}
		}
		const last = data.at(-1)?.year;
		if (last !== undefined && ticks.at(-1) !== last) {
			if (ticks.length > 1) ticks.pop();
			ticks.push(last);
		}
		return ticks;
	});

	function nearestYear(clientX: number) {
		if (!svg) return null;
		const bounds = svg.getBoundingClientRect();
		const x = ((clientX - bounds.left) / bounds.width) * svgWidth;
		const closest = data.reduce<TrendPoint | undefined>(
			(nearest, point) =>
				!nearest || Math.abs(xScale(point.year) - x) < Math.abs(xScale(nearest.year) - x)
					? point
					: nearest,
			undefined
		);
		return closest?.year ?? null;
	}

	function hoverPoint(event: PointerEvent) {
		if (!svg || event.pointerType === 'touch') return;
		const bounds = svg.getBoundingClientRect();
		const x = ((event.clientX - bounds.left) / bounds.width) * svgWidth;
		const y = ((event.clientY - bounds.top) / bounds.height) * height;
		if (x < padding.left || x > svgWidth - padding.right || y < 0 || y > baseline) {
			hoveredYear = null;
			return;
		}
		hoveredYear = nearestYear(event.clientX);
	}
</script>

<svelte:window
	onkeydown={(event) => {
		if (event.key === 'Escape') {
			hoveredYear = null;
			focusedYear = null;
		}
	}}
/>

{#if data.length === 0}
	<p class="py-8 text-center text-xs text-muted-foreground">No yearly results available.</p>
{:else}
	<div
		class="relative"
		role="group"
		aria-label="Pass rate trend. Hover, tap, or focus a year to view its percentage."
		data-trend-chart
		onpointermove={hoverPoint}
		onpointerleave={() => (hoveredYear = null)}
	>
		<svg
			bind:this={svg}
			bind:clientWidth={width}
			viewBox="0 0 {svgWidth} {height}"
			class="block w-full"
			preserveAspectRatio="xMidYMid meet"
			role="img"
			aria-label={`NSC pass rate by year: ${data.map((point) => `${point.year} ${point.passRate.toFixed(1)} percent`).join(', ')}`}
		>
			<defs>
				<linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
					<stop offset="0%" stop-color="hsl(var(--chart-1))" stop-opacity="0.2" />
					<stop offset="100%" stop-color="hsl(var(--chart-1))" stop-opacity="0.02" />
				</linearGradient>
			</defs>
			{#each gridTicks as value (value)}
				<line
					x1={padding.left}
					y1={yScale(value)}
					x2={svgWidth - padding.right}
					y2={yScale(value)}
					stroke="hsl(var(--border))"
					stroke-width="1"
				/>
			{/each}
			{#if paths.area}<path d={paths.area} fill={`url(#${gradientId})`} />{/if}
			<path
				data-trend-line
				d={paths.line}
				fill="none"
				stroke="hsl(var(--chart-1))"
				stroke-width="2.5"
				stroke-linecap="round"
				stroke-linejoin="round"
			/>
			{#if active}
				<line
					x1={xScale(active.year)}
					y1={padding.top}
					x2={xScale(active.year)}
					y2={baseline}
					stroke="hsl(var(--muted-foreground) / 0.4)"
					stroke-dasharray="3 4"
				/>
				<circle
					cx={xScale(active.year)}
					cy={yScale(active.passRate)}
					r="4"
					fill="hsl(var(--chart-1))"
					stroke="hsl(var(--card))"
					stroke-width="2"
				/>
			{:else if data.length === 1 && data[0]}
				<circle
					cx={xScale(data[0].year)}
					cy={yScale(data[0].passRate)}
					r="3"
					fill="hsl(var(--chart-1))"
				/>
			{/if}
			{#each yearTicks as year (year)}
				<text
					x={xScale(year)}
					y={height - 8}
					text-anchor="middle"
					class="fill-muted-foreground text-[12px]">{year}</text
				>
			{/each}
		</svg>
		{#each data as point (point.year)}
			<button
				type="button"
				data-trend-point={point.year}
				class="absolute size-[44px] -translate-x-1/2 -translate-y-1/2 rounded-full focus-visible:outline-2 focus-visible:outline-ring"
				style:left={`${(xScale(point.year) / svgWidth) * 100}%`}
				style:top={`${(yScale(point.passRate) / height) * 100}%`}
				aria-label={`${point.year}: ${point.passRate.toFixed(1)}% pass rate`}
				aria-describedby={active?.year === point.year ? `${id}-tooltip` : undefined}
				onfocus={() => (focusedYear = point.year)}
				onblur={() => (focusedYear = null)}
				onclick={(event) => {
					hoveredYear = null;
					// Dense charts can have overlapping touch targets. Select by pointer position.
					focusedYear = event.detail === 0 ? point.year : nearestYear(event.clientX);
				}}
			>
				<span class="sr-only">{point.year}</span>
			</button>
		{/each}
		{#if active}
			<div
				id={`${id}-tooltip`}
				role="tooltip"
				class="trend-tooltip pointer-events-none absolute z-10 min-w-24 rounded-md border bg-popover px-3 py-2 text-center text-popover-foreground shadow-md"
				style:left={`clamp(56px, ${(xScale(active.year) / svgWidth) * 100}%, calc(100% - 56px))`}
				style:top={`${(yScale(active.passRate) / height) * 100}%`}
				style:transform={`translate(-50%, ${yScale(active.passRate) < 60 ? '12px' : 'calc(-100% - 12px)'})`}
			>
				<div class="text-xs text-muted-foreground">{active.year}</div>
				<div class="text-sm font-semibold tabular-nums">{active.passRate.toFixed(1)}%</div>
			</div>
		{/if}
	</div>
{/if}

<style>
	@keyframes tooltip-enter {
		from {
			opacity: 0;
			translate: 0 4px;
		}
		to {
			opacity: 1;
			translate: 0 0;
		}
	}

	.trend-tooltip {
		animation: tooltip-enter 140ms ease-out;
		transition:
			left 120ms ease-out,
			top 120ms ease-out;
	}

	@media (prefers-reduced-motion: reduce) {
		.trend-tooltip {
			animation: none;
			transition: none;
		}
	}
</style>
