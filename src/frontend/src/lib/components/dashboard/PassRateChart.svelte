<script lang="ts">
	import {
		Card,
		CardContent,
		CardDescription,
		CardHeader,
		CardTitle
	} from '$lib/components/ui/card';
	import { Button } from '$lib/components/ui/button';
	import type { PassRateTrend } from '$lib/types/dashboard';
	import SmoothTrendChart from './SmoothTrendChart.svelte';

	let { trends }: { trends: PassRateTrend[] } = $props();

	let timeRange = $state('all');

	let filteredData = $derived.by(() => {
		if (timeRange === 'all') return trends;
		const yearsToShow = timeRange === '5y' ? 5 : 10;
		const currentYear = 2024;
		const startYear = currentYear - yearsToShow + 1;
		return trends.filter((d) => d.year >= startYear);
	});

	let rangeLabel = $derived.by(() => {
		if (timeRange === '5y') return 'Last 5 years (2020 - 2024)';
		if (timeRange === '10y') return 'Last 10 years (2015 - 2024)';
		return '2008 - 2024';
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
		{#if filteredData.length > 0}
			<SmoothTrendChart years={filteredData} height={300} />
		{:else}
			<div class="flex h-[300px] items-center justify-center text-muted-foreground">No data</div>
		{/if}
	</CardContent>
</Card>
