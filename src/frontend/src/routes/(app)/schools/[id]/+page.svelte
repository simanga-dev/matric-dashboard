<script lang="ts">
	import InfoTooltip from '$lib/components/common/InfoTooltip.svelte';
	import { metricHelp } from '$lib/components/common/metric-help';
	import { resolve } from '$app/paths';
	import { goto } from '$app/navigation';
	import { EmptyState } from '$lib/components/common';
	import { Button } from '$lib/components/ui/button';
	import { toast } from '$lib/components/ui/sonner';
	import { passRateColor } from '$lib/components/dashboard/school-hierarchy';
	import SchoolLocationMap from '$lib/components/dashboard/SchoolLocationMap.svelte';
	import SmoothTrendChart from '$lib/components/dashboard/SmoothTrendChart.svelte';
	import { routes } from '$lib/config';
	import { setDynamicLabel, clearDynamicLabel } from '$lib/state/breadcrumb.svelte';
	import { cn, toTitleCase } from '$lib/utils';
	import type { PageData } from './$types';
	import {
		School as SchoolIcon,
		Users,
		GraduationCap,
		XCircle,
		Percent,
		MapPin,
		MapPinned,
		Building2,
		Calendar,
		ArrowLeft,
		TrendingUp,
		TrendingDown,
		Copy,
		Scale,
		Info,
		Award,
		Mail,
		ClipboardList
	} from '@lucide/svelte';

	let { data }: { data: PageData } = $props();

	// The server load resolves the school for the current route param, so the
	// page renders on first paint and stays correct across param changes.
	let school = $derived(data.school);

	// The system header breadcrumb shows the school's display name, not its id.
	$effect(() => {
		const name = school?.name;
		if (!name) return;
		setDynamicLabel(toTitleCase(name));
		return () => clearDynamicLabel();
	});

	function formatPassRate(rate: number): string {
		return `${rate.toFixed(1)}%`;
	}

	function viewOnMap() {
		if (!school) return;
		goto(resolve(`${routes.overview}?school=${encodeURIComponent(school.id)}`));
	}

	async function copyLink() {
		try {
			await navigator.clipboard.writeText(window.location.href);
			toast.success('Link copied to clipboard');
		} catch {
			toast.error('Could not copy the link');
		}
	}

	const quintileText: Record<number, string> = {
		1: 'serves the poorest communities and is a no-fee school',
		2: 'serves low-income communities and is a no-fee school',
		3: 'serves middle-income communities and is a no-fee school',
		4: 'serves more affluent communities and is fee-paying',
		5: 'serves the wealthiest communities and is fee-paying'
	};

	const phaseLabel: Record<string, string> = {
		Primary: 'Primary school',
		Secondary: 'Secondary school',
		Combined: 'Combined school',
		Intermediate: 'Intermediate school',
		SpecialNeeds: 'Special needs',
		Skills: 'School of skills',
		Other: 'Other'
	};

	const ownershipLabel: Record<string, string> = {
		Government: 'Government',
		Private: 'Private',
		Independent: 'Independent',
		Unknown: 'Unknown'
	};

	const settlementLabel: Record<string, string> = {
		Urban: 'Urban',
		Rural: 'Rural',
		Unknown: 'Unknown'
	};

	const sectorLabel: Record<string, string> = {
		Public: 'Public',
		Independent: 'Independent',
		Unknown: 'Unknown'
	};

	function formatPhone(value: string): string {
		const match = /^\+27(\d{2})(\d{3})(\d{4})$/.exec(value);
		return match ? `+27 ${match[1]} ${match[2]} ${match[3]}` : value;
	}

	function yesNo(value: boolean | null | undefined): string {
		return value === null || value === undefined ? 'n/a' : value ? 'Yes' : 'No';
	}

	let learnerEducatorRatio = $derived.by(() => {
		const enrolment = school?.enrolment;
		if (!enrolment?.learners || !enrolment?.educators) return null;
		return (enrolment.learners / enrolment.educators).toFixed(1);
	});

	let schoolName = $derived(school ? toTitleCase(school.name) : '');
	let headerCardHeight = $state(0);
	let mapBottomInset = $derived(headerCardHeight + 32);
	let latest = $derived(school?.years.at(-1) ?? null);
	let failedCount = $derived(latest ? Math.max(0, latest.totalWrote - latest.totalPassed) : 0);
	let yoy = $derived.by(() => {
		const years = school?.years ?? [];
		const last = years.at(-1);
		const prev = years.at(-2);
		if (!last || !prev) return null;
		return Math.round((last.passRate - prev.passRate) * 10) / 10;
	});
	let comparisonRows = $derived(
		school
			? [
					{ label: 'This school', value: school.passRate, color: passRateColor(school.passRate) },
					{
						label: `${toTitleCase(school.province)} average`,
						value: school.comparison.provinceAveragePassRate,
						color: '#6366f1'
					},
					{
						label: 'National average',
						value: school.comparison.nationalAveragePassRate,
						color: 'hsl(var(--muted-foreground))'
					}
				]
			: []
	);
	let nationalDelta = $derived(
		school && school.comparison.nationalAveragePassRate !== null
			? Math.round((school.passRate - school.comparison.nationalAveragePassRate) * 10) / 10
			: null
	);
