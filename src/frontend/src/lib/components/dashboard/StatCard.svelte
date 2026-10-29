<script lang="ts">
	import {
		Card,
		CardDescription,
		CardFooter,
		CardHeader,
		CardTitle
	} from '$lib/components/ui/card';
	import { Badge } from '$lib/components/ui/badge';
	import { ArrowUpRight, ArrowDownRight, Minus } from '@lucide/svelte';
	import type { Component } from 'svelte';
	import type { IconProps } from '@lucide/svelte';

	interface Props {
		title: string;
		value: string;
		/** Year-over-year change. Omit to hide the trend footer entirely. */
		trend?: number | null;
		icon: Component<IconProps>;
		/** Optional CSS colour for the headline value, e.g. a pass-rate band colour. */
		valueColor?: string;
		/** Suffix for the trend badge. Use ' pts' for percentage-point changes. */
		trendUnit?: string;
	}

	let { title, value, trend, icon: Icon, valueColor, trendUnit = '%' }: Props = $props();

	let trendLabel = $derived.by(() => {
		if (trend === null || trend === undefined) return 'N/A';
		return `${trend > 0 ? '+' : ''}${trend}${trendUnit}`;
	});

	let isPositive = $derived(trend !== null && trend !== undefined && trend >= 0);
	let isNeutral = $derived(trend === null);
</script>

<Card class="@container/card">
	<CardHeader>
		<div class="flex items-center justify-between">
			<CardDescription>{title}</CardDescription>
			<Icon class="size-4 text-muted-foreground" />
		</div>
		<CardTitle
			class="text-2xl font-semibold tabular-nums @[250px]/card:text-3xl"
			style={valueColor ? `color: ${valueColor}` : undefined}
		>
			{value}
		</CardTitle>
	</CardHeader>
	{#if trend !== undefined}
		<CardFooter class="flex-col items-start gap-1.5 text-sm">
			<Badge variant="outline" class="gap-1">
				{#if isNeutral}
					<Minus class="size-3" />
				{:else if isPositive}
					<ArrowUpRight class="size-3" />
				{:else}
					<ArrowDownRight class="size-3" />
				{/if}
				{trendLabel}
			</Badge>
			<div class="text-muted-foreground">
				{isNeutral
					? 'No trend data available'
					: isPositive
						? 'Up from last year'
						: 'Down from last year'}
			</div>
		</CardFooter>
	{/if}
</Card>
