<script lang="ts">
	import { StatCard, PassRateChart, SchoolsTable } from '$lib/components/dashboard';
	import * as m from '$lib/paraglide/messages';
	import { onMount } from 'svelte';
	import { fetchDashboardStats } from '$lib/api/dashboard';
	import type { DashboardStats } from '$lib/types/dashboard';
	import { GraduationCap, Building2, Users, Percent } from '@lucide/svelte';
	import type { PageData } from './$types';
	import { page } from '$app/state';

	let { data }: { data: PageData } = $props();

	let stats: DashboardStats | null = $state(null);
	let statsLoading = $state(true);
	let statsError = $state<string | null>(null);

	let externalSearch = $derived(page.url.searchParams.get('search') ?? undefined);

	onMount(() => {
		loadStats();
	});

	async function loadStats() {
		statsLoading = true;
		statsError = null;
		try {
			stats = await fetchDashboardStats();
		} catch (e) {
			statsError = e instanceof Error ? e.message : 'Failed to load stats';
		} finally {
			statsLoading = false;
		}
	}
</script>

<svelte:head>
	<title>{m.meta_titleTemplate({ title: m.meta_dashboard_title() })}</title>
	<meta name="description" content={m.meta_dashboard_description()} />
</svelte:head>

<div class="flex flex-1 flex-col">
	<div class="@container/main flex flex-1 flex-col gap-2">
		<div class="flex flex-col gap-4 py-4 md:gap-6 md:py-6">
			{#if statsLoading}
				<div class="grid grid-cols-1 gap-4 px-4 lg:px-6 @xl/main:grid-cols-2 @5xl/main:grid-cols-4">
					{#each [1, 2, 3, 4] as _}
						<div class="animate-pulse rounded-xl border bg-card p-6">
							<div class="mb-3 h-4 w-24 rounded bg-muted"></div>
							<div class="mb-4 h-8 w-20 rounded bg-muted"></div>
							<div class="h-3 w-32 rounded bg-muted"></div>
						</div>
					{/each}
				</div>
			{:else if statsError}
				<div class="px-4 lg:px-6">
					<div class="rounded-xl border bg-card p-6 text-destructive">{statsError}</div>
				</div>
			{:else if stats}
				<div
					class="grid grid-cols-1 gap-4 px-4 *:data-[slot=card]:bg-gradient-to-t *:data-[slot=card]:from-primary/5 *:data-[slot=card]:to-card *:data-[slot=card]:shadow-xs lg:px-6 @xl/main:grid-cols-2 @5xl/main:grid-cols-4 dark:*:data-[slot=card]:bg-card"
				>
					<StatCard
						title="Top School Performers"
						value={stats.topSchools.total.toLocaleString()}
						trend={stats.topSchools.trend}
						icon={GraduationCap}
					/>
					<StatCard
						title="Exam Centers"
						value={stats.examCenters.total.toLocaleString()}
						trend={stats.examCenters.trend}
						icon={Building2}
					/>
					<StatCard
						title="Learners in 2024"
						value={stats.totalLearners.total.toLocaleString()}
						trend={stats.totalLearners.trend}
						icon={Users}
					/>
					<StatCard
						title="Pass Rate 2024"
						value={`${(stats.passRate.total / 10).toFixed(1)}%`}
						trend={stats.passRate.trend}
						icon={Percent}
					/>
				</div>
			{/if}

			<div class="px-4 lg:px-6">
				<PassRateChart />
			</div>

			<div class="px-4 lg:px-6">
				<SchoolsTable externalSearch={externalSearch} />
			</div>
		</div>
	</div>
</div>
