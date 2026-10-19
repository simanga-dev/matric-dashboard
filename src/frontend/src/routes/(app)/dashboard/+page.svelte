<script lang="ts">
	import { StatCard, PassRateChart, SchoolsTable } from '$lib/components/dashboard';
	import * as m from '$lib/paraglide/messages';
	import { GraduationCap, Building2, Users, Percent } from '@lucide/svelte';
	import { page } from '$app/state';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	let externalSearch = $derived(page.url.searchParams.get('search') ?? undefined);
</script>

<svelte:head>
	<title>{m.meta_titleTemplate({ title: m.meta_dashboard_title() })}</title>
	<meta name="description" content={m.meta_dashboard_description()} />
</svelte:head>

<div class="flex flex-1 flex-col">
	<div class="@container/main flex flex-1 flex-col gap-2">
		<div class="flex flex-col gap-4 py-4 md:gap-6 md:py-6">
			<div
				class="grid grid-cols-1 gap-4 px-4 *:data-[slot=card]:bg-gradient-to-t *:data-[slot=card]:from-primary/5 *:data-[slot=card]:to-card *:data-[slot=card]:shadow-xs lg:px-6 @xl/main:grid-cols-2 @5xl/main:grid-cols-4 dark:*:data-[slot=card]:bg-card"
			>
				<StatCard
					title="Top School Performers"
					value={data.stats.topSchools.total.toLocaleString()}
					trend={data.stats.topSchools.trend}
					icon={GraduationCap}
				/>
				<StatCard
					title="Exam Centers"
					value={data.stats.examCenters.total.toLocaleString()}
					trend={data.stats.examCenters.trend}
					icon={Building2}
				/>
				<StatCard
					title="Learners in {data.stats.year}"
					value={data.stats.totalLearners.total.toLocaleString()}
					trend={data.stats.totalLearners.trend}
					icon={Users}
				/>
				<StatCard
					title="Pass Rate {data.stats.year}"
					value={`${(data.stats.passRate.total / 10).toFixed(1)}%`}
					trend={data.stats.passRate.trend}
					icon={Percent}
				/>
			</div>

			<div class="px-4 lg:px-6">
				<PassRateChart trends={data.trends} />
			</div>

			<div class="px-4 lg:px-6">
				<SchoolsTable {externalSearch} />
			</div>
		</div>
	</div>
</div>
