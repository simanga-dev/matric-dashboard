import type { Snippet } from 'svelte';

export interface ResizableContext {
	sizes: number[];
	direction: 'horizontal' | 'vertical';
	isResizing: boolean;
	registerPanel: (defaultSize?: number) => number;
	getPanelStyle: (index: number) => string;
	startResize: (panelIndex: number, event: PointerEvent, containerEl: HTMLElement) => void;
}

export const RESIZABLE_CTX_KEY = Symbol('resizable-ctx');

export { default as ResizablePanelGroup } from './resizable-panel-group.svelte';
export { default as ResizablePanel } from './resizable-panel.svelte';
export { default as ResizableHandle } from './resizable-handle.svelte';
