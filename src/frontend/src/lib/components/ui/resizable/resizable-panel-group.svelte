<script lang="ts">
	import { setContext, type Snippet } from 'svelte';
	import { cn } from '$lib/utils';
	import { RESIZABLE_CTX_KEY, type ResizableContext } from './index.js';

	interface Props {
		children: Snippet;
		orientation?: 'horizontal' | 'vertical';
		class?: string;
	}

	let { children, orientation = 'horizontal', class: className }: Props = $props();

	let sizes = $state<number[]>([]);
	let isResizing = $state(false);
	let panelCount = $state(0);

	let dragState: {
		panelIndex: number;
		nextPanelIndex: number;
		startPointer: number;
		startSize: number;
		startNextSize: number;
		totalSize: number;
	} | null = $state(null);

	function registerPanel(defaultSize?: number): number {
		const index = panelCount;
		panelCount++;
		if (defaultSize !== undefined) {
			sizes[index] = defaultSize;
		}
		return index;
	}

	$effect(() => {
		const count = panelCount;
		if (count === 0) return;
		const current = sizes.slice(0, count);
		const defined = current.filter((s) => s > 0);
		if (defined.length === count) {
			const total = defined.reduce((a, b) => a + b, 0);
			if (Math.abs(total - 100) > 0.1) {
				const adjusted = defined.map((s) => (s / total) * 100);
				for (let i = 0; i < count; i++) {
					sizes[i] = Math.round((adjusted[i] ?? 0) * 10) / 10;
				}
				sizes = sizes;
			}
			return;
		}
		const remaining = 100 - defined.reduce((a, b) => a + b, 0);
		const undefinedCount = count - defined.length;
		if (undefinedCount > 0) {
			const each = Math.floor((remaining / undefinedCount) * 10) / 10;
			for (let i = 0; i < count; i++) {
				if (!current[i] || (current[i] ?? 0) <= 0) {
					sizes[i] = each;
				}
			}
			const sum = sizes.slice(0, count).reduce((a, b) => a + b, 0);
			const diff = Math.round((100 - sum) * 10) / 10;
			if (Math.abs(diff) > 0 && undefinedCount > 0) {
				for (let i = count - 1; i >= 0; i--) {
					if (!current[i] || (current[i] ?? 0) <= 0) {
						sizes[i] = Math.round(((sizes[i] ?? 0) + diff) * 10) / 10;
						break;
					}
				}
			}
			sizes = sizes;
		}
	});

	function getPanelStyle(index: number): string {
		if (index >= panelCount || index >= sizes.length) return '';
		return `flex: 0 0 ${sizes[index]!}%; overflow: hidden; min-width: 0;`;
	}

	function startResize(panelIndex: number, event: PointerEvent, containerEl: HTMLElement) {
		const rect = containerEl.getBoundingClientRect();
		const totalSize = orientation === 'horizontal' ? rect.width : rect.height;

		const startSize = sizes[panelIndex] ?? 50;
		const startNextSize = sizes[panelIndex + 1] ?? 50;

		dragState = {
			panelIndex,
			nextPanelIndex: panelIndex + 1,
			startPointer: orientation === 'horizontal' ? event.clientX : event.clientY,
			startSize,
			startNextSize,
			totalSize
		};
	}

	function handlePointerMove(event: PointerEvent) {
		if (!dragState) return;
		isResizing = true;
		event.preventDefault();

		const currentPointer = orientation === 'horizontal' ? event.clientX : event.clientY;
		const delta = currentPointer - dragState.startPointer;
		const deltaPercent = (delta / dragState.totalSize) * 100;

		const newSize = Math.max(8, Math.min(92, dragState.startSize + deltaPercent));
		const newNextSize = Math.max(8, Math.min(92, dragState.startNextSize - deltaPercent));
		const total = newSize + newNextSize;

		sizes[dragState.panelIndex] = Math.round((newSize / total) * 1000) / 10;
		sizes[dragState.nextPanelIndex] = Math.round((newNextSize / total) * 1000) / 10;
		sizes = sizes;
	}

	function handlePointerUp() {
		dragState = null;
		isResizing = false;
	}

	const context: ResizableContext = {
		get sizes() {
			return sizes;
		},
		get direction() {
			return orientation;
		},
		get isResizing() {
			return isResizing;
		},
		registerPanel,
		getPanelStyle,
		startResize
	};

	setContext(RESIZABLE_CTX_KEY, context);
</script>

<svelte:window
	onpointermove={isResizing ? handlePointerMove : undefined}
	onpointerup={isResizing ? handlePointerUp : undefined}
/>

<div
	class={cn(
		'flex',
		orientation === 'horizontal' ? 'flex-row' : 'flex-col',
		'h-full w-full',
		isResizing && 'select-none',
		className
	)}
	data-orientation={orientation}
	role="group"
>
	{@render children()}
</div>
