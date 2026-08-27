<script lang="ts">
	import { onMount } from 'svelte';
	import { page } from '$app/state';
	import { resolve } from '$app/paths';
	import { goto } from '$app/navigation';
	import { PageHeader, LoadingSpinner, EmptyState } from '$lib/components/common';
	import * as Card from '$lib/components/ui/card';
	import { Badge } from '$lib/components/ui/badge';
	import { Button } from '$lib/components/ui/button';
	import { fetchSchoolById } from '$lib/api/dashboard';
	import { routes } from '$lib/config';
	import type { School } from '$lib/types/dashboard';
	import {
		School as SchoolIcon,
		Users,
		GraduationCap,
		Award,
		MapPin,
		TrendingUp,
		ArrowLeft,
		Building2
	} from '@lucide/svelte';

	let school = $state<School | null>(null);
	let loading = $state(true);
	let error = $state<string | null>(null);

	let schoolId = $derived(page.params.id ?? '');

	onMount(async () => {
		if (!schoolId) {
			error = 'Invalid school.';
			loading = false;
			return;
		}
		try {
			school = await fetchSchoolById(schoolId);
		} catch (e) {
			error = e instanceof Error ? e.message : 'Failed to load school.';
		} finally {
			loading = false;
		}
	});

	function formatPassRate(rate: number): string {
		return `${rate.toFixed(1)}%`;
	}

	function passRateColor(rate: number): string {
		if (rate >= 80) return 'text-green-600';
		if (rate >= 60) return 'text-amber-600';
		return 'text-red-600';
	}

	function passRateBar(rate: number): string {
		if (rate >= 80) return 'bg-green-500';
		if (rate >= 60) return 'bg-amber-500';
		return 'bg-red-500';
	}

	let passRatePct = $derived(school ? Math.round(school.passRate * 10) / 10 : 0);
	let failedCount = $derived(school ? school.totalWrote - school.totalPassed : 0);
	let bachelorPct = $derived(
		school && school.totalAchieved !== null && school.totalWrote > 0
			? Math.round((school.totalAchieved / school.totalWrote) * 1000) / 10
			: null
	);
</script>

<svelte:head>
	<title>{school ? school.name : 'School'} • MatricDashboard</title>
	<meta
		name="description"
		content={school
			? `${school.name} - ${school.province} matric performance`
			: 'School matric performance details'}
	/>
</svelte:head>

