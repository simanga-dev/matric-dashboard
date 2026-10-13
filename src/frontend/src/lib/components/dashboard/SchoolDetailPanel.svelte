<script lang="ts">
	import type { Snippet } from 'svelte';
	import { resolve } from '$app/paths';
	import { Skeleton } from '$lib/components/ui/skeleton';
	import { Button } from '$lib/components/ui/button';
	import { fetchSchoolById } from '$lib/api/dashboard';
	import type { SchoolDetail } from '$lib/types/dashboard';
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
		ExternalLink
	} from '@lucide/svelte';

	let {
		selectedSchoolId = null,
		selectedProvince = null,
		headerAction
	}: {
		selectedSchoolId?: string | null;
		selectedProvince?: string | null;
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
			<h2 class="text-sm font-semibold">School details</h2>
		</div>
		{@render headerAction?.()}
	</header>
	<div class="min-h-0 flex-1 overflow-y-auto p-4" aria-busy={loading}>
		{#if !selectedSchoolId && !selectedProvince}
			<div class="flex h-full flex-col justify-center py-6">
				<div
					class="mb-4 flex size-12 items-center justify-center rounded-xl border bg-primary/5 text-primary"
				>
					<SchoolIcon class="size-6" />
				</div>
				<h3 class="text-base font-semibold">Start with a school</h3>
				<p class="mt-2 text-sm leading-relaxed text-muted-foreground">
					Select a school in the browser or on the map to view its results.
				</p>
				<ol class="mt-6 space-y-3 text-xs text-muted-foreground">
					<li class="rounded-lg border bg-muted/20 p-3">
						<span class="font-medium text-foreground">01 · Browse</span>
						<p class="mt-1">Expand a province and circuit, or filter by name or EMIS.</p>
					</li>
					<li class="rounded-lg border bg-muted/20 p-3">
						<span class="font-medium text-foreground">02 · Inspect</span>
						<p class="mt-1">View learner counts, pass rate and school identification here.</p>
					</li>
				</ol>
			</div>
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
		{:else if selectedProvince}
			<div class="space-y-3 rounded-xl border bg-muted/20 p-4">
				<MapPin class="size-6 text-primary" />
				<h3 class="text-base font-semibold">{toTitleCase(selectedProvince)}</h3>
				<p class="text-sm leading-relaxed text-muted-foreground">
					Expand this province in the browser, then choose a circuit and school to view results.
				</p>
			</div>
		{/if}
	</div>
</section>
