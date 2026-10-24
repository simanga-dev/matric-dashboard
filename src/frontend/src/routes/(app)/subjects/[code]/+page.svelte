<script lang="ts">
	import InfoTooltip from '$lib/components/common/InfoTooltip.svelte';
	import { metricHelp } from '$lib/components/common/metric-help';
	import { goto, invalidate } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { navigating, page } from '$app/state';
	import { EmptyState } from '$lib/components/common';
	import * as Breadcrumb from '$lib/components/ui/breadcrumb';
	import * as Card from '$lib/components/ui/card';
	import * as Select from '$lib/components/ui/select';
	import { Badge } from '$lib/components/ui/badge';
	import { Button } from '$lib/components/ui/button';
	import StatCard from '$lib/components/dashboard/StatCard.svelte';
	import SmoothTrendChart from '$lib/components/dashboard/SmoothTrendChart.svelte';
	import SubjectRegionBreakdown from '$lib/components/dashboard/SubjectRegionBreakdown.svelte';
	import SubjectSchoolsTable from '$lib/components/dashboard/SubjectSchoolsTable.svelte';
	import YearPills from '$lib/components/dashboard/YearPills.svelte';
	import { passRateColor } from '$lib/components/dashboard/school-hierarchy';
	import {
		SUBJECT_DETAIL_DEPENDENCY,
		subjectHref,
		subjectsListHref,
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
		Users,
		X
	} from '@lucide/svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	const ALL = '__all__';

	let detail = $derived(data.detail);
	let scope = $derived(data.scope);
	let subjectName = $derived(detail?.name ?? 'Subject');

	// Scope changes are query navigations that rerun the load for this page.
	let updating = $derived(navigating.to?.url.pathname === page.url.pathname);

	let provinceOptions = $derived(
		[...(detail?.provinces ?? [])].map((row) => row.name).sort((a, b) => a.localeCompare(b))
	);
	let districtOptions = $derived(
		[...(detail?.districts ?? [])].map((row) => row.name).sort((a, b) => a.localeCompare(b))
	);

	let scopeKind = $derived(
		detail?.scope === 'district'
			? 'District'
			: detail?.scope === 'province'
				? 'Province'
				: 'National'
	);
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
			.map((point) => ({ year: point.year, passRate: point.percentAchieved }))
	);
	let maxTrendWrote = $derived(Math.max(1, ...(detail?.trend ?? []).map((p) => p.totalWrote)));

	let breakdownKind = $derived<'province' | 'district'>(scope.province ? 'district' : 'province');
	let breakdownRows = $derived(
		breakdownKind === 'district' ? (detail?.districts ?? []) : (detail?.provinces ?? [])
	);

	function navigateScope(next: SubjectScopeParams) {
		void goto(resolve(subjectHref(data.code, next)), { noScroll: true, keepFocus: true });
	}

	function selectProvince(value: string | undefined) {
		const province = !value || value === ALL ? null : value;
		navigateScope({ province, district: null, year: scope.year });
	}

	function selectDistrict(value: string | undefined) {
		const district = !value || value === ALL ? null : value;
		navigateScope({ province: scope.province, district, year: scope.year });
	}

	function selectYear(year: number) {
		navigateScope({ province: scope.province, district: scope.district, year });
	}

	function drillInto(name: string) {
		if (breakdownKind === 'province')
			navigateScope({ province: name, district: null, year: scope.year });
		else navigateScope({ province: scope.province, district: name, year: scope.year });
	}

	function clearScope() {
		navigateScope({ province: null, district: null, year: scope.year });
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

<div class="mx-auto w-full max-w-7xl space-y-6">
	<Breadcrumb.Root>
		<Breadcrumb.List>
			<Breadcrumb.Item>
				<Breadcrumb.Link href={resolve(subjectsListHref(scope))}>Subjects</Breadcrumb.Link>
			</Breadcrumb.Item>
			<Breadcrumb.Separator />
			<Breadcrumb.Item>
				<Breadcrumb.Page>{subjectName}</Breadcrumb.Page>
			</Breadcrumb.Item>
		</Breadcrumb.List>
	</Breadcrumb.Root>

	<div class="flex flex-col gap-2">
		<div class="flex flex-wrap items-center gap-3">
			<h1 class="text-2xl font-semibold tracking-tight">{subjectName}</h1>
			{#if detail?.isGateway}
				<Badge variant="secondary">Gateway subject</Badge>
			{/if}
		</div>
		<p class="max-w-2xl text-sm text-muted-foreground">
			{#if detail}
				{scopeKind} results for {scopeLabel}{detail.year ? ` in ${detail.year}` : ''}. Pick a
				province, district or year below, or drill into a region.
			{:else}
				NSC results for this subject.
			{/if}
		</p>
	</div>

	{#if data.loadError || !detail}
		<Card.Root>
			<Card.Content class="flex h-48 flex-col items-center justify-center gap-3">
				<p role="alert" class="text-sm text-destructive">
					{data.loadError ?? 'Failed to load subject results'}
				</p>
				<Button variant="outline" size="sm" onclick={retry}>Try again</Button>
			</Card.Content>
		</Card.Root>
	{:else}
		<Card.Root>
			<Card.Header>
				<Card.Title class="flex items-center gap-2 text-base">
					<MapPin class="size-4 text-primary" />
					Scope
				</Card.Title>
				<Card.Description>Narrow the results to a province or district and year.</Card.Description>
			</Card.Header>
			<Card.Content class="space-y-4">
				<div class="flex flex-wrap items-center gap-2">
					<Select.Root
						type="single"
						value={scope.province ?? ALL}
						onValueChange={selectProvince}
						disabled={updating}
					>
						<Select.Trigger class="h-8 w-full sm:w-48" aria-label="Province">
							<span class={cn(!scope.province && 'text-muted-foreground')}>
								{scope.province ? toTitleCase(scope.province) : 'All provinces'}
							</span>
						</Select.Trigger>
						<Select.Content>
							<Select.Item value={ALL}>All provinces</Select.Item>
							{#each provinceOptions as option (option)}
								<Select.Item value={option}>{toTitleCase(option)}</Select.Item>
							{/each}
						</Select.Content>
					</Select.Root>

					{#if scope.province}
						<Select.Root
							type="single"
							value={scope.district ?? ALL}
							onValueChange={selectDistrict}
							disabled={updating}
						>
							<Select.Trigger class="h-8 w-full sm:w-52" aria-label="District">
								<span class={cn(!scope.district && 'text-muted-foreground')}>
									{scope.district ? toTitleCase(scope.district) : 'All districts'}
								</span>
							</Select.Trigger>
							<Select.Content>
								<Select.Item value={ALL}>All districts</Select.Item>
								{#each districtOptions as option (option)}
									<Select.Item value={option}>{toTitleCase(option)}</Select.Item>
								{/each}
							</Select.Content>
						</Select.Root>

						<Button variant="outline" size="sm" onclick={clearScope} disabled={updating}>
							<X class="size-3.5" />
							National view
						</Button>
					{/if}

					{#if updating}
						<span role="status" class="text-xs text-muted-foreground">Updating...</span>
					{/if}
				</div>

				{#if detail.availableYears.length > 0}
					<YearPills
						years={detail.availableYears}
						selected={detail.year}
						onSelect={selectYear}
						disabled={updating}
					/>
				{/if}
			</Card.Content>
		</Card.Root>

		{#if !hasResults}
			<Card.Root>
				<Card.Content>
					<EmptyState
						icon={BookMarked}
						message={`No ${detail.name} results for ${scopeLabel}${scope.year ? ` in ${scope.year}` : ''}.`}
					/>
				</Card.Content>
			</Card.Root>
		{:else}
			<div
				aria-busy={updating}
				class={cn('space-y-6 motion-safe:transition-opacity', updating && 'opacity-60')}
			>
				<div
					class="grid grid-cols-1 gap-4 *:data-[slot=card]:bg-gradient-to-t *:data-[slot=card]:from-primary/5 *:data-[slot=card]:to-card *:data-[slot=card]:shadow-xs sm:grid-cols-2 lg:grid-cols-4 dark:*:data-[slot=card]:bg-card"
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

				<div class="grid grid-cols-1 gap-6 lg:grid-cols-2">
					<Card.Root>
						<Card.Header>
							<Card.Title class="flex items-center gap-2 text-base">
								<TrendingUp class="size-4 text-primary" />
								Trend
							</Card.Title>
							<Card.Description>
								Pass rate<InfoTooltip label="subject pass rate" text={metricHelp.subjectPassRate} /> and
								learners who wrote, {scopeLabel}
							</Card.Description>
						</Card.Header>
						<Card.Content class="space-y-5">
							<SmoothTrendChart years={trendPoints} height={200} />
							<div>
								<p class="mb-2 text-xs font-medium text-muted-foreground">
									Learners who wrote<InfoTooltip
										label="subject learners who wrote"
										text={metricHelp.subjectWrote}
									/>
								</p>
								<ul class="flex h-20 items-end gap-1" aria-label="Learners who wrote by year">
									{#each detail.trend as point (point.year)}
										<li class="flex h-full min-w-0 flex-1 flex-col justify-end">
											<span class="sr-only"
												>{point.year}: {point.totalWrote.toLocaleString()} wrote</span
											>
											<div
												aria-hidden="true"
												title={`${point.year}: ${point.totalWrote.toLocaleString()} wrote`}
												class={cn(
													'w-full rounded-t-sm motion-safe:transition-[height] motion-safe:duration-500',
													point.year === detail.year ? 'bg-primary' : 'bg-primary/25'
												)}
												style:height={`${Math.max(2, (point.totalWrote / maxTrendWrote) * 100)}%`}
											></div>
										</li>
									{/each}
								</ul>
								<div
									class="mt-1 flex justify-between text-[11px] text-muted-foreground tabular-nums"
									aria-hidden="true"
								>
									<span>{detail.trend[0]?.year}</span>
									<span>{detail.trend.at(-1)?.year}</span>
								</div>
							</div>
						</Card.Content>
					</Card.Root>

					<Card.Root>
						<Card.Header>
							<Card.Title class="flex items-center gap-2 text-base">
								<MapPin class="size-4 text-primary" />
								{breakdownKind === 'district' ? 'By district' : 'By province'}
							</Card.Title>
							<Card.Description>
								{breakdownKind === 'district'
									? `Districts in ${toTitleCase(scope.province ?? '')}`
									: 'Every province'}{detail.year ? ` · ${detail.year}` : ''}. Select a row to drill
								in.
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
				</div>
			</div>

			<SubjectSchoolsTable
				code={data.code}
				{subjectName}
				province={scope.province}
				district={scope.district}
				year={detail.year}
			/>
		{/if}
	{/if}
</div>
