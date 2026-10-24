<script lang="ts">
	import { cn } from '$lib/utils';

	/** Pill buttons for choosing one academic year from the years with data. */
	interface Props {
		years: number[];
		selected: number | null;
		onSelect: (year: number) => void;
		disabled?: boolean;
	}

	let { years, selected, onSelect, disabled = false }: Props = $props();
</script>

<div class="flex flex-wrap items-center gap-2" role="group" aria-label="Year">
	<span class="text-xs font-medium text-muted-foreground">Year</span>
	{#each years as year (year)}
		<button
			type="button"
			aria-pressed={selected === year}
			{disabled}
			onclick={() => onSelect(year)}
			class={cn(
				'rounded-full border px-3 py-1 text-xs font-medium tabular-nums transition-colors disabled:opacity-60',
				selected === year
					? 'border-transparent bg-primary text-primary-foreground'
					: 'bg-background hover:bg-muted'
			)}
		>
			{year}
		</button>
	{/each}
</div>
