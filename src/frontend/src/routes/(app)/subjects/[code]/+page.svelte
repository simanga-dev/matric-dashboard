<script lang="ts">
	import InfoTooltip from '$lib/components/common/InfoTooltip.svelte';
	import { metricHelp } from '$lib/components/common/metric-help';
	import { goto, invalidate } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { navigating, page } from '$app/state';
	import { EmptyState } from '$lib/components/common';
	import * as Card from '$lib/components/ui/card';
	import { Button } from '$lib/components/ui/button';
	import StatCard from '$lib/components/dashboard/StatCard.svelte';
	import SmoothTrendChart from '$lib/components/dashboard/SmoothTrendChart.svelte';
	import SubjectRegionBreakdown from '$lib/components/dashboard/SubjectRegionBreakdown.svelte';
	import SubjectSchoolsTable from '$lib/components/dashboard/SubjectSchoolsTable.svelte';
	import { passRateColor } from '$lib/components/dashboard/school-hierarchy';
	import { setDynamicLabel, clearDynamicLabel } from '$lib/state/breadcrumb.svelte';
	import {
		SUBJECT_DETAIL_DEPENDENCY,
		subjectHref,
		type SubjectScopeParams
	} from '$lib/components/dashboard/subject-url';
	import type { SubjectTrendPoint } from '$lib/types/dashboard';
	import { cn, toTitleCase } from '$lib/utils';
	import {
		BookMarked,
		Building2,
		CheckCircle2,
		MapPin,
		Percent,
		TrendingUp,
		Users
	} from '@lucide/svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	let detail = $derived(data.detail);
	let scope = $derived(data.scope);
	let subjectName = $derived(detail?.name ?? 'Subject');

	// The system header breadcrumb shows the subject's display name, not its URL slug.
	$effect(() => {
		const name = detail?.name;
		if (!name) return;
		setDynamicLabel(name);
		return () => clearDynamicLabel();
	});

	// Scope changes are query navigations that rerun the load for this page.
	let updating = $derived(navigating.to?.url.pathname === page.url.pathname);

	let scopeLabel = $derived(
		detail?.scope === 'country' || !detail ? 'South Africa' : toTitleCase(detail.scopeName)
	);
	let hasResults = $derived(detail !== null && detail.year !== null && detail.totalWrote > 0);

	let current = $derived(detail?.trend.find((point) => point.year === detail?.year) ?? null);
	let previous = $derived.by(() => {
		if (!detail || !current) return null;
		const index = detail.trend.indexOf(current);
		return index > 0 ? (detail.trend[index - 1] ?? null) : null;
	});

	function round1(value: number): number {
		return Math.round(value * 10) / 10;
	}

	function relativeChange(now: number | null | undefined, before: number | null | undefined) {
		if (now === null || now === undefined || !before) return null;
		return round1(((now - before) / before) * 100);
	}

	let passRateDelta = $derived.by(() => {
		const now = current?.percentAchieved ?? null;
		const before = previous?.percentAchieved ?? null;
		return now !== null && before !== null ? round1(now - before) : null;
	});

	let trendPoints = $derived(
		(detail?.trend ?? [])
			.filter(
				(point): point is SubjectTrendPoint & { percentAchieved: number } =>
					point.percentAchieved !== null
			)
			.map((point) => ({
				year: point.year,
				passRate: point.percentAchieved,
				wrote: point.totalWrote
			}))
	);

	let breakdownKind = $derived<'province' | 'district'>(scope.province ? 'district' : 'province');
	let breakdownRows = $derived(
		breakdownKind === 'district' ? (detail?.districts ?? []) : (detail?.provinces ?? [])
	);

	function navigateScope(next: SubjectScopeParams) {
		void goto(resolve(subjectHref(data.code, next)), { noScroll: true, keepFocus: true });
	}

	function drillInto(name: string) {
		if (breakdownKind === 'province')
			navigateScope({ province: name, district: null, year: scope.year });
		else navigateScope({ province: scope.province, district: name, year: scope.year });
	}

	function retry() {
		void invalidate(SUBJECT_DETAIL_DEPENDENCY);
	}

	function formatRate(rate: number | null | undefined): string {
		return rate === null || rate === undefined ? '-' : `${rate.toFixed(1)}%`;
	}
</script>

<svelte:head>
	<title>{subjectName} • Subjects • MatricDashboard</title>
	<meta
		name="description"
		content={detail
			? `${detail.name} NSC results for ${scopeLabel}: pass rate, trend, regional breakdown and schools.`
			: 'NSC subject results by province, district and school.'}
	/>
</svelte:head>

