<script lang="ts">
	import InfoTooltip from '$lib/components/common/InfoTooltip.svelte';
	import { metricHelp } from '$lib/components/common/metric-help';
	import { resolve } from '$app/paths';
	import { goto } from '$app/navigation';
	import { EmptyState } from '$lib/components/common';
	import * as Card from '$lib/components/ui/card';
	import { Badge } from '$lib/components/ui/badge';
	import { Button } from '$lib/components/ui/button';
	import { toast } from '$lib/components/ui/sonner';
	import { passRateColor } from '$lib/components/dashboard/school-hierarchy';
	import { routes } from '$lib/config';
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
		Award
	} from '@lucide/svelte';

	let { data }: { data: PageData } = $props();

	// The server load resolves the school for the current route param, so the
	// page renders on first paint and stays correct across param changes.
	let school = $derived(data.school);

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

	let schoolName = $derived(school ? toTitleCase(school.name) : '');
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

<div class="space-y-6">
	<div>
		<Button variant="ghost" size="sm" class="gap-1" onclick={() => goto(resolve(routes.overview))}>
			<ArrowLeft class="size-4" />
			Back to overview
		</Button>
	</div>

	{#if !school}
		<EmptyState icon={SchoolIcon} message="School not found." />
	{:else}
		<Card.Root class="overflow-hidden bg-gradient-to-br from-primary/10 via-card to-card">
			<Card.Content class="flex flex-col gap-6 p-6 sm:flex-row sm:items-center sm:justify-between">
				<div class="flex items-start gap-4">
					<div
						class="flex size-14 shrink-0 items-center justify-center rounded-2xl bg-primary/10 text-primary"
					>
						<SchoolIcon class="size-7" />
					</div>
					<div class="min-w-0 space-y-1.5">
						<h1 class="text-xl font-semibold tracking-tight text-balance sm:text-2xl">
							{schoolName}
						</h1>
						<div class="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-muted-foreground">
							<span class="inline-flex items-center gap-1">
								<MapPin class="size-3.5" />
								{toTitleCase(school.province)}
							</span>
							<span class="inline-flex items-center gap-1">
								<Building2 class="size-3.5" />
								{toTitleCase(school.circuit)}
							</span>
						</div>
						<div class="flex flex-wrap gap-1.5 pt-1">
							<Badge variant="outline" class="tabular-nums"
								>EMIS {school.id}<InfoTooltip label="EMIS number" text={metricHelp.emis} /></Badge
							>
							{#if school.centreNumber}
								<Badge variant="outline" class="tabular-nums"
									>Centre {school.centreNumber}<InfoTooltip
										label="examination centre"
										text={metricHelp.centre}
									/></Badge
								>
							{/if}
							<Badge variant="secondary"
								>Quintile {school.quintile}<InfoTooltip
									label="quintile"
									text={metricHelp.quintile}
								/></Badge
							>
							{#if latest}
								<Badge variant="outline" class="gap-1">
									<Calendar class="size-3" />
									NSC {latest.year}
								</Badge>
							{/if}
						</div>
					</div>
				</div>
				<div class="flex items-end gap-6">
					<div class="text-right">
						<p class="text-xs text-muted-foreground">
							Pass rate<InfoTooltip label="NSC pass rate" text={metricHelp.passRate} />
						</p>
						<p
							class="text-3xl font-semibold tracking-tight tabular-nums"
							style:color={passRateColor(school.passRate)}
						>
							{formatPassRate(school.passRate)}
						</p>
						{#if yoy !== null}
							<p
								class={cn(
									'mt-0.5 inline-flex items-center gap-1 text-xs font-medium',
									yoy >= 0 ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'
								)}
							>
								{#if yoy >= 0}<TrendingUp class="size-3.5" />{:else}<TrendingDown
										class="size-3.5"
									/>{/if}
								{Math.abs(yoy).toFixed(1)} pts YoY<InfoTooltip
									label="year-on-year change"
									text={metricHelp.points}
								/>
							</p>
						{/if}
					</div>
					<div class="flex flex-col gap-2">
						<Button variant="outline" size="sm" class="gap-1.5" onclick={viewOnMap}>
							<MapPinned class="size-4" />
							View on map
						</Button>
						<Button variant="ghost" size="sm" class="gap-1.5" onclick={copyLink}>
							<Copy class="size-4" />
							Copy link
						</Button>
					</div>
				</div>
			</Card.Content>
		</Card.Root>

		<div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
			<Card.Root>
				<Card.Content class="flex items-center gap-4 p-4">
					<div
						class="flex size-10 shrink-0 items-center justify-center rounded-lg bg-muted text-muted-foreground"
					>
						<Users class="size-5" />
					</div>
					<div class="min-w-0">
						<p class="text-2xl font-semibold tabular-nums">
							{school.totalWrote.toLocaleString()}
						</p>
						<p class="truncate text-xs text-muted-foreground">
							Learners wrote<InfoTooltip label="learners who wrote" text={metricHelp.wrote} />
						</p>
					</div>
				</Card.Content>
			</Card.Root>
			<Card.Root>
				<Card.Content class="flex items-center gap-4 p-4">
					<div
						class="flex size-10 shrink-0 items-center justify-center rounded-lg bg-green-500/10 text-green-600 dark:text-green-400"
					>
						<GraduationCap class="size-5" />
					</div>
					<div class="min-w-0">
						<p class="text-2xl font-semibold tabular-nums">
							{school.totalPassed.toLocaleString()}
						</p>
						<p class="truncate text-xs text-muted-foreground">Passed</p>
					</div>
				</Card.Content>
			</Card.Root>
			<Card.Root>
				<Card.Content class="flex items-center gap-4 p-4">
					<div
						class="flex size-10 shrink-0 items-center justify-center rounded-lg bg-red-500/10 text-red-600 dark:text-red-400"
					>
						<XCircle class="size-5" />
					</div>
					<div class="min-w-0">
						<p class="text-2xl font-semibold tabular-nums">{failedCount.toLocaleString()}</p>
						<p class="truncate text-xs text-muted-foreground">Did not pass</p>
					</div>
				</Card.Content>
			</Card.Root>
			<Card.Root>
				<Card.Content class="flex items-center gap-4 p-4">
					<div
						class="flex size-10 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary"
					>
						<Percent class="size-5" />
					</div>
					<div class="min-w-0">
						<p
							class="text-2xl font-semibold tabular-nums"
							style:color={passRateColor(school.passRate)}
						>
							{formatPassRate(school.passRate)}
						</p>
						<p class="truncate text-xs text-muted-foreground">
							Pass rate<InfoTooltip label="NSC pass rate" text={metricHelp.passRate} />
						</p>
					</div>
				</Card.Content>
			</Card.Root>
		</div>

		<div class="grid gap-4 lg:grid-cols-2">
			<Card.Root>
				<Card.Header>
					<Card.Title class="flex items-center gap-2 text-base">
						<TrendingUp class="size-4 text-primary" />
						Performance over time
					</Card.Title>
					<Card.Description>Pass rate by academic year.</Card.Description>
				</Card.Header>
				<Card.Content class="space-y-4">
					{#if school.years.length === 0}
						<p class="text-sm text-muted-foreground">
							No historical performance is recorded for this school yet.
						</p>
					{:else}
						<div class="flex items-end gap-3">
							{#each school.years as year (year.year)}
								{@const color = passRateColor(year.passRate)}
								<div class="flex flex-1 flex-col items-center gap-2">
									<span class="text-xs font-semibold tabular-nums" style:color>
										{year.passRate.toFixed(1)}%
									</span>
									<div class="flex h-28 w-full items-end overflow-hidden rounded-md bg-muted/50">
										<div
											class="w-full rounded-md"
											style:background={color}
											style:height={`${Math.max(2, Math.min(100, year.passRate))}%`}
										></div>
									</div>
									<span class="text-xs text-muted-foreground tabular-nums">{year.year}</span>
								</div>
							{/each}
						</div>
						{#if latest}
							<p class="text-xs text-muted-foreground">
								{latest.progressed.toLocaleString()} progressed learners wrote in {latest.year} of
								{latest.totalWrote.toLocaleString()} total.
							</p>
						{/if}
					{/if}
				</Card.Content>
			</Card.Root>

			<Card.Root>
				<Card.Header>
					<Card.Title class="flex items-center gap-2 text-base">
						<Scale class="size-4 text-primary" />
						How it compares
					</Card.Title>
					<Card.Description>
						Against {toTitleCase(school.province)} and the country ({school.comparison.year ??
							latest?.year ??
							'latest year'}).
					</Card.Description>
				</Card.Header>
				<Card.Content class="space-y-4">
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
				</Card.Content>
			</Card.Root>
		</div>

		<div class="grid gap-4 lg:grid-cols-2">
			<Card.Root>
				<Card.Header>
					<Card.Title class="flex items-center gap-2 text-base">
						<Info class="size-4 text-primary" />
						Quintile context
					</Card.Title>
					<Card.Description>
						Socio-economic classification used for school funding.
					</Card.Description>
				</Card.Header>
				<Card.Content class="space-y-4">
					<div class="flex items-center gap-1">
						{#each [1, 2, 3, 4, 5] as q (q)}
							<div class="flex flex-1 flex-col items-center gap-1">
								<div
									class={cn(
										'flex size-8 items-center justify-center rounded-full border text-xs font-semibold tabular-nums',
										q === school.quintile
											? 'border-transparent bg-primary text-primary-foreground'
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
						This is a <span class="font-medium text-foreground">Quintile {school.quintile}</span>
						school, which {quintileText[school.quintile] ?? 'is classified by relative poverty'}.
						Quintile 1 is the poorest and Quintile 5 the wealthiest.
					</p>
				</Card.Content>
			</Card.Root>

			<Card.Root>
				<Card.Header>
					<Card.Title class="flex items-center gap-2 text-base">
						<Award class="size-4 text-primary" />
						Identification
					</Card.Title>
				</Card.Header>
				<Card.Content>
					<dl class="space-y-3 text-sm">
						<div class="flex items-baseline justify-between gap-4 border-b pb-3">
							<dt class="text-xs text-muted-foreground">EMIS number</dt>
							<dd class="font-medium tabular-nums">{school.id}</dd>
						</div>
						<div class="flex items-baseline justify-between gap-4 border-b pb-3">
							<dt class="text-xs text-muted-foreground">Centre number</dt>
							<dd class="font-medium tabular-nums">{school.centreNumber || 'n/a'}</dd>
						</div>
						<div class="flex items-baseline justify-between gap-4 border-b pb-3">
							<dt class="text-xs text-muted-foreground">District / circuit</dt>
							<dd class="text-right font-medium">{toTitleCase(school.circuit)}</dd>
						</div>
						<div class="flex items-baseline justify-between gap-4 border-b pb-3">
							<dt class="text-xs text-muted-foreground">Province</dt>
							<dd class="text-right font-medium">{toTitleCase(school.province)}</dd>
						</div>
						<div class="flex items-baseline justify-between gap-4">
							<dt class="text-xs text-muted-foreground">Quintile</dt>
							<dd class="font-medium tabular-nums">{school.quintile}</dd>
						</div>
					</dl>
				</Card.Content>
			</Card.Root>
		</div>
	{/if}
</div>
