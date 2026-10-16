<script lang="ts">
	import { TrendingUp } from '@lucide/svelte';
	import * as Tooltip from '$lib/components/ui/tooltip';

	/**
	 * A single subject row for the bar chart. `year` is optional and only shown
	 * when it differs from the headline year, so subjects last offered earlier
	 * stay legible.
	 */
	export interface SubjectBar {
		name: string;
		totalWrote: number;
		percentAchieved: number | null;
		year?: number;
	}

	let {
		subjects,
		headlineYear = null,
		class: className = ''
	}: {
		subjects: SubjectBar[];
		headlineYear?: number | null;
		class?: string;
	} = $props();

	let ordered = $derived(
		[...subjects].sort((a, b) => {
			const left = a.totalWrote;
			const right = b.totalWrote;
			if (right !== left) return right - left;
			return a.name.localeCompare(b.name);
		})
	);

	let maxWrote = $derived(Math.max(1, ...subjects.map((s) => s.totalWrote)));

	function barWidth(subject: SubjectBar): number {
		return (subject.totalWrote / maxWrote) * 100;
	}

	// Counts are estimated because the API supplies a rounded pass percentage.
	function passedCount(subject: SubjectBar): number {
		return Math.round(
			(subject.totalWrote * Math.min(100, Math.max(0, subject.percentAchieved ?? 0))) / 100
		);
	}

	function rowTitle(subject: SubjectBar): string {
		const rate =
			subject.percentAchieved === null ? '' : `, ${subject.percentAchieved.toFixed(1)}% pass`;
		const year = subject.year && subject.year !== headlineYear ? ` (${subject.year})` : '';
		return `${subject.name}${year}: ${subject.totalWrote.toLocaleString()} wrote${rate}`;
	}
</script>

<div class={className}>
	<div
		class="mb-3 flex flex-wrap gap-x-4 gap-y-1 text-[11px] text-muted-foreground"
		aria-label="Chart legend"
	>
		<span class="flex items-center gap-1.5"
			><span class="h-2 w-3 rounded-sm bg-emerald-500" aria-hidden="true"></span>Passed</span
		>
		<span class="flex items-center gap-1.5"
			><span class="h-2 w-3 rounded-sm bg-rose-500" aria-hidden="true"></span>Not passed</span
		>
		{#if subjects.some((subject) => subject.percentAchieved === null)}
			<span class="flex items-center gap-1.5"
				><span class="h-2 w-3 rounded-sm bg-muted-foreground" aria-hidden="true"></span>Results
				unavailable</span
			>
		{/if}
	</div>
	<Tooltip.Provider delayDuration={150}>
		<ul class="space-y-2" aria-label="Subject performance">
			{#each ordered as subject (subject.name)}
				<li data-subject-bar>
					<Tooltip.Root>
						<Tooltip.Trigger
							aria-label={rowTitle(subject)}
							class="group flex w-full items-center gap-3 rounded-md text-start outline-none focus-visible:ring-2 focus-visible:ring-ring"
						>
							<div class="relative flex min-h-9 min-w-0 flex-1 items-center rounded-md bg-muted/25">
								{#each [25, 50, 75, 100] as grid (grid)}
									<span
										class="pointer-events-none absolute inset-y-0 border-s border-border/40"
										style="inset-inline-start: {grid}%"
										aria-hidden="true"
									></span>
								{/each}
								<div
									class="pointer-events-none absolute inset-y-0 start-0 flex overflow-hidden rounded-md opacity-30 transition-[width] duration-500 ease-out group-hover:opacity-45 group-focus-visible:opacity-45 motion-reduce:transition-none"
									style:width={`${Math.min(100, Math.max(0, barWidth(subject)))}%`}
									aria-hidden="true"
								>
									{#if subject.percentAchieved !== null}
										<span
											class="h-full bg-emerald-500"
											style:width={`${subject.totalWrote > 0 ? (passedCount(subject) / subject.totalWrote) * 100 : 0}%`}
										></span>
										<span class="h-full flex-1 bg-rose-500"></span>
									{:else}
										<span class="h-full w-full bg-muted-foreground"></span>
									{/if}
								</div>
								<span
									class="relative flex items-center gap-2 px-3 py-2 text-xs leading-snug font-medium"
								>
									{subject.name}
								</span>
							</div>
							<span class="w-12 shrink-0 text-end">
								<span class="block text-xs font-semibold tabular-nums">
									{subject.totalWrote.toLocaleString()}
								</span>
								{#if subject.year && subject.year !== headlineYear}
									<span class="block text-[10px] text-muted-foreground tabular-nums"
										>{subject.year}</span
									>
								{/if}
							</span>
						</Tooltip.Trigger>
						<Tooltip.Content
							side="top"
							sideOffset={8}
							class="max-w-72 border border-border bg-popover px-3 py-2.5 text-popover-foreground shadow-lg"
						>
							<p class="mb-2 text-xs font-semibold">
								{subject.name}{subject.year ? ` · ${subject.year}` : ''}
							</p>
							<dl class="grid grid-cols-[1fr_auto] gap-x-6 gap-y-1 text-xs">
								<dt class="opacity-70">Learners who wrote</dt>
								<dd class="text-end font-medium tabular-nums">
									{subject.totalWrote.toLocaleString()}
								</dd>
								<dt class="opacity-70">Pass rate</dt>
								<dd class="text-end font-medium tabular-nums">
									{subject.percentAchieved === null
										? 'Unavailable'
										: `${subject.percentAchieved.toFixed(1)}%`}
								</dd>
								{#if subject.percentAchieved !== null}
									<dt class="opacity-70">Passed (approx.)</dt>
									<dd class="text-end font-medium tabular-nums">
										{passedCount(subject).toLocaleString()}
									</dd>
									<dt class="opacity-70">Not passed (approx.)</dt>
									<dd class="text-end font-medium tabular-nums">
										{(subject.totalWrote - passedCount(subject)).toLocaleString()}
									</dd>
								{/if}
							</dl>
						</Tooltip.Content>
					</Tooltip.Root>
				</li>
			{/each}
		</ul>
	</Tooltip.Provider>

	<div class="mt-3 flex items-start gap-2 border-t pt-2.5">
		<TrendingUp class="mt-0.5 size-3.5 shrink-0 text-muted-foreground" />
		<div class="text-xs">
			<p class="font-medium">
				{subjects.reduce((total, s) => total + s.totalWrote, 0).toLocaleString()} subject entries
			</p>
			<p class="text-muted-foreground">
				Across {subjects.length} subjects{headlineYear ? ` in ${headlineYear}` : ''}
			</p>
			<p class="mt-1 text-muted-foreground">Pass counts estimated from reported pass rates.</p>
		</div>
	</div>
</div>