<div class="flex flex-1 flex-col">
	<div class="@container/main flex flex-1 flex-col gap-2">
		<div class="flex flex-col gap-4 py-4 md:gap-6 md:py-6">
			<h1 class="sr-only">{subjectName}</h1>

			{#if data.loadError || !detail}
				<div class="px-4 lg:px-6">
					<Card.Root>
						<Card.Content class="flex h-48 flex-col items-center justify-center gap-3">
							<p role="alert" class="text-sm text-destructive">
								{data.loadError ?? 'Failed to load subject results'}
							</p>
							<Button variant="outline" size="sm" onclick={retry}>Try again</Button>
						</Card.Content>
					</Card.Root>
				</div>
			{:else if !hasResults}
				<div class="px-4 lg:px-6">
					<Card.Root>
						<Card.Content>
							<EmptyState
								icon={BookMarked}
								message={`No ${detail.name} results for ${scopeLabel}${scope.year ? ` in ${scope.year}` : ''}.`}
							/>
						</Card.Content>
					</Card.Root>
				</div>
			{:else}
				<div
					aria-busy={updating}
					class={cn(
						'flex flex-col gap-4 motion-safe:transition-opacity md:gap-6',
						updating && 'opacity-60'
					)}
				>
					<div
						class="grid grid-cols-1 gap-4 px-4 *:data-[slot=card]:bg-gradient-to-t *:data-[slot=card]:from-primary/5 *:data-[slot=card]:to-card *:data-[slot=card]:shadow-xs lg:px-6 @xl/main:grid-cols-2 @5xl/main:grid-cols-4 dark:*:data-[slot=card]:bg-card"
					>
						<StatCard
							title="Learners who wrote"
							value={detail.totalWrote.toLocaleString()}
							trend={relativeChange(current?.totalWrote, previous?.totalWrote)}
							icon={Users}
						/>
						<StatCard
							title="Achieved (30%+)"
							value={detail.achieved?.toLocaleString() ?? '-'}
							trend={relativeChange(current?.achieved, previous?.achieved)}
							icon={CheckCircle2}
						/>
						<StatCard
							title="Pass rate"
							value={formatRate(detail.percentAchieved)}
							valueColor={detail.percentAchieved !== null
								? passRateColor(detail.percentAchieved)
								: undefined}
							trend={passRateDelta}
							trendUnit=" pts"
							icon={Percent}
						/>
						<StatCard
							title="Schools offering"
							value={detail.schoolCount.toLocaleString()}
							trend={relativeChange(current?.schoolCount, previous?.schoolCount)}
							icon={Building2}
						/>
					</div>

					<div class="flex flex-col gap-4 px-4 md:gap-6 lg:px-6">
						<Card.Root>
							<Card.Header>
								<Card.Title class="flex items-center gap-2 text-base">
									<TrendingUp class="size-4 text-primary" />
									Trend
								</Card.Title>
								<Card.Description>
									Pass rate<InfoTooltip
										label="subject pass rate"
										text={metricHelp.subjectPassRate}
									/> and learners who wrote, {scopeLabel}
								</Card.Description>
							</Card.Header>
							<Card.Content class="space-y-3">
								<div class="flex flex-wrap items-center gap-4 text-xs text-muted-foreground">
									<span class="flex items-center gap-1.5">
										<span aria-hidden="true" class="h-0.5 w-4 rounded-full bg-chart-1"></span>
										Pass rate
									</span>
									<span class="flex items-center gap-1.5">
										<span aria-hidden="true" class="size-2.5 rounded-sm bg-primary/30"></span>
										Learners who wrote<InfoTooltip
											label="subject learners who wrote"
											text={metricHelp.subjectWrote}
										/>
									</span>
								</div>
								<SmoothTrendChart years={trendPoints} height={280} />
							</Card.Content>
						</Card.Root>

						<div class="grid grid-cols-1 items-start gap-4 md:gap-6 @6xl/main:grid-cols-2">
							<Card.Root>
								<Card.Header>
									<Card.Title class="flex items-center gap-2 text-base">
										<MapPin class="size-4 text-primary" />
										{breakdownKind === 'district' ? 'By district' : 'By province'}
									</Card.Title>
									<Card.Description>
										{breakdownKind === 'district'
											? `Share by district in ${toTitleCase(scope.province ?? '')}`
											: 'Share by province'}{detail.year ? ` · ${detail.year}` : ''}. Select a
										region to drill in.
									</Card.Description>
								</Card.Header>
								<Card.Content>
									{#if breakdownRows.length === 0}
										<EmptyState icon={MapPin} message="No regional results for this year." />
									{:else}
										<SubjectRegionBreakdown
											rows={breakdownRows}
											kind={breakdownKind}
											selected={breakdownKind === 'district' ? scope.district : scope.province}
											onSelect={drillInto}
										/>
									{/if}
								</Card.Content>
							</Card.Root>

							<SubjectSchoolsTable
								code={data.code}
								{subjectName}
								province={scope.province}
								district={scope.district}
								year={detail.year}
							/>
						</div>
					</div>
				</div>
			{/if}
		</div>
	</div>
</div>
