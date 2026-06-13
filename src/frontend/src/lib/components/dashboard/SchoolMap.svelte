<script lang="ts">
	import { Card, CardContent, CardHeader, CardTitle } from '$lib/components/ui/card';
	import { Skeleton } from '$lib/components/ui/skeleton';
	import { Badge } from '$lib/components/ui/badge';
	import { fetchSchools } from '$lib/api/dashboard';
	import { onMount } from 'svelte';
	import { Map } from '@lucide/svelte';

	interface ProvinceData {
		name: string;
		schools: number;
		avgPassRate: number;
		totalWrote: number;
		totalPassed: number;
	}

	let loading = $state(true);
	let provinces: ProvinceData[] = $state([]);
	const mapSrc = 'https://commons.wikimedia.org/wiki/Special:FilePath/SA_provinces.svg';

	onMount(async () => {
		try {
			const data = await fetchSchools(1, 1000);
			const grouped: Record<string, { schools: number; wrote: number; passed: number }> = {};
			for (const s of data.items) {
				const existing = grouped[s.province] ?? { schools: 0, wrote: 0, passed: 0 };
				existing.schools++;
				existing.wrote += s.totalWrote;
				existing.passed += s.totalPassed;
				grouped[s.province] = existing;
			}
			provinces = Object.entries(grouped).map(([name, d]) => ({
				name,
				schools: d.schools,
				totalWrote: d.wrote,
				totalPassed: d.passed,
				avgPassRate: d.wrote > 0 ? Math.round((d.passed / d.wrote) * 1000) / 10 : 0
			}));
		} catch {
			// silently fail
		} finally {
			loading = false;
		}
	});

	let totalSchools = $derived(provinces.reduce((sum, province) => sum + province.schools, 0));
	let totalWrote = $derived(provinces.reduce((sum, province) => sum + province.totalWrote, 0));
	let totalPassed = $derived(provinces.reduce((sum, province) => sum + province.totalPassed, 0));
	let overallPassRate = $derived(totalWrote > 0 ? Math.round((totalPassed / totalWrote) * 1000) / 10 : 0);
</script>

<Card class="h-full">
	<CardHeader class="pb-3">
		<CardTitle class="flex items-center gap-2 text-base">
			<Map class="size-4" />
			Schools Map
		</CardTitle>
	</CardHeader>
	<CardContent class="flex flex-col gap-4 p-4">
		{#if loading}
			<div class="flex items-center justify-center py-12">
				<Skeleton class="h-80 w-full" />
			</div>
		{:else}
			<div class="overflow-hidden rounded-xl border bg-background shadow-sm">
				<img src={mapSrc} alt="South Africa provinces map" class="h-auto w-full" />
			</div>

			<div class="grid gap-2 sm:grid-cols-4">
				<div class="rounded-lg border bg-card p-3">
					<div class="text-xs text-muted-foreground">Schools</div>
					<div class="text-lg font-semibold tabular-nums">{totalSchools.toLocaleString()}</div>
				</div>
				<div class="rounded-lg border bg-card p-3">
					<div class="text-xs text-muted-foreground">Wrote</div>
					<div class="text-lg font-semibold tabular-nums">{totalWrote.toLocaleString()}</div>
				</div>
				<div class="rounded-lg border bg-card p-3">
					<div class="text-xs text-muted-foreground">Passed</div>
					<div class="text-lg font-semibold tabular-nums">{totalPassed.toLocaleString()}</div>
				</div>
				<div class="rounded-lg border bg-card p-3">
					<div class="text-xs text-muted-foreground">Pass rate</div>
					<div class="text-lg font-semibold tabular-nums">{overallPassRate}%</div>
				</div>
			</div>

			<div class="flex flex-wrap gap-2">
				{#each provinces as province}
					<Badge variant="secondary" class="gap-1">
						{province.name}
						<span class="text-muted-foreground">{province.schools}</span>
					</Badge>
				{/each}
			</div>
		{/if}
	</CardContent>
</Card>
