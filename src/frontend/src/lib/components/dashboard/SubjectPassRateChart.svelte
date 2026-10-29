<script lang="ts">
	import * as Tooltip from '$lib/components/ui/tooltip';
	import { cn } from '$lib/utils';
	import type { SubjectBar } from './subject-bar';

	interface Props {
		subjects: SubjectBar[];
		headlineYear?: number | null;
		class?: string;
	}

	let { subjects, headlineYear = null, class: className = '' }: Props = $props();

	// Highest pass rate first; subjects without a reported rate sink to the end.
	let ordered = $derived(
		[...subjects].sort((a, b) => {
			const left = a.percentAchieved ?? -1;
			const right = b.percentAchieved ?? -1;
			if (right !== left) return right - left;
			return a.name.localeCompare(b.name);
		})
	);

	const ticks = [0, 25, 50, 75, 100];

	// Entry-weighted average, so large subjects count for more than niche ones.
	let average = $derived.by(() => {
		let wrote = 0;
		let passed = 0;
		for (const subject of subjects) {
			if (subject.percentAchieved === null) continue;
			wrote += subject.totalWrote;
			passed += subject.totalWrote * subject.percentAchieved;
		}
		return wrote > 0 ? passed / wrote : null;
	});

	function isBelowAverage(subject: SubjectBar): boolean {
		return (
			average !== null && subject.percentAchieved !== null && subject.percentAchieved < average
		);
	}

	function barHeight(subject: SubjectBar): number {
		if (subject.percentAchieved === null) return 0;
		return Math.min(100, Math.max(0, subject.percentAchieved));
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

{#snippet column(subject: SubjectBar)}
	{@const below = isBelowAverage(subject)}
	<div class="relative flex h-56 w-full items-end">
		{#if subject.percentAchieved !== null}
			<div
				class={cn(
					'relative w-full rounded-t-md group-hover:opacity-90 motion-safe:transition-[height,opacity] motion-safe:duration-500 motion-safe:ease-out',
					below ? 'bg-amber-500' : 'bg-muted-foreground/35'
				)}
				style:height={`${barHeight(subject)}%`}
			>
				<span
					class={cn(
						'absolute inset-x-0 -top-5 text-center text-xs font-semibold tabular-nums',
						below ? 'text-amber-500' : 'text-foreground'
					)}
				>
					{subject.percentAchieved.toFixed(1)}%
				</span>
			</div>
		{:else}
			<span class="w-full text-center text-xs text-muted-foreground">–</span>
		{/if}
	</div>
	<span
		class="mt-2 line-clamp-2 h-8 w-full text-center text-[11px] leading-tight text-muted-foreground group-hover:text-foreground"
		title={subject.name}
	>
		{subject.name}
	</span>
{/snippet}

<div class={cn('w-full', className)}>
	{#if average !== null}
		<div class="mb-3 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-muted-foreground">
			<span class="flex items-center gap-1.5">
				<span class="h-0 w-4 border-t-2 border-dashed border-foreground/60"></span>
				Average {average.toFixed(1)}%
			</span>
			<span class="flex items-center gap-1.5">
				<span class="size-2.5 rounded-sm bg-amber-500"></span>
				Below average
			</span>
		</div>
	{/if}
	<Tooltip.Provider delayDuration={150}>
		<div class="flex">
			<!-- Y axis: aligned with the 14rem (h-56) plot area below the 1.5rem headroom. -->
			<div class="relative mt-6 h-56 w-9 shrink-0" aria-hidden="true">
				{#each ticks as tick (tick)}
					<span
						class="absolute right-2 translate-y-1/2 text-[10px] text-muted-foreground tabular-nums"
						style:bottom={`${tick}%`}>{tick}%</span
					>
				{/each}
			</div>
			<div class="relative min-w-0 flex-1 overflow-x-auto pb-1">
				<div class="pointer-events-none absolute inset-x-0 top-6 h-56" aria-hidden="true">
					{#each ticks as tick (tick)}
						<div
							class="absolute inset-x-0 border-t border-border/60"
							style:bottom={`${tick}%`}
						></div>
					{/each}
					{#if average !== null}
						<div
							class="absolute inset-x-0 z-10 border-t-2 border-dashed border-foreground/60"
							style:bottom={`${average}%`}
						></div>
					{/if}
				</div>
				<ul
					class="relative flex min-w-full items-stretch gap-2 pt-6"
					aria-label="Subject pass rate"
				>
					{#each ordered as subject (subject.name)}
						<li data-subject-bar class="flex min-w-16 flex-1 flex-col">
							<Tooltip.Root>
								<Tooltip.Trigger
									aria-label={rowTitle(subject)}
									class="group flex w-full flex-col items-center rounded-md outline-none focus-visible:ring-2 focus-visible:ring-ring"
								>
									{#snippet child({ props })}
										{#if subject.href}
											<!-- eslint-disable-next-line svelte/no-navigation-without-resolve -- callers pass resolved hrefs -->
											<a {...props} href={subject.href}>{@render column(subject)}</a>
										{:else}
											<button {...props} type="button">{@render column(subject)}</button>
										{/if}
									{/snippet}
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
			</div>
		</div>
	</Tooltip.Provider>
</div>
