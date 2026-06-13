<script lang="ts">
	import { Card, CardContent, CardHeader, CardTitle } from '$lib/components/ui/card';
	import { Badge } from '$lib/components/ui/badge';
	import { Skeleton } from '$lib/components/ui/skeleton';
	import { fetchSchools } from '$lib/api/dashboard';
	import { onMount } from 'svelte';
	import { School as SchoolIcon, Users, GraduationCap, Award, TrendingUp, MapPin } from '@lucide/svelte';
	import type { School } from '$lib/types/dashboard';

	let { selectedSchoolId = $bindable(null) }: { selectedSchoolId?: number | null } = $props();

	let schools: School[] = $state([]);
	let loading = $state(true);

	onMount(async () => {
		try {
			const data = await fetchSchools(1, 1000);
			schools = data.items;
		} catch {
			// silently fail
		} finally {
			loading = false;
		}
	});

	let displaySchools = $derived(
		selectedSchoolId
			? schools.filter((s) => s.id === selectedSchoolId)
			: schools.slice(0, 10)
	);

	function formatPassRate(rate: number): string {
		return `${rate.toFixed(1)}%`;
	}

	function passRateColor(rate: number): string {
		if (rate >= 80) return 'text-green-600';
		if (rate >= 60) return 'text-amber-600';
		return 'text-red-600';
	}
</script>

<Card class="h-full">
	<CardHeader class="pb-3">
		<CardTitle class="flex items-center gap-2 text-base">
			<SchoolIcon class="size-4" />
			{selectedSchoolId ? 'School Details' : 'Top Schools'}
		</CardTitle>
	</CardHeader>
	<CardContent class="overflow-y-auto">
		{#if loading}
			<div class="space-y-3">
				<Skeleton class="h-28 w-full" />
				<Skeleton class="h-28 w-full" />
				<Skeleton class="h-28 w-full" />
			</div>
		{:else}
			<div class="space-y-2">
				{#each displaySchools as school}
					<div
						class="rounded-lg border p-3 transition-colors hover:border-primary/50"
					>
						<div class="flex items-start justify-between gap-2">
							<span class="text-sm font-medium leading-tight">{school.name}</span>
							<Badge
								variant="secondary"
								class="shrink-0 {passRateColor(school.passRate)}"
							>
								{formatPassRate(school.passRate)}
							</Badge>
						</div>
						<div class="mt-2 flex flex-wrap gap-x-3 gap-y-1 text-xs text-muted-foreground">
							<span class="flex items-center gap-1">
								<MapPin class="size-3" />
								{school.province}
							</span>
							<span class="flex items-center gap-1">
								<Users class="size-3" />
								{school.totalWrote.toLocaleString()} wrote
							</span>
							<span class="flex items-center gap-1">
								<GraduationCap class="size-3" />
								{school.totalPassed.toLocaleString()} passed
							</span>
							{#if school.totalAchieved !== null}
								<span class="flex items-center gap-1">
									<Award class="size-3" />
									{school.totalAchieved.toLocaleString()} bachelor
								</span>
							{/if}
						</div>
						{#if !selectedSchoolId}
							<div class="mt-2 flex items-center gap-2">
								<div class="h-1.5 flex-1 rounded-full bg-muted">
									<div
										class="h-full rounded-full {school.passRate >= 80 ? 'bg-green-500' : school.passRate >= 60 ? 'bg-amber-500' : 'bg-red-500'}"
										style="width: {school.passRate}%"
									></div>
								</div>
								<span class="text-[10px] tabular-nums text-muted-foreground">
									<TrendingUp class="inline size-3 align-text-bottom" />
									{formatPassRate(school.passRate)}
								</span>
							</div>
						{/if}
					</div>
				{/each}
			</div>
		{/if}
	</CardContent>
</Card>
