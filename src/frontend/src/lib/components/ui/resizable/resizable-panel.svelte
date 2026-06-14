<script lang="ts">
	import { getContext, type Snippet } from 'svelte';
	import { RESIZABLE_CTX_KEY, type ResizableContext } from './index.js';

	interface Props {
		children: Snippet;
		defaultSize?: number;
	}

	let { children, defaultSize }: Props = $props();

	let ctx = getContext<ResizableContext>(RESIZABLE_CTX_KEY);

	let index = $state(ctx.registerPanel(defaultSize));
	let style = $derived(ctx.getPanelStyle(index));
</script>

<div {style} data-resizable-panel-index={index}>
	{@render children()}
</div>
