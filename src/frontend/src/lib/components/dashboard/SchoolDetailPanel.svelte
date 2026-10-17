<script lang="ts">
	import InfoTooltip from '$lib/components/common/InfoTooltip.svelte';
	import { metricHelp } from '$lib/components/common/metric-help';
	import type { Snippet } from 'svelte';
	import { SvelteURLSearchParams } from 'svelte/reactivity';
	import { resolve } from '$app/paths';
	import { Skeleton } from '$lib/components/ui/skeleton';
	import { Button } from '$lib/components/ui/button';
	import * as Card from '$lib/components/ui/card';
	import { fetchSchoolById, fetchRegionStats, fetchRegionSubjects } from '$lib/api/dashboard';
	import type { RegionStats, RegionSubjects, SchoolDetail } from '$lib/types/dashboard';
	import { schoolPath } from '$lib/config';
	import { toTitleCase } from '$lib/utils';
	import SchoolPerformanceTimeline from './SchoolPerformanceTimeline.svelte';
	import SubjectPerformanceBars from './SubjectPerformanceBars.svelte';
	import {
		Building2,
		School as SchoolIcon,
		MapPin,
		Users,
		GraduationCap,
		XCircle,
		ExternalLink,
		Network
	} from '@lucide/svelte';

	let {
		selectedSchoolId = null,
		selectedProvince = null,
		selectedDistrict = null,
		headerAction
	}: {
		selectedSchoolId?: string | null;
		selectedProvince?: string | null;
		selectedDistrict?: string | null;
		headerAction?: Snippet;
	} = $props();

	let school = $state<SchoolDetail | null>(null);
	let loading = $state(true);
	let error = $state<string | null>(null);
	let retry = $state(0);
	let request = $derived({ id: selectedSchoolId, attempt: retry });

	// Load the selected school by its stable id (EMIS). Doing this per selection
	// keeps the panel correct even when the school is not in the tree's page.
	$effect(() => {
		const id = request.id;

		if (!id) {
			school = null;
			error = null;
			loading = false;
			return;
		}

		let cancelled = false;
		// Keep the last result in place until its replacement is ready. The busy
		// content is inert so links cannot open the previous school by mistake.
		loading = true;
		error = null;

		fetchSchoolById(id)
			.then((result) => {
				if (!cancelled) school = result;
			})
			.catch((e) => {
				if (!cancelled) error = e instanceof Error ? e.message : 'Failed to load school.';
			})
			.finally(() => {
				if (!cancelled) loading = false;
			});

		return () => {
			cancelled = true;
		};
	});

	let failedCount = $derived(school ? Math.max(0, school.totalWrote - school.totalPassed) : 0);

	// When no school is selected the panel describes the selected scope:
	// a district, a province, or the whole country.
	let region = $state<RegionStats | null>(null);
	let regionSubjects = $state<RegionSubjects | null>(null);
	let subjectsLoading = $state(false);
	let subjectsError = $state<string | null>(null);
	let subjectsRetry = $state(0);
	let regionLoading = $state(true);
	let regionError = $state<string | null>(null);
	let regionRequest = $derived({
		enabled: !selectedSchoolId,
		province: selectedProvince,
		district: selectedDistrict
	});

	$effect(() => {
		const { enabled, province, district } = regionRequest;
		if (!enabled) {
			region = null;
			regionError = null;
			regionLoading = false;
			return;
		}
		let cancelled = false;
		region = null;
		regionLoading = true;
		regionError = null;
		fetchRegionStats(province, district)
			.then((result) => {
				if (!cancelled) region = result;
			})
			.catch((e) => {
				if (!cancelled)
					regionError = e instanceof Error ? e.message : 'Failed to load region stats.';
			})
			.finally(() => {
				if (!cancelled) regionLoading = false;
			});
		return () => {
			cancelled = true;
		};
	});

	$effect(() => {
		const currentRegion = region;
		const { enabled, province, district } = regionRequest;
		void subjectsRetry;
		regionSubjects = null;
		subjectsError = null;
		subjectsLoading = false;
		if (!enabled || !currentRegion) return;
		let cancelled = false;
		subjectsLoading = true;
		fetchRegionSubjects(province, district, currentRegion.year)
			.then((result) => {
				if (!cancelled) regionSubjects = result;
			})
			.catch((e) => {
				if (!cancelled)
					subjectsError = e instanceof Error ? e.message : 'Failed to load subject results.';
			})
			.finally(() => {
				if (!cancelled) subjectsLoading = false;
			});
		return () => {
			cancelled = true;
		};
	});

	let regionSubjectsHref = $derived.by(() => {
		const params = new SvelteURLSearchParams();
		if (selectedProvince) params.set('province', selectedProvince);
		if (selectedDistrict) params.set('district', selectedDistrict);
		if (regionSubjects?.year) params.set('year', String(regionSubjects.year));
		return `/subjects?${params.toString()}` as const;
	});

	let regionFailedCount = $derived(
		region ? Math.max(0, region.totalWrote - region.totalPassed) : 0
	);

	let timeline = $derived(school ? [...school.years].sort((a, b) => a.year - b.year) : []);
	let latestYear = $derived(timeline.at(-1)?.year ?? null);
	let delta = $derived(
		timeline.length > 1 ? timeline[timeline.length - 1]!.passRate - timeline[0]!.passRate : null
	);
	let regionTimeline = $derived(region ? [...region.years].sort((a, b) => a.year - b.year) : []);
	let regionDelta = $derived(
		regionTimeline.length > 1
			? regionTimeline[regionTimeline.length - 1]!.passRate - regionTimeline[0]!.passRate
			: null
	);

	function deltaColor(value: number | null): string {
		if (value === null || Math.abs(value) < 0.05) return 'text-muted-foreground';
		return value > 0 ? 'text-green-700 dark:text-green-400' : 'text-red-700 dark:text-red-400';
	}

	function passRateColor(rate: number): string {
		if (rate >= 80) return 'text-green-700 dark:text-green-400';
		if (rate >= 60) return 'text-amber-700 dark:text-amber-400';
		return 'text-red-700 dark:text-red-400';
	}

	function formatRate(rate: number): string {
		return `${rate.toFixed(1)}%`;
	}
