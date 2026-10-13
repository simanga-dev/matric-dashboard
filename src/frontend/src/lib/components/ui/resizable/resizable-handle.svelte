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
		if (event.button !== 0) return;
		if (!ref?.parentElement) return;
		event.preventDefault();
		const parent = ref.parentElement;
		const children = [...parent.children];
		const handleIndex = children.indexOf(ref);
		if (handleIndex < 1 || handleIndex >= children.length - 1) return;
		const panelIndex = Math.floor(handleIndex / 2);
		ctx.startResize(panelIndex, event, parent);
	}

	function panelIndex() {
		return ref?.parentElement ? Math.floor([...ref.parentElement.children].indexOf(ref) / 2) : 0;
	}

	function handleKeyDown(event: KeyboardEvent) {
		const decrease = ctx.direction === 'horizontal' ? 'ArrowLeft' : 'ArrowUp';
		const increase = ctx.direction === 'horizontal' ? 'ArrowRight' : 'ArrowDown';
		if (event.key !== decrease && event.key !== increase) return;
		event.preventDefault();
		ctx.resizePanel(panelIndex(), (event.key === increase ? 1 : -1) * (event.shiftKey ? 5 : 2));
	}
</script>

<!-- The separator is a keyboard-operable splitter, with its size exposed below. -->
<!-- svelte-ignore a11y_no_noninteractive_tabindex, a11y_no_noninteractive_element_interactions -->
<div
	bind:this={ref}
	onpointerdown={handlePointerDown}
	onkeydown={handleKeyDown}
	class={cn(
		'relative flex touch-none items-center justify-center bg-transparent transition-colors outline-none hover:bg-accent/50 focus-visible:ring-2 focus-visible:ring-ring',
		ctx.direction === 'horizontal'
			? 'w-2 shrink-0 cursor-col-resize'
			: 'h-2 shrink-0 cursor-row-resize',
		className
	)}
	data-resizable-handle
	data-resizable-handle-index={panelIndex()}
	role="separator"
	aria-label="Resize adjacent panels"
	aria-orientation={ctx.direction === 'horizontal' ? 'vertical' : 'horizontal'}
	aria-valuemin={8}
	aria-valuemax={(ctx.sizes[panelIndex()] ?? 50) + (ctx.sizes[panelIndex() + 1] ?? 50) - 8}
	aria-valuenow={Math.round(ctx.sizes[panelIndex()] ?? 50)}
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
