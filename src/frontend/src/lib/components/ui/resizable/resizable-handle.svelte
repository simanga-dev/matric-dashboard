<script lang="ts">
	import { getContext, type Snippet } from 'svelte';
	import { cn } from '$lib/utils';
	import { RESIZABLE_CTX_KEY, type ResizableContext } from './index.js';
	import { GripVertical, GripHorizontal } from '@lucide/svelte';

	interface Props {
		class?: string;
		children?: Snippet;
	}

	let { class: className, children }: Props = $props();

	let ctx = getContext<ResizableContext>(RESIZABLE_CTX_KEY);

	let ref: HTMLElement | null = $state(null);

	function handlePointerDown(event: PointerEvent) {
		if (!ref?.parentElement) return;
		event.preventDefault();
		const parent = ref.parentElement;
		const children = [...parent.children];
		const handleIndex = children.indexOf(ref);
		if (handleIndex < 1 || handleIndex >= children.length - 1) return;
		const panelIndex = Math.floor(handleIndex / 2);
		ctx.startResize(panelIndex, event, parent);
	}
</script>

<div
	bind:this={ref}
	onpointerdown={handlePointerDown}
	class={cn(
		'relative flex items-center justify-center bg-transparent transition-colors hover:bg-accent/50',
		ctx.direction === 'horizontal'
			? 'w-2 shrink-0 cursor-col-resize'
			: 'h-2 shrink-0 cursor-row-resize',
		className
	)}
	data-resizable-handle
	role="separator"
	tabindex="0"
>
	{#if children}
		{@render children()}
	{:else}
		<div
			class={cn(
				'z-10 flex items-center justify-center rounded-sm bg-border transition-colors group-hover:bg-accent-foreground/20',
				ctx.direction === 'horizontal' ? 'h-8 w-1' : 'h-1 w-8'
			)}
		>
			{#if ctx.direction === 'horizontal'}
				<GripVertical class="size-3 text-muted-foreground" />
			{:else}
				<GripHorizontal class="size-3 text-muted-foreground" />
			{/if}
		</div>
	{/if}
</div>