</script>

<section
	aria-label="School details"
	class="@container/details flex h-full min-h-0 flex-col bg-card text-card-foreground"
>
	<header class="flex min-h-14 shrink-0 items-center gap-2 bg-muted/60 px-3">
		<Building2 class="size-4 shrink-0 text-muted-foreground" />
		<div class="min-w-0 flex-1">
			<h2 class="text-sm font-semibold">
				{selectedSchoolId ? 'School details' : 'Region details'}
			</h2>
		</div>
		{@render headerAction?.()}
	</header>
	<div
		class="min-h-0 flex-1 overflow-y-auto p-3 pt-5 @[320px]/details:p-4"
		aria-busy={selectedSchoolId ? loading : regionLoading}
	>
		{#if !selectedSchoolId}
			{#if regionLoading}
				<div role="status" class="space-y-4">
					<span class="sr-only">Loading region stats…</span>
					<Skeleton class="h-5 w-40 rounded-full" />
					<div class="space-y-3 rounded-xl bg-overview-metric p-3">
						<Skeleton class="h-3 w-28 rounded-full" />
						<Skeleton class="h-9 w-24 rounded-md" />
						<Skeleton class="h-1.5 w-full rounded-full" />
						<Skeleton class="h-3 w-40 rounded-full" />
					</div>
				</div>
			{:else if regionError}
				<div role="alert" class="space-y-3 rounded-lg border bg-muted/30 p-4">
					<p class="text-sm font-medium">Unable to load region stats</p>
					<p class="text-xs text-muted-foreground">{regionError}</p>
				</div>
			{:else if region}
				<div class="space-y-4">
					<div class="min-w-0 px-1">
						<h3 class="text-base leading-snug font-semibold tracking-tight">
							{region.scope === 'country' ? 'South Africa' : toTitleCase(region.name)}
						</h3>
						<p class="mt-1 flex items-center gap-1 text-xs text-muted-foreground">
							<Network class="size-3" />
							{region.scope === 'country'
								? 'National overview'
								: region.scope === 'province'
									? 'Province overview'
									: `District in ${toTitleCase(region.province ?? '')}`}
						</p>
					</div>

					<Card.Root
						class="border-0 bg-overview-metric shadow-sm"
						style="--muted-foreground: var(--overview-muted-foreground)"
						data-overview-metric
					>
						<Card.Content class="p-3 @[320px]/details:p-4">
							<div class="flex items-end justify-between gap-3">
								<div>
									<p class="text-xs text-muted-foreground">
										NSC pass rate · {region.year}<InfoTooltip
											label="NSC pass rate"
											text={metricHelp.passRate}
										/>
									</p>
									<p
										class="mt-1 text-4xl font-semibold tracking-tight tabular-nums {passRateColor(
											region.passRate
										)}"
									>
										{formatRate(region.passRate)}
									</p>
								</div>
								{#if regionDelta !== null}
									<span
										class="pb-1 text-xs font-medium whitespace-nowrap tabular-nums {deltaColor(
											regionDelta
										)}"
									>
										{regionDelta >= 0 ? '+' : ''}{regionDelta.toFixed(1)} pts<InfoTooltip
											label="pass rate change"
											text={`${metricHelp.points} Compares ${regionTimeline[0]?.year} with ${regionTimeline.at(-1)?.year}.`}
										/>
									</span>
								{/if}
							</div>
							<div class="mt-3">
								<SchoolPerformanceTimeline years={region.years} />
							</div>
							<p class="mt-2 text-xs text-muted-foreground">
								{region.totalPassed.toLocaleString()} of {region.totalWrote.toLocaleString()} learners
								passed
							</p>
						</Card.Content>
					</Card.Root>

					<Card.Root
						class="border-0 bg-overview-summary shadow-sm"
						style="--muted-foreground: var(--overview-muted-foreground)"
						data-overview-summary
					>
						<Card.Header class="p-3 pb-2 @[320px]/details:px-4">
							<Card.Title class="text-sm font-semibold" role="heading" aria-level={3}>
								{region.scope === 'country'
									? 'Country'
									: region.scope === 'province'
										? 'Province'
										: 'District'} summary
							</Card.Title>
						</Card.Header>
						<Card.Content class="px-3 pb-3 @[320px]/details:px-4">
							<dl class="space-y-3 text-sm">
								<div class="flex items-baseline justify-between gap-4">
									<dt class="flex items-center gap-1.5 text-xs text-muted-foreground">
										<SchoolIcon class="size-3.5" />
										Schools
									</dt>
									<dd class="tabular-nums">{region.schoolCount.toLocaleString()}</dd>
								</div>
								<div class="flex items-baseline justify-between gap-4">
									<dt class="flex items-center gap-1.5 text-xs text-muted-foreground">
										<Users class="size-3.5" />
										Learners wrote<InfoTooltip label="learners who wrote" text={metricHelp.wrote} />
									</dt>
									<dd class="tabular-nums">{region.totalWrote.toLocaleString()}</dd>
								</div>
								<div class="flex items-baseline justify-between gap-4">
									<dt class="flex items-center gap-1.5 text-xs text-muted-foreground">
										<GraduationCap class="size-3.5" />
										Passed
									</dt>
									<dd class="text-green-700 tabular-nums dark:text-green-400">
										{region.totalPassed.toLocaleString()}
									</dd>
								</div>
								<div class="flex items-baseline justify-between gap-4">
									<dt class="flex items-center gap-1.5 text-xs text-muted-foreground">
										<XCircle class="size-3.5" />
										Did not pass
									</dt>
									<dd class="text-red-700 tabular-nums dark:text-red-400">
										{regionFailedCount.toLocaleString()}
									</dd>
								</div>
							</dl>
						</Card.Content>
					</Card.Root>
					<section class="px-1 pt-1" aria-busy={subjectsLoading}>
						<div class="mb-3 flex flex-wrap items-baseline justify-between gap-2">
							<h3 class="text-sm font-semibold">
								Subject results{regionSubjects?.year ? ` · ${regionSubjects.year}` : ''}
							</h3>
							<a
								href={resolve(regionSubjectsHref)}
								class="shrink-0 text-xs font-medium text-primary underline-offset-4 hover:underline"
								>View all subjects</a
							>
						</div>
						{#if subjectsLoading}
							<div role="status" class="space-y-2">
								<span class="sr-only">Loading subject results…</span>
								{#each [1, 2, 3] as row (row)}<Skeleton class="h-9 w-full rounded-md" />{/each}
							</div>
						{:else if subjectsError}
							<div role="alert" class="space-y-2 text-xs">
								<p class="text-muted-foreground">{subjectsError}</p>
								<button
									type="button"
									onclick={() => subjectsRetry++}
									class="rounded-md border px-3 py-2 hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring"
									>Try again</button
								>
							</div>
						{:else if regionSubjects && regionSubjects.subjects.length > 0}
							<p class="mb-3 text-xs text-muted-foreground">
								Across {regionSubjects.schoolCount.toLocaleString()} schools with subject data<InfoTooltip
									label="subject data coverage"
									text={metricHelp.coverage}
								/>
							</p>
							<SubjectPerformanceBars
								subjects={regionSubjects.subjects}
								headlineYear={regionSubjects.year}
								class="space-y-3"
							/>
						{:else}
							<p class="text-sm text-muted-foreground">
								No subject data for this region in {region.year}.
							</p>
						{/if}
					</section>
				</div>
			{/if}
		{:else if selectedSchoolId && loading && !school}
			<div role="status" class="space-y-5">
				<span class="sr-only">Loading selected school…</span>
				<div class="flex items-center gap-3">
					<Skeleton class="size-8 shrink-0 rounded-full" />
					<div class="space-y-1.5">
						<Skeleton class="h-4 w-40 rounded-full" />
						<Skeleton class="h-3 w-24 rounded-full" />
					</div>
				</div>
				<div class="space-y-3 rounded-xl bg-overview-metric p-3">
					<Skeleton class="h-3 w-28 rounded-full" />
					<Skeleton class="h-8 w-24 rounded-md" />
					<Skeleton class="h-1.5 w-full rounded-full" />
					<Skeleton class="h-3 w-40 rounded-full" />
				</div>
				{#each [[['w-24', 'w-16'], ['w-20', 'w-10'], ['w-28', 'w-12']], [['w-28', 'w-10'], ['w-24', 'w-12'], ['w-32', 'w-10']]] as rows, i (i)}
					<div class="space-y-2">
						<Skeleton class="h-3 w-24 rounded-full" />
						{#each rows as [label, value], j (j)}
							<div class="flex items-center justify-between">
								<Skeleton class="h-3 {label} rounded-full" />
								<Skeleton class="h-3 {value} rounded-full" />
							</div>
						{/each}
					</div>
				{/each}
			</div>
		{:else if selectedSchoolId && error}
			<div role="alert" class="space-y-3 rounded-lg border bg-muted/30 p-4">
				<p class="text-sm font-medium">Unable to load school details</p>
				<p class="text-xs text-muted-foreground">{error}</p>
				<button
					type="button"
					onclick={() => retry++}
					class="rounded-md border px-3 py-2 text-xs hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring"
					>Try again</button
				>
			</div>
		{:else if selectedSchoolId && !school}
			<div role="status" class="rounded-lg border bg-muted/30 p-4 text-sm text-muted-foreground">
				School not found.
			</div>
		{:else if school}
			{@const offered = school.subjectsOffered ?? []}
			{#key school.id}
				<div class="detail-content space-y-4" class:updating={loading} inert={loading}>
					<div class="flex items-start justify-between gap-3 px-1">
						<div class="min-w-0">
							<h3 class="text-base leading-snug font-semibold tracking-tight">
								{toTitleCase(school.name)}
							</h3>
							<p class="mt-1 flex items-center gap-1 text-xs text-muted-foreground">
								<MapPin class="size-3" />
								{toTitleCase(school.province)}
							</p>
						</div>
					</div>

					<Card.Root
						class="border-0 bg-overview-metric shadow-sm"
						style="--muted-foreground: var(--overview-muted-foreground)"
						data-overview-metric
					>
						<Card.Content class="p-3 @[320px]/details:p-4">
							<div class="flex items-end justify-between gap-3">
								<div>
									<p class="text-xs text-muted-foreground">
										NSC pass rate{latestYear ? ` · ${latestYear}` : ''}<InfoTooltip
											label="NSC pass rate"
											text={metricHelp.passRate}
										/>
									</p>
									<p
										class="mt-1 text-4xl font-semibold tracking-tight tabular-nums {passRateColor(
											school.passRate
										)}"
									>
										{formatRate(school.passRate)}
									</p>
								</div>
								{#if delta !== null}
									<span
										class="pb-1 text-xs font-medium whitespace-nowrap tabular-nums {deltaColor(
											delta
										)}"
									>
										{delta >= 0 ? '+' : ''}{delta.toFixed(1)} pts<InfoTooltip
											label="pass rate change"
											text={`${metricHelp.points} Compares ${timeline[0]?.year} with ${latestYear}.`}
										/>
									</span>
								{/if}
							</div>
							<div class="mt-3">
								<SchoolPerformanceTimeline years={school.years} />
							</div>
							<p class="mt-2 text-xs text-muted-foreground">
								{school.totalPassed.toLocaleString()} of {school.totalWrote.toLocaleString()} learners
								passed
							</p>
						</Card.Content>
					</Card.Root>

					<Button
						variant="outline"
						class="w-full gap-1.5 shadow-none"
						href={resolve(schoolPath(school.id))}
						aria-label={`View full page for ${toTitleCase(school.name)}`}
					>
						<ExternalLink class="size-4" />
						View full school page
					</Button>

					<section class="rounded-xl bg-muted/30 p-3">
						<h3 class="mb-3 text-sm font-semibold">Identification</h3>
						<dl class="space-y-3 text-sm">
							<div class="flex items-baseline justify-between gap-4">
								<dt class="text-xs text-muted-foreground">
									EMIS number<InfoTooltip label="EMIS number" text={metricHelp.emis} />
								</dt>
								<dd class="tabular-nums">{school.id}</dd>
							</div>
							<div class="flex items-baseline justify-between gap-4">
								<dt class="text-xs text-muted-foreground">Province</dt>
								<dd class="min-w-0 text-end break-words">{toTitleCase(school.province)}</dd>
							</div>
							<div class="flex items-baseline justify-between gap-4">
								<dt class="text-xs text-muted-foreground">Circuit</dt>
								<dd class="min-w-0 text-end break-words">{toTitleCase(school.circuit)}</dd>
							</div>
						</dl>
					</section>

					<Card.Root
						class="border-0 bg-overview-summary shadow-sm"
						style="--muted-foreground: var(--overview-muted-foreground)"
						data-overview-summary
					>
						<Card.Header class="p-3 pb-2 @[320px]/details:px-4">
							<Card.Title class="text-sm font-semibold" role="heading" aria-level={3}>
								NSC Results{latestYear ? ` · ${latestYear}` : ''}
							</Card.Title>
						</Card.Header>
						<Card.Content class="px-3 pb-3 @[320px]/details:px-4">
							<dl class="space-y-3 text-sm">
								<div class="flex items-baseline justify-between gap-4">
									<dt class="flex items-center gap-1.5 text-xs text-muted-foreground">
										<Users class="size-3.5" />
										Learners wrote<InfoTooltip label="learners who wrote" text={metricHelp.wrote} />
									</dt>
									<dd class="tabular-nums">{school.totalWrote.toLocaleString()}</dd>
								</div>
								<div class="flex items-baseline justify-between gap-4">
									<dt class="flex items-center gap-1.5 text-xs text-muted-foreground">
										<GraduationCap class="size-3.5" />
										Passed
									</dt>
									<dd class="text-green-700 tabular-nums dark:text-green-400">
										{school.totalPassed.toLocaleString()}
									</dd>
								</div>
								<div class="flex items-baseline justify-between gap-4">
									<dt class="flex items-center gap-1.5 text-xs text-muted-foreground">
										<XCircle class="size-3.5" />
										Did not pass
									</dt>
									<dd class="text-red-700 tabular-nums dark:text-red-400">
										{failedCount.toLocaleString()}
									</dd>
								</div>
							</dl>
						</Card.Content>
					</Card.Root>

					<section class="px-1 pt-1">
						<div class="mb-3 flex flex-wrap items-baseline justify-between gap-2">
							<h3 class="text-sm font-semibold">
								Subjects offered<InfoTooltip
									label="subjects offered"
									text={metricHelp.gateway}
								/>{school.subjectsOfferedYear ? ` · ${school.subjectsOfferedYear}` : ''}
							</h3>
							<a
								href={resolve(`/subjects?school=${encodeURIComponent(school.id)}`)}
								class="shrink-0 text-xs font-medium text-primary underline-offset-4 hover:underline"
							>
								View all subjects
							</a>
						</div>
						{#if offered.length === 0}
							<p class="text-sm text-muted-foreground">No subject data</p>
						{:else}
							<SubjectPerformanceBars
								subjects={offered}
								headlineYear={school.subjectsOfferedYear}
								class="space-y-3"
							/>
						{/if}
					</section>
				</div>
			{/key}
		{/if}
	</div>
</section>

<style>
	.detail-content {
		transition: opacity 160ms ease-out;
	}

	.detail-content.updating {
		opacity: 0.55;
	}

	@media (prefers-reduced-motion: no-preference) {
		.detail-content > :global(*) {
			animation: detail-enter 450ms ease-in-out both;
		}

		.detail-content > :global(:nth-child(2)) {
			animation-delay: 70ms;
		}

		.detail-content > :global(:nth-child(3)) {
			animation-delay: 140ms;
		}

		.detail-content > :global(:nth-child(4)) {
			animation-delay: 210ms;
		}

		.detail-content > :global(:nth-child(5)) {
			animation-delay: 280ms;
		}

		.detail-content > :global(:nth-child(n + 6)) {
			animation-delay: 350ms;
		}
	}

	@keyframes detail-enter {
		from {
			opacity: 0;
			transform: translateY(10px);
		}
		to {
			opacity: 1;
			transform: translateY(0);
		}
	}

	@media (prefers-reduced-motion: reduce) {
		.detail-content {
			transition: none;
		}
	}
</style>