</script>

<svelte:head>
	<title>{school ? schoolName : 'School'} • MatricDashboard</title>
	<meta
		name="description"
		content={school
			? `${schoolName} - ${toTitleCase(school.province)} matric performance`
			: 'School matric performance details'}
	/>
</svelte:head>

<div class="space-y-8 lg:space-y-12">
	{#if !school}
		<div>
			<Button
				variant="ghost"
				size="sm"
				class="gap-1"
				onclick={() => goto(resolve(routes.overview))}
			>
				<ArrowLeft class="size-4" />
				Back to overview
			</Button>
		</div>
		<EmptyState icon={SchoolIcon} message="School not found." />
	{:else}
		<section aria-label="School overview map" class="relative overflow-hidden">
			<div
				class="pointer-events-none relative z-10 flex min-h-[42rem] flex-col justify-end p-3 sm:min-h-[30rem] sm:p-4 lg:block"
			>
				<aside
					bind:clientHeight={headerCardHeight}
					class="pointer-events-auto relative overflow-hidden rounded-2xl bg-gradient-to-br from-background/60 via-background/40 to-background/30 shadow-2xl backdrop-blur-3xl lg:absolute lg:top-1/2 lg:left-4 lg:w-1/2 lg:-translate-y-1/2"
				>
					<div
						aria-hidden="true"
						class="pointer-events-none absolute inset-x-0 top-0 h-px bg-gradient-to-r from-transparent via-primary/60 to-transparent"
					></div>

					<div class="relative space-y-4 p-5 sm:p-6">
						<div class="flex items-start gap-3.5">
							<div
								class="flex size-11 shrink-0 items-center justify-center rounded-xl bg-primary/15 text-primary ring-1 ring-primary/20 ring-inset"
							>
								<SchoolIcon class="size-5" />
							</div>
							<div class="min-w-0 flex-1">
								<p
									class="text-[10px] font-medium tracking-[0.18em] text-muted-foreground uppercase"
								>
									School profile
								</p>
								<h1 class="mt-1 text-xl font-semibold tracking-tight text-balance sm:text-2xl">
									{schoolName}
								</h1>
								<div
									class="mt-1.5 flex flex-wrap items-center gap-x-3 gap-y-1 text-[11px] text-muted-foreground"
								>
									<span class="inline-flex items-center gap-1">
										<MapPin class="size-3" />
										{toTitleCase(school.province)}
									</span>
									<span class="h-3 w-px bg-border" aria-hidden="true"></span>
									<span class="inline-flex items-center gap-1">
										<Building2 class="size-3" />
										{toTitleCase(school.circuit)}
									</span>
									{#if latest}
										<span class="h-3 w-px bg-border" aria-hidden="true"></span>
										<span class="inline-flex items-center gap-1 tabular-nums">
											<Calendar class="size-3" />
											NSC {latest.year}
										</span>
									{/if}
								</div>
							</div>
						</div>

						<div class="flex flex-wrap gap-1.5">
							<span
								class="inline-flex items-center gap-0.5 rounded-md border border-border/60 bg-background/40 px-2 py-0.5 text-[11px] font-medium tabular-nums"
							>
								EMIS {school.id}
								<InfoTooltip label="EMIS number" text={metricHelp.emis} />
							</span>
							{#if school.centreNumber}
								<span
									class="inline-flex items-center gap-0.5 rounded-md border border-border/60 bg-background/40 px-2 py-0.5 text-[11px] font-medium tabular-nums"
								>
									Centre {school.centreNumber}
									<InfoTooltip label="examination centre" text={metricHelp.centre} />
								</span>
							{/if}
							<span
								class="inline-flex items-center gap-0.5 rounded-md border border-border/60 bg-background/40 px-2 py-0.5 text-[11px] font-medium tabular-nums"
							>
								Quintile {school.quintile ?? 'n/a'}
								<InfoTooltip label="quintile" text={metricHelp.quintile} />
							</span>
						</div>

						<div
							class="grid grid-cols-[minmax(0,1fr)_auto] items-end gap-4 border-t border-border/50 pt-4"
						>
							<div class="min-w-0">
								<p
									class="flex items-center gap-0.5 text-[10px] font-medium tracking-[0.16em] whitespace-nowrap text-muted-foreground uppercase"
								>
									Pass rate
									<InfoTooltip
										label="NSC pass rate"
										text={`${metricHelp.passRate} ${metricHelp.points}`}
									/>
								</p>
								<div class="mt-1 flex flex-wrap items-baseline gap-x-2 gap-y-1">
									<span
										class="text-4xl leading-none font-semibold tracking-tight tabular-nums"
										style:color={passRateColor(school.passRate)}
									>
										{formatPassRate(school.passRate)}
									</span>
									{#if yoy !== null}
										<span
											class={cn(
												'inline-flex items-center gap-0.5 rounded-full px-1.5 py-0.5 text-[11px] font-medium',
												yoy >= 0
													? 'bg-green-500/10 text-green-600 dark:text-green-400'
													: 'bg-red-500/10 text-red-600 dark:text-red-400'
											)}
										>
											{#if yoy >= 0}<TrendingUp class="size-3" />{:else}<TrendingDown
													class="size-3"
												/>{/if}
											{Math.abs(yoy).toFixed(1)} pts
										</span>
									{/if}
								</div>
							</div>
							<div class="flex shrink-0 items-center gap-2">
								<Button variant="secondary" size="sm" class="gap-1.5 shadow-sm" onclick={viewOnMap}>
									<MapPinned class="size-4" />
									View on map
								</Button>
								<Button
									variant="outline"
									size="icon-sm"
									class="shadow-sm"
									onclick={copyLink}
									aria-label="Copy link to this school"
									title="Copy link"
								>
									<Copy class="size-4" />
								</Button>
							</div>
						</div>
					</div>
				</aside>
			</div>
			<div class="absolute inset-0 z-0">
				{#key school.id}
					<SchoolLocationMap spatial={data.spatial} {schoolName} bottomInset={mapBottomInset} />
				{/key}
			</div>
		</section>

		<div class="grid grid-cols-2 gap-x-4 gap-y-6 sm:grid-cols-4 sm:gap-x-6">
			<div class="flex items-center gap-3">
				<div
					class="flex size-9 shrink-0 items-center justify-center rounded-xl bg-muted/60 text-muted-foreground sm:size-10"
				>
					<Users class="size-5" />
				</div>
				<div class="min-w-0">
					<p class="text-2xl font-semibold tracking-tight tabular-nums">
						{school.totalWrote.toLocaleString()}
					</p>
					<p class="flex items-center gap-0.5 text-xs text-muted-foreground">
						Learners wrote<InfoTooltip label="learners who wrote" text={metricHelp.wrote} />
					</p>
				</div>
			</div>
			<div class="flex items-center gap-3">
				<div
					class="flex size-9 shrink-0 items-center justify-center rounded-xl bg-green-500/10 text-green-600 sm:size-10 dark:text-green-400"
				>
					<GraduationCap class="size-5" />
				</div>
				<div class="min-w-0">
					<p class="text-2xl font-semibold tracking-tight tabular-nums">
						{school.totalPassed.toLocaleString()}
					</p>
					<p class="text-xs text-muted-foreground">Passed</p>
				</div>
			</div>
			<div class="flex items-center gap-3">
				<div
					class="flex size-9 shrink-0 items-center justify-center rounded-xl bg-red-500/10 text-red-600 sm:size-10 dark:text-red-400"
				>
					<XCircle class="size-5" />
				</div>
				<div class="min-w-0">
					<p class="text-2xl font-semibold tracking-tight tabular-nums">
						{failedCount.toLocaleString()}
					</p>
					<p class="text-xs text-muted-foreground">Did not pass</p>
				</div>
			</div>
			<div class="flex items-center gap-3">
				<div
					class="flex size-9 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary sm:size-10"
				>
					<Percent class="size-5" />
				</div>
				<div class="min-w-0">
					<p
						class="text-2xl font-semibold tracking-tight tabular-nums"
						style:color={passRateColor(school.passRate)}
					>
						{formatPassRate(school.passRate)}
					</p>
					<p class="flex items-center gap-0.5 text-xs text-muted-foreground">
						Pass rate<InfoTooltip label="NSC pass rate" text={metricHelp.passRate} />
					</p>
				</div>
			</div>
		</div>

		<div class="grid gap-10 lg:grid-cols-2 lg:gap-x-16 lg:gap-y-12">
			<section class="space-y-4">
				<header class="space-y-1">
					<h2 class="flex items-center gap-2 text-base font-semibold">
						<TrendingUp class="size-4 text-primary" />
						Performance over time
					</h2>
					<p class="text-sm text-muted-foreground">Pass rate by academic year.</p>
				</header>
				<div class="space-y-3">
					<SmoothTrendChart years={school.years} height={220} />
					{#if latest}
						<p class="text-xs text-muted-foreground">
							{latest.progressed.toLocaleString()} progressed learners wrote in {latest.year} of
							{latest.totalWrote.toLocaleString()} total.
						</p>
					{/if}
				</div>
			</section>

			<section class="space-y-4">
				<header class="space-y-1">
					<h2 class="flex items-center gap-2 text-base font-semibold">
						<Scale class="size-4 text-primary" />
						How it compares
					</h2>
					<p class="text-sm text-muted-foreground">
						Against {toTitleCase(school.province)} and the country ({school.comparison.year ??
							latest?.year ??
							'latest year'}).
					</p>
				</header>
				<div class="space-y-4">
					{#if school.comparison.provinceRank > 0}
						<p class="text-sm">
							Ranked <span class="font-semibold">{school.comparison.provinceRank}</span> of
							<span class="font-semibold"
								>{school.comparison.provinceSchoolCount.toLocaleString()}</span
							>
							schools in {toTitleCase(school.province)}.<InfoTooltip
								label="school ranking"
								text={metricHelp.ranking}
							/>
						</p>
					{/if}
					<ul class="space-y-3">
						{#each comparisonRows as row (row.label)}
							<li class="space-y-1.5">
								<div class="flex items-baseline justify-between text-sm">
									<span class="text-muted-foreground">{row.label}</span>
									<span class="font-semibold tabular-nums" style:color={row.color}>
										{row.value === null ? 'n/a' : `${row.value.toFixed(1)}%`}
									</span>
								</div>
								<div class="h-2 w-full overflow-hidden rounded-full bg-muted">
									<div
										class="h-full rounded-full"
										style:background={row.color}
										style:width={`${row.value === null ? 0 : Math.min(100, row.value)}%`}
									></div>
								</div>
							</li>
						{/each}
					</ul>
					{#if nationalDelta !== null}
						<p
							class={cn(
								'flex items-center gap-1.5 text-xs font-medium',
								nationalDelta >= 0
									? 'text-green-600 dark:text-green-400'
									: 'text-red-600 dark:text-red-400'
							)}
						>
							{#if nationalDelta >= 0}<TrendingUp class="size-3.5" />{:else}<TrendingDown
									class="size-3.5"
								/>{/if}
							{Math.abs(nationalDelta).toFixed(1)} points {nationalDelta >= 0 ? 'above' : 'below'} the
							national average
						</p>
					{/if}
				</div>
			</section>

			<section class="space-y-4">
				<header class="space-y-1">
					<h2 class="flex items-center gap-2 text-base font-semibold">
						<Info class="size-4 text-primary" />
						Quintile context
					</h2>
					<p class="text-sm text-muted-foreground">
						Socio-economic classification used for school funding.
					</p>
				</header>
				<div class="space-y-4">
					<div class="flex items-center gap-1">
						{#each [1, 2, 3, 4, 5] as q (q)}
							<div class="flex flex-1 flex-col items-center gap-1">
								<div
									class={cn(
										'flex size-8 items-center justify-center rounded-full text-xs font-semibold tabular-nums',
										q === school.quintile
											? 'bg-primary text-primary-foreground'
											: 'bg-muted text-muted-foreground'
									)}
								>
									{q}
								</div>
								<span class="text-[10px] text-muted-foreground">
									{q === 1 ? 'Poorest' : q === 5 ? 'Wealthiest' : ''}
								</span>
							</div>
						{/each}
					</div>
					<p class="text-sm text-muted-foreground">
						{#if school.quintile === null}
							No numeric quintile is published for this school{school.quintileCode
								? ` (masterlist value: ${school.quintileCode})`
								: ''}. Quintile 1 is the poorest and Quintile 5 the wealthiest.
						{:else}
							This is a <span class="font-medium text-foreground">Quintile {school.quintile}</span>
							school, which {quintileText[school.quintile] ?? 'is classified by relative poverty'}.
							Quintile 1 is the poorest and Quintile 5 the wealthiest.
						{/if}
					</p>
				</div>
			</section>

			<section class="space-y-4">
				<header class="space-y-1">
					<h2 class="flex items-center gap-2 text-base font-semibold">
						<Award class="size-4 text-primary" />
						Identification
					</h2>
				</header>
				<dl class="space-y-2.5 text-sm">
					<div class="flex items-baseline justify-between gap-4">
						<dt class="text-xs text-muted-foreground">EMIS number</dt>
						<dd class="font-medium tabular-nums">{school.id}</dd>
					</div>
					<div class="flex items-baseline justify-between gap-4">
						<dt class="text-xs text-muted-foreground">Centre number</dt>
						<dd class="font-medium tabular-nums">{school.centreNumber || 'n/a'}</dd>
					</div>
					<div class="flex items-baseline justify-between gap-4">
						<dt class="text-xs text-muted-foreground">District / circuit</dt>
						<dd class="text-right font-medium">{toTitleCase(school.circuit)}</dd>
					</div>
					<div class="flex items-baseline justify-between gap-4">
						<dt class="text-xs text-muted-foreground">Province</dt>
						<dd class="text-right font-medium">{toTitleCase(school.province)}</dd>
					</div>
					<div class="flex items-baseline justify-between gap-4">
						<dt class="text-xs text-muted-foreground">Quintile</dt>
						<dd class="font-medium tabular-nums">{school.quintile ?? 'n/a'}</dd>
					</div>
				</dl>
			</section>

			<section class="space-y-4">
				<header class="space-y-1">
					<h2 class="flex items-center gap-2 text-base font-semibold">
						<Mail class="size-4 text-primary" />
						Contact
					</h2>
					<p class="text-sm text-muted-foreground">From the DBE school masterlist.</p>
				</header>
				<dl class="space-y-2.5 text-sm">
					<div class="flex items-baseline justify-between gap-4">
						<dt class="text-xs text-muted-foreground">Email</dt>
						<dd class="min-w-0 text-right font-medium break-all">
							{#if school.email}
								<a
									class="underline-offset-4 hover:text-primary hover:underline"
									href={`mailto:${school.email}`}>{school.email}</a
								>
							{:else}
								<span class="text-muted-foreground">n/a</span>
							{/if}
						</dd>
					</div>
					<div class="flex items-baseline justify-between gap-4">
						<dt class="text-xs text-muted-foreground">Telephone</dt>
						<dd class="text-right font-medium tabular-nums">
							{#if school.telephone}
								<a
									class="underline-offset-4 hover:text-primary hover:underline"
									href={`tel:${school.telephone}`}>{formatPhone(school.telephone)}</a
								>
							{:else}
								<span class="text-muted-foreground">n/a</span>
							{/if}
						</dd>
					</div>
					{#if school.enrolment}
						<div class="flex items-baseline justify-between gap-4">
							<dt class="text-xs text-muted-foreground">Learners ({school.enrolment.year})</dt>
							<dd class="font-medium tabular-nums">
								{school.enrolment.learners?.toLocaleString() ?? 'n/a'}
							</dd>
						</div>
						<div class="flex items-baseline justify-between gap-4">
							<dt class="text-xs text-muted-foreground">Educators ({school.enrolment.year})</dt>
							<dd class="font-medium tabular-nums">
								{school.enrolment.educators?.toLocaleString() ?? 'n/a'}
							</dd>
						</div>
						{#if learnerEducatorRatio}
							<div class="flex items-baseline justify-between gap-4">
								<dt class="text-xs text-muted-foreground">Learners per educator</dt>
								<dd class="font-medium tabular-nums">{learnerEducatorRatio}</dd>
							</div>
						{/if}
					{/if}
				</dl>
			</section>

			<section class="space-y-4">
				<header class="space-y-1">
					<h2 class="flex items-center gap-2 text-base font-semibold">
						<ClipboardList class="size-4 text-primary" />
						School profile
					</h2>
					<p class="text-sm text-muted-foreground">
						Classification and ownership recorded by the department.
					</p>
				</header>
				<dl class="space-y-2.5 text-sm">
					<div class="flex items-baseline justify-between gap-4">
						<dt class="text-xs text-muted-foreground">Sector</dt>
						<dd class="text-right font-medium">
							{school.sector ? (sectorLabel[school.sector] ?? school.sector) : 'n/a'}
						</dd>
					</div>
					<div class="flex items-baseline justify-between gap-4">
						<dt class="text-xs text-muted-foreground">Phase</dt>
						<dd class="text-right font-medium">
							{school.phase ? (phaseLabel[school.phase] ?? school.phase) : 'n/a'}
						</dd>
					</div>
					<div class="flex items-baseline justify-between gap-4">
						<dt class="text-xs text-muted-foreground">Urban / rural</dt>
						<dd class="text-right font-medium">
							{school.urbanRural
								? (settlementLabel[school.urbanRural] ?? school.urbanRural)
								: 'n/a'}
						</dd>
					</div>
					<div class="flex items-baseline justify-between gap-4">
						<dt class="text-xs text-muted-foreground">Land ownership</dt>
						<dd class="text-right font-medium">
							{school.landOwnership
								? (ownershipLabel[school.landOwnership] ?? school.landOwnership)
								: 'n/a'}
						</dd>
					</div>
					<div class="flex items-baseline justify-between gap-4">
						<dt class="text-xs text-muted-foreground">Building ownership</dt>
						<dd class="text-right font-medium">
							{school.buildingOwnership
								? (ownershipLabel[school.buildingOwnership] ?? school.buildingOwnership)
								: 'n/a'}
						</dd>
					</div>
					<div class="flex items-baseline justify-between gap-4">
						<dt class="text-xs text-muted-foreground">Section 21 school</dt>
						<dd class="font-medium">{yesNo(school.isSection21)}</dd>
					</div>
					<div class="flex items-baseline justify-between gap-4">
						<dt class="text-xs text-muted-foreground">No-fee school</dt>
						<dd class="font-medium">{yesNo(school.isNoFeeSchool)}</dd>
					</div>
				</dl>
			</section>
		</div>
	{/if}
</div>
