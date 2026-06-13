<script lang="ts">
	import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '$lib/components/ui/card';
	import { Button } from '$lib/components/ui/button';
	import { onMount } from 'svelte';
	import type { PassRateTrend } from '$lib/types/dashboard';
	import { fetchPassRateTrends } from '$lib/api/dashboard';

	let data: PassRateTrend[] = $state([]);
	let loading = $state(true);
	let timeRange = $state('all');
	let error = $state<string | null>(null);

	let filteredData = $derived.by(() => {
		if (timeRange === 'all') return data;
		const yearsToShow = timeRange === '5y' ? 5 : 10;
		const currentYear = 2024;
		const startYear = currentYear - yearsToShow + 1;
		return data.filter((d) => d.year >= startYear);
	});

	let rangeLabel = $derived.by(() => {
		if (timeRange === '5y') return 'Last 5 years (2020 - 2024)';
		if (timeRange === '10y') return 'Last 10 years (2015 - 2024)';
		return '2008 - 2024';
	});

	onMount(() => {
		loadData();
	});

	async function loadData() {
		loading = true;
		error = null;
		try {
			data = await fetchPassRateTrends();
		} catch (e) {
			error = e instanceof Error ? e.message : 'Failed to load chart data';
		} finally {
			loading = false;
		}
	}

	const CHART_HEIGHT = 300;
	const CHART_PADDING = { top: 20, right: 20, bottom: 40, left: 60 };

	let chartWidth = $state(600);

	let svgWidth = $derived(Math.max(chartWidth, 300));
	let plotWidth = $derived(svgWidth - CHART_PADDING.left - CHART_PADDING.right);
	let plotHeight = $derived(CHART_HEIGHT - CHART_PADDING.top - CHART_PADDING.bottom);

	let minYear = $derived(filteredData.length > 0 ? filteredData[0].year : 2008);
	let maxYear = $derived(filteredData.length > 0 ? filteredData[filteredData.length - 1].year : 2024);
	let minPassRate = $derived(
		filteredData.length > 0 ? Math.min(...filteredData.map((d) => d.passRate)) - 5 : 50
	);
	let maxPassRate = $derived(
		filteredData.length > 0 ? Math.max(...filteredData.map((d) => d.passRate)) + 5 : 100
	);

	function xScale(year: number): number {
		return CHART_PADDING.left + ((year - minYear) / (maxYear - minYear || 1)) * plotWidth;
	}

	function yScale(passRate: number): number {
		return CHART_PADDING.top + plotHeight - ((passRate - minPassRate) / (maxPassRate - minPassRate || 1)) * plotHeight;
	}

	let points = $derived(filteredData.map((d) => `${xScale(d.year)},${yScale(d.passRate)}`).join(' '));
	let areaPoints = $derived(
		`${xScale(minYear)},${yScale(minPassRate)} ${points} ${xScale(maxYear)},${yScale(minPassRate)}`
	);

	let yTicks = $derived.by(() => {
		const step = Math.max(5, Math.round((maxPassRate - minPassRate) / 5 / 5) * 5);
		const ticks: number[] = [];
		for (let v = Math.ceil(minPassRate / step) * step; v <= maxPassRate; v += step) {
			ticks.push(v);
		}
		return ticks;
	});

	let xTicks = $derived.by(() => {
		const step = Math.max(1, Math.floor((maxYear - minYear) / 6));
		const ticks: number[] = [];
		for (let y = minYear; y <= maxYear; y += step) {
			ticks.push(y);
		}
		return ticks;
	});
</script>

<Card class="@container/card">
	<CardHeader>
		<div class="flex items-start justify-between gap-4">
			<div>
				<CardTitle>NSC Pass Rate Trends</CardTitle>
				<CardDescription>
					<span class="hidden @[540px]/card:block">{rangeLabel}</span>
					<span class="@[540px]/card:hidden">Pass rate over time</span>
				</CardDescription>
			</div>
			<div class="flex gap-1">
				<Button
					variant={timeRange === '5y' ? 'default' : 'outline'}
					size="sm"
					onclick={() => (timeRange = '5y')}
				>
					5Y
				</Button>
				<Button
					variant={timeRange === '10y' ? 'default' : 'outline'}
					size="sm"
					onclick={() => (timeRange = '10y')}
				>
					10Y
				</Button>
				<Button
					variant={timeRange === 'all' ? 'default' : 'outline'}
					size="sm"
					onclick={() => (timeRange = 'all')}
				>
					ALL
				</Button>
			</div>
		</div>
	</CardHeader>
	<CardContent>
		{#if loading}
			<div class="flex h-[300px] items-center justify-center text-muted-foreground">Loading...</div>
		{:else if error}
			<div class="flex h-[300px] items-center justify-center text-destructive">{error}</div>
		{:else if filteredData.length > 0}
			<svg
				bind:clientWidth={chartWidth}
				viewBox="0 0 {svgWidth} {CHART_HEIGHT}"
				class="w-full"
				preserveAspectRatio="xMidYMid meet"
			>
				<defs>
					<linearGradient id="areaGradient" x1="0" y1="0" x2="0" y2="1">
						<stop offset="0%" stop-color="hsl(var(--primary))" stop-opacity="0.2" />
						<stop offset="100%" stop-color="hsl(var(--primary))" stop-opacity="0.02" />
					</linearGradient>
				</defs>

				<!-- Y axis grid lines -->
				{#each yTicks as tick}
					<line
						x1={CHART_PADDING.left}
						y1={yScale(tick)}
						x2={svgWidth - CHART_PADDING.right}
						y2={yScale(tick)}
						stroke="hsl(var(--border))"
						stroke-width="1"
					/>
					<text
						x={CHART_PADDING.left - 8}
						y={yScale(tick) + 4}
						text-anchor="end"
						class="fill-muted-foreground text-[11px]"
					>
						{tick}%
					</text>
				{/each}

				<!-- X axis labels -->
				{#each xTicks as tick}
					<text
						x={xScale(tick)}
						y={CHART_HEIGHT - 8}
						text-anchor="middle"
						class="fill-muted-foreground text-[11px]"
					>
						{tick}
					</text>
				{/each}

				<!-- Area fill -->
				<polygon points={areaPoints} fill="url(#areaGradient)" />

				<!-- Line -->
				<polyline points={points} fill="none" stroke="hsl(var(--primary))" stroke-width="2" />

				<!-- Data dots -->
				{#each filteredData as d}
					<circle cx={xScale(d.year)} cy={yScale(d.passRate)} r="3" fill="hsl(var(--primary))" />
				{/each}
			</svg>
		{:else}
			<div class="flex h-[300px] items-center justify-center text-muted-foreground">No data</div>
		{/if}
	</CardContent>
</Card>
