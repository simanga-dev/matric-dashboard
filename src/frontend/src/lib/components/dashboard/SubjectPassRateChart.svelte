<script lang="ts">
	import * as Tooltip from '$lib/components/ui/tooltip';
	import { passRateColor } from './school-hierarchy';
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
	<span class="text-[10px] leading-none font-semibold text-muted-foreground tabular-nums">
		{subject.percentAchieved === null ? '–' : `${Math.round(subject.percentAchieved)}%`}
	</span>
	<div class="mt-1 flex h-48 w-full items-end overflow-hidden rounded-sm bg-muted/25">
		{#if subject.percentAchieved !== null}
			<div
				class="w-full rounded-t-sm motion-safe:transition-[height] motion-safe:duration-500 motion-safe:ease-out"
				style:height={`${barHeight(subject)}%`}
				style:background={passRateColor(subject.percentAchieved)}
			></div>
		{/if}
	</div>
	<span
		class="mt-1.5 h-24 w-full overflow-hidden text-[10px] leading-tight text-muted-foreground [writing-mode:vertical-rl]"
		title={subject.name}
	>
		{subject.name}
	</span>
{/snippet}

<div class={cn('w-full', className)}>
	<Tooltip.Provider delayDuration={150}>
		<div class="overflow-x-auto pb-1">
			<ul class="flex min-w-full items-stretch gap-1.5" aria-label="Subject pass rate">
				{#each ordered as subject (subject.name)}
					<li data-subject-bar class="flex min-w-10 flex-1 flex-col">
						<Tooltip.Root>
							<Tooltip.Trigger
								aria-label={rowTitle(subject)}
								class="group flex w-full flex-col items-center rounded-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
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
	</Tooltip.Provider>
</div>