<div class="space-y-6">
	<div class="flex items-center gap-2">
		<Button variant="ghost" size="sm" class="gap-1" onclick={() => goto(resolve(routes.overview))}>
			<ArrowLeft class="size-4" />
			Back to overview
		</Button>
	</div>

	{#if loading}
		<div class="flex justify-center py-24">
			<LoadingSpinner />
		</div>
	{:else if error || !school}
		<EmptyState icon={SchoolIcon} message={error ?? 'School not found.'} />
	{:else}
		<PageHeader title={school.name} description={`${school.province} • ${school.circuit}`} />

		<div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
			<Card.Root>
				<Card.Header class="pb-2">
					<Card.Description class="flex items-center gap-1.5 text-xs">
						<Users class="size-3.5" />
						Wrote
					</Card.Description>
				</Card.Header>
				<Card.Content>
					<div class="text-2xl font-semibold tabular-nums">
						{school.totalWrote.toLocaleString()}
					</div>
				</Card.Content>
			</Card.Root>
			<Card.Root>
				<Card.Header class="pb-2">
					<Card.Description class="flex items-center gap-1.5 text-xs">
						<GraduationCap class="size-3.5" />
						Passed
					</Card.Description>
				</Card.Header>
				<Card.Content>
					<div class="text-2xl font-semibold tabular-nums">
						{school.totalPassed.toLocaleString()}
					</div>
				</Card.Content>
			</Card.Root>
			<Card.Root>
				<Card.Header class="pb-2">
					<Card.Description class="flex items-center gap-1.5 text-xs">
						<Award class="size-3.5" />
						Bachelor passes
					</Card.Description>
				</Card.Header>
				<Card.Content>
					<div class="text-2xl font-semibold tabular-nums">
						{school.totalAchieved !== null ? school.totalAchieved.toLocaleString() : '-'}
					</div>
				</Card.Content>
			</Card.Root>
			<Card.Root>
				<Card.Header class="pb-2">
					<Card.Description class="flex items-center gap-1.5 text-xs">
						<TrendingUp class="size-3.5" />
						Pass rate
					</Card.Description>
				</Card.Header>
				<Card.Content>
					<div class="text-2xl font-semibold tabular-nums {passRateColor(school.passRate)}">
						{formatPassRate(school.passRate)}
					</div>
				</Card.Content>
			</Card.Root>
		</div>

		<Card.Root>
			<Card.Header>
				<Card.Title class="flex items-center justify-between gap-2">
					<span class="flex items-center gap-2">
						<SchoolIcon class="size-4" />
						Overview
					</span>
					<Badge variant="secondary" class={passRateColor(school.passRate)}>
						{formatPassRate(school.passRate)}
					</Badge>
				</Card.Title>
			</Card.Header>
			<Card.Content class="space-y-6">
				<div class="grid gap-4 sm:grid-cols-3">
					<div class="rounded-lg border bg-muted/30 p-4">
						<div class="flex items-center gap-1.5 text-xs text-muted-foreground">
							<MapPin class="size-3.5" />
							Province
						</div>
						<div class="mt-1 font-medium">{school.province}</div>
					</div>
					<div class="rounded-lg border bg-muted/30 p-4">
						<div class="flex items-center gap-1.5 text-xs text-muted-foreground">
							<Building2 class="size-3.5" />
							Circuit
						</div>
						<div class="mt-1 font-medium">{school.circuit}</div>
					</div>
					<div class="rounded-lg border bg-muted/30 p-4">
						<div class="text-xs text-muted-foreground">School ID</div>
						<div class="mt-1 font-medium tabular-nums">{school.id}</div>
					</div>
				</div>

				<div>
					<div class="mb-2 flex items-center justify-between text-sm">
						<span class="font-medium">Pass rate</span>
						<span class="tabular-nums {passRateColor(school.passRate)}">
							{formatPassRate(passRatePct)}
						</span>
					</div>
					<div class="h-2.5 w-full rounded-full bg-muted">
						<div
							class="h-full rounded-full {passRateBar(school.passRate)}"
							style="width: {Math.min(100, school.passRate)}%"
						></div>
					</div>
				</div>

				<div class="grid gap-4 sm:grid-cols-3">
					<div>
						<div class="text-xs text-muted-foreground">Wrote</div>
						<div class="mt-1 text-lg font-semibold tabular-nums">
							{school.totalWrote.toLocaleString()}
						</div>
					</div>
					<div>
						<div class="text-xs text-muted-foreground">Passed</div>
						<div class="mt-1 text-lg font-semibold text-green-600 tabular-nums">
							{school.totalPassed.toLocaleString()}
						</div>
					</div>
					<div>
						<div class="text-xs text-muted-foreground">Did not pass</div>
						<div class="mt-1 text-lg font-semibold text-red-600 tabular-nums">
							{failedCount.toLocaleString()}
						</div>
					</div>
				</div>

				{#if bachelorPct !== null}
					<div>
						<div class="mb-2 flex items-center justify-between text-sm">
							<span class="font-medium">Bachelor eligibility rate</span>
							<span class="tabular-nums">{formatPassRate(bachelorPct)}</span>
						</div>
						<div class="h-2.5 w-full rounded-full bg-muted">
							<div
								class="h-full rounded-full bg-primary"
								style="width: {Math.min(100, bachelorPct)}%"
							></div>
						</div>
						<p class="mt-2 text-xs text-muted-foreground">
							{school.totalAchieved?.toLocaleString() ?? 0} of {school.totalWrote.toLocaleString()} learners
							qualified for bachelor admission.
						</p>
					</div>
				{/if}
			</Card.Content>
		</Card.Root>
	{/if}
</div>
