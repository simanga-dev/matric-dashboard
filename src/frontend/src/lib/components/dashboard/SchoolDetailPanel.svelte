<script lang="ts">
	import type { Snippet } from 'svelte';
	import { resolve } from '$app/paths';
	import { Skeleton } from '$lib/components/ui/skeleton';
	import { Button } from '$lib/components/ui/button';
	import { fetchSchoolById, fetchRegionStats } from '$lib/api/dashboard';
	import type { RegionStats, SchoolDetail } from '$lib/types/dashboard';
	import { schoolPath } from '$lib/config';
	import { toTitleCase } from '$lib/utils';
	import SchoolPerformanceTimeline from './SchoolPerformanceTimeline.svelte';
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
	let loading = $state(false);
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
		school = null;
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
	let regionLoading = $state(false);
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

	let regionFailedCount = $derived(
		region ? Math.max(0, region.totalWrote - region.totalPassed) : 0
	);

	let timeline = $derived(school ? [...school.years].sort((a, b) => a.year - b.year) : []);
	let delta = $derived(
		timeline.length > 1 ? timeline[timeline.length - 1]!.passRate - timeline[0]!.passRate : null
	);
	let deltaClass = $derived(
		delta === null || Math.abs(delta) < 0.05
			? 'text-muted-foreground'
			: delta > 0
				? 'text-green-700 dark:text-green-400'
				: 'text-red-700 dark:text-red-400'
	);

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
	class="flex h-full min-h-0 flex-col bg-card text-card-foreground"
>
	<header class="flex h-11 shrink-0 items-center gap-3 border-b bg-muted/30 px-4">
		<Building2 class="size-4 shrink-0 text-primary" />
		<div class="min-w-0 flex-1">
			<h2 class="text-sm font-semibold">
				{selectedSchoolId ? 'School details' : 'Region details'}
			</h2>
		</div>
		{@render headerAction?.()}
	</header>
	<div class="min-h-0 flex-1 overflow-y-auto p-4" aria-busy={loading}>
		{#if !selectedSchoolId}
			{#if regionLoading}
				<div role="status" class="space-y-4">
					<span class="sr-only">Loading region stats…</span>
					<Skeleton class="h-5 w-40 rounded-full" />
					<div class="space-y-3 rounded-xl border bg-muted/20 p-4">
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
				<div class="space-y-5">
					<div class="min-w-0">
						<h3 class="text-base leading-tight font-semibold">
							{region.scope === 'country' ? 'South Africa' : toTitleCase(region.name)}
						</h3>
						<p class="mt-0.5 flex items-center gap-1 text-xs text-muted-foreground">
							<Network class="size-3" />
							{region.scope === 'country'
								? 'National overview'
								: region.scope === 'province'
									? 'Province overview'
									: `District in ${toTitleCase(region.province ?? '')}`}
						</p>
					</div>

					<div class="rounded-xl border bg-muted/20 p-4">
						<p class="text-xs text-muted-foreground">NSC pass rate · {region.year}</p>
						<p
							class="mt-1 text-4xl font-semibold tracking-tight tabular-nums {passRateColor(
								region.passRate
							)}"
						>
							{formatRate(region.passRate)}
						</p>
						<div class="mt-3 h-1.5 overflow-hidden rounded-full bg-muted" aria-hidden="true">
							<div
								class="h-full rounded-full bg-primary"
								style:width={`${Math.min(100, Math.max(0, region.passRate))}%`}
							></div>
						</div>
						<p class="mt-2 text-xs text-muted-foreground">
							{region.totalPassed.toLocaleString()} of {region.totalWrote.toLocaleString()} learners passed
						</p>
					</div>

					<section>
						<h3
							class="mb-2 border-b pb-1 text-xs font-semibold tracking-wide text-muted-foreground uppercase"
						>
							{region.scope === 'country'
								? 'Country'
								: region.scope === 'province'
									? 'Province'
									: 'District'} summary
						</h3>
						<dl class="space-y-1.5 text-sm">
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
									Learners wrote
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
					</section>
				</div>
			{/if}
		{:else if selectedSchoolId && loading}
			<div role="status" class="space-y-5">
				<span class="sr-only">Loading selected school…</span>
				<div class="flex items-center gap-3">
					<Skeleton class="size-8 shrink-0 rounded-full" />
					<div class="space-y-1.5">
						<Skeleton class="h-4 w-40 rounded-full" />
						<Skeleton class="h-3 w-24 rounded-full" />
					</div>
				</div>
				<div class="space-y-3 rounded-xl border bg-muted/20 p-4">
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
			<div class="space-y-5">
				<div class="flex items-start justify-between gap-3">
					<div class="min-w-0">
						<h3 class="text-base leading-tight font-semibold">{toTitleCase(school.name)}</h3>
						<p class="mt-0.5 flex items-center gap-1 text-xs text-muted-foreground">
							<MapPin class="size-3" />
							{toTitleCase(school.province)}
						</p>
					</div>
				</div>

				<div class="rounded-xl border bg-muted/20 p-4">
					<div class="flex items-end justify-between gap-3">
						<div>
							<p class="text-xs text-muted-foreground">NSC pass rate · 2024</p>
							<p
								class="mt-1 text-4xl font-semibold tracking-tight tabular-nums {passRateColor(
									school.passRate
								)}"
							>
								{formatRate(school.passRate)}
							</p>
						</div>
						{#if delta !== null}
							<span class="pb-1 text-xs font-medium whitespace-nowrap tabular-nums {deltaClass}">
								{delta >= 0 ? '+' : ''}{delta.toFixed(1)} pts
							</span>
						{/if}
					</div>
					<div class="mt-3">
						<SchoolPerformanceTimeline years={school.years} />
					</div>
					<p class="mt-2 text-xs text-muted-foreground">
						{school.totalPassed.toLocaleString()} of {school.totalWrote.toLocaleString()} learners passed
					</p>
				</div>

				<Button
					variant="outline"
					class="w-full gap-1.5"
					href={resolve(schoolPath(school.id))}
					aria-label={`View full page for ${toTitleCase(school.name)}`}
				>
					<ExternalLink class="size-4" />
					View full school page
				</Button>

				<section>
					<h3
						class="mb-2 border-b pb-1 text-xs font-semibold tracking-wide text-muted-foreground uppercase"
					>
						Identification
					</h3>
					<dl class="space-y-1.5 text-sm">
						<div class="flex items-baseline justify-between gap-4">
							<dt class="text-xs text-muted-foreground">EMIS number</dt>
							<dd class="tabular-nums">{school.id}</dd>
						</div>
						<div class="flex items-baseline justify-between gap-4">
							<dt class="text-xs text-muted-foreground">Province</dt>
							<dd class="text-right">{toTitleCase(school.province)}</dd>
						</div>
						<div class="flex items-baseline justify-between gap-4">
							<dt class="text-xs text-muted-foreground">Circuit</dt>
							<dd class="text-right">{toTitleCase(school.circuit)}</dd>
						</div>
					</dl>
				</section>

				<section>
					<h3
						class="mb-2 border-b pb-1 text-xs font-semibold tracking-wide text-muted-foreground uppercase"
					>
						NSC Results · 2024
					</h3>
					<dl class="space-y-1.5 text-sm">
						<div class="flex items-baseline justify-between gap-4">
							<dt class="flex items-center gap-1.5 text-xs text-muted-foreground">
								<Users class="size-3.5" />
								Learners wrote
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
				</section>
			</div>
		{/if}
	</div>
</section>
