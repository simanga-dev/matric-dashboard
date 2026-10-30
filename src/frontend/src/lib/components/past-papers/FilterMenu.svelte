<script lang="ts" module>
	export interface FilterOption {
		value: string;
		label: string;
	}
</script>

<script lang="ts">
	import * as DropdownMenu from '$lib/components/ui/dropdown-menu';
	import { cn } from '$lib/utils';
	import { Check, ChevronDown, SlidersHorizontal } from '@lucide/svelte';
	import type { IconProps } from '@lucide/svelte';
	import type { Component } from 'svelte';

	interface Props {
		label: string;
		value: string;
		options: FilterOption[];
		onChange: (value: string) => void;
		icon?: Component<IconProps>;
	}

	let { label, value, options, onChange, icon: Icon = SlidersHorizontal }: Props = $props();

	// The first option is always the "all" reset, so anything else is active.
	const active = $derived(value !== options[0]?.value);
	const selectedLabel = $derived(options.find((option) => option.value === value)?.label ?? label);
</script>

<DropdownMenu.Root>
	<DropdownMenu.Trigger>
		{#snippet child({ props })}
			<button
				type="button"
				aria-label={`Filter by ${label.toLowerCase()}`}
				class={cn(
					'inline-flex h-8 items-center gap-1.5 rounded-md border bg-background px-2.5 text-xs font-medium shadow-xs transition-colors hover:bg-accent',
					active && 'border-primary/40 bg-primary/5 text-primary'
				)}
				{...props}
			>
				<Icon class="size-3.5 opacity-70" />
				<span>{label}</span>
				{#if active}
					<span
						class="max-w-24 truncate rounded-sm bg-primary/10 px-1 py-0.5 text-[10px] font-semibold"
					>
						{selectedLabel}
					</span>
				{/if}
				<ChevronDown class="size-3 opacity-60" />
			</button>
		{/snippet}
	</DropdownMenu.Trigger>
	<DropdownMenu.Content align="start" class="w-52">
		<DropdownMenu.Label class="text-xs text-muted-foreground">{label}</DropdownMenu.Label>
		{#each options as option (option.value)}
			<DropdownMenu.Item class="gap-2 text-xs" onclick={() => onChange(option.value)}>
				<Check class={cn('size-3.5', option.value === value ? 'opacity-100' : 'opacity-0')} />
				{option.label}
			</DropdownMenu.Item>
		{/each}
	</DropdownMenu.Content>
</DropdownMenu.Root>
