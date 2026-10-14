<script lang="ts">
	import SmoothTrendChart from './SmoothTrendChart.svelte';
	import type { TrendPoint } from './trend-path';

	let { years }: { years: TrendPoint[] } = $props();
</script>

<SmoothTrendChart {years} />
