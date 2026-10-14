<script lang="ts">
	import { getContext, onMount, tick } from 'svelte';
	import { ChevronLeft, ChevronRight } from '@lucide/svelte';
	import * as Collapsible from '$lib/components/ui/collapsible';
	import SchoolTree from './SchoolTree.svelte';
	import SchoolGeoMap from './SchoolGeoMap.svelte';
	import SchoolDetailPanel from './SchoolDetailPanel.svelte';
	import {
		ResizablePanel,
		ResizableHandle,
		RESIZABLE_CTX_KEY,
		type ResizableContext
	} from '$lib/components/ui/resizable';
	import { overviewWidths } from './overview-layout';
	import type { School } from '$lib/types/dashboard';

	let {
		schools = [],
		selectedSchoolId = $bindable(null),
		selectedProvince = $bindable(null),
		selectedDistrict = $bindable(null),
		expandedProvinces = $bindable(new Set<string>()),
		expandedCircuits = $bindable(new Set<string>()),
		restoredSchoolId = null,
		revealRequest = 0
	}: {
		schools?: School[];
		selectedSchoolId?: string | null;
		selectedProvince?: string | null;
		selectedDistrict?: string | null;
		expandedProvinces?: Set<string>;
		expandedCircuits?: Set<string>;
		restoredSchoolId?: string | null;
		revealRequest?: number;
	} = $props();

	const id = $props.id();
	const browserId = `${id}-school-browser`;
	const detailsId = `${id}-school-details`;
	const panels = getContext<ResizableContext>(RESIZABLE_CTX_KEY);

	let treeOpen = $state(true);
	let detailsOpen = $state(true);
	let treeCollapsed = $derived(!treeOpen);
	let detailsCollapsed = $derived(!detailsOpen);

	let container: HTMLDivElement;
	let workspaceWidth = $state(0);
	let treeRestoreButton: HTMLButtonElement | null = $state(null);
	let treeCollapseButton: HTMLButtonElement | null = $state(null);
	let detailsRestoreButton: HTMLButtonElement | null = $state(null);
	let detailsCollapseButton: HTMLButtonElement | null = $state(null);

	let widths = $derived(
		overviewWidths(workspaceWidth, panels.sizes, treeCollapsed, detailsCollapsed)
	);

	onMount(() => {
		const observer = new ResizeObserver(() => {
			workspaceWidth = container.clientWidth;
		});
		workspaceWidth = container.clientWidth;
		observer.observe(container);
		return () => observer.disconnect();
	});

	async function focusTree(nextOpen: boolean) {
		await tick();
		if (treeOpen === nextOpen) (nextOpen ? treeCollapseButton : treeRestoreButton)?.focus();
	}

	async function focusDetails(nextOpen: boolean) {
		await tick();
		if (detailsOpen === nextOpen)
			(nextOpen ? detailsCollapseButton : detailsRestoreButton)?.focus();
	}
</script>

<Collapsible.Root bind:open={treeOpen} onOpenChange={focusTree}>
	{#snippet child({ props })}
		<div
			{...props}
			bind:this={container}
			class="overview-panels flex h-full w-full min-w-0"
			class:collapsed={treeCollapsed}
			class:details-collapsed={detailsCollapsed}
			style:--tree-width={`${widths.tree}px`}
			style:--details-width={`${widths.details}px`}
		>
			<ResizablePanel defaultSize={20}>
				<Collapsible.Content id={browserId} forceMount>
					{#snippet child({ props, open })}
						<div {...props} hidden={!open} inert={!open} class="h-full min-h-0">
							<SchoolTree
								initialSchools={schools}
								{restoredSchoolId}
								{revealRequest}
								bind:selectedSchoolId
								bind:selectedProvince
								bind:selectedDistrict
								bind:expandedProvinces
								bind:expandedCircuits
							>
								{#snippet headerAction()}
									<Collapsible.Trigger
										bind:ref={treeCollapseButton}
										aria-label="Collapse school browser"
										title="Collapse school browser"
										class="shrink-0 rounded-md p-1.5 hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring"
										><ChevronLeft class="size-4" /></Collapsible.Trigger
									>
								{/snippet}
							</SchoolTree>
						</div>
					{/snippet}
				</Collapsible.Content>
				{#if treeCollapsed}
					<div class="panel-rail browser-rail h-full w-full text-card-foreground">
						<Collapsible.Trigger
							bind:ref={treeRestoreButton}
							aria-label="Expand school browser"
							title="Expand school browser"
							class="restore-button flex items-center gap-3 rounded-md hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-inset"
						>
							<ChevronRight class="size-4 shrink-0" />
							<span class="rail-label text-xs font-semibold">School browser</span>
						</Collapsible.Trigger>
					</div>
				{/if}
			</ResizablePanel>
			<ResizableHandle />
			<ResizablePanel defaultSize={60}>
				<SchoolGeoMap bind:selectedSchoolId {selectedProvince} {selectedDistrict} />
			</ResizablePanel>
			<ResizableHandle />
			<ResizablePanel defaultSize={20}>
				<Collapsible.Root bind:open={detailsOpen} onOpenChange={focusDetails}>
					{#snippet child({ props })}
						<div {...props} class="flex h-full min-h-0">
							<Collapsible.Content id={detailsId} forceMount>
								{#snippet child({ props, open })}
									<div {...props} hidden={!open} inert={!open} class="h-full min-h-0 flex-1">
										<SchoolDetailPanel {selectedSchoolId} {selectedProvince} {selectedDistrict}>
											{#snippet headerAction()}
												<Collapsible.Trigger
													bind:ref={detailsCollapseButton}
													aria-label="Collapse school details"
													title="Collapse school details"
													class="shrink-0 rounded-md p-1.5 hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring"
													><ChevronRight class="size-4" /></Collapsible.Trigger
												>
											{/snippet}
										</SchoolDetailPanel>
									</div>
								{/snippet}
							</Collapsible.Content>
							{#if detailsCollapsed}
								<div class="panel-rail details-rail h-full w-full text-card-foreground">
									<Collapsible.Trigger
										bind:ref={detailsRestoreButton}
										aria-label="Expand school details"
										title="Expand school details"
										class="restore-button flex items-center gap-3 rounded-md hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-inset"
									>
										<ChevronLeft class="size-4 shrink-0" />
										<span class="rail-label text-xs font-semibold">School details</span>
									</Collapsible.Trigger>
								</div>
							{/if}
						</div>
					{/snippet}
				</Collapsible.Root>
			</ResizablePanel>
		</div>
	{/snippet}
</Collapsible.Root>

<style>
	@media (min-width: 1200px) {
		.overview-panels.collapsed > :global([data-resizable-panel-index='0']) {
			flex: 0 0 40px !important;
		}
		.overview-panels.details-collapsed > :global([data-resizable-panel-index='2']) {
			flex: 0 0 40px !important;
		}
		.overview-panels.collapsed:not(.details-collapsed) > :global([data-resizable-panel-index='2']) {
			flex: 0 0 var(--details-width) !important;
		}
		.overview-panels.details-collapsed:not(.collapsed) > :global([data-resizable-panel-index='0']) {
			flex: 0 0 var(--tree-width) !important;
		}
		.overview-panels.collapsed > :global([data-resizable-panel-index='1']),
		.overview-panels.details-collapsed > :global([data-resizable-panel-index='1']) {
			flex: 1 1 0 !important;
		}
		.overview-panels.collapsed > :global([data-resizable-handle-index='0']) {
			display: none;
		}
		.overview-panels.details-collapsed > :global([data-resizable-handle-index='1']) {
			display: none;
		}
	}
	.panel-rail {
		background: hsl(var(--card));
	}
	.browser-rail {
		border-right: 1px solid hsl(var(--border));
	}
	.details-rail {
		border-left: 1px solid hsl(var(--border));
	}
	.panel-rail :global(.restore-button) {
		width: 100%;
		padding: 1rem 0.5rem;
		flex-direction: column;
	}
	.rail-label {
		writing-mode: vertical-rl;
	}
	@media (max-width: 1199px) {
		.overview-panels {
			flex-direction: column;
		}
		.overview-panels > :global([data-resizable-panel-index]) {
			flex: none !important;
			width: 100%;
		}
		.overview-panels > :global([data-resizable-panel-index='0']) {
			height: 24rem;
		}
		.overview-panels.collapsed > :global([data-resizable-panel-index='0']) {
			height: 40px;
		}
		.overview-panels > :global([data-resizable-panel-index='1']) {
			height: 30rem;
			border-block: 1px solid hsl(var(--border));
		}
		.overview-panels > :global([data-resizable-panel-index='2']) {
			min-height: 22rem !important;
		}
		.overview-panels.details-collapsed > :global([data-resizable-panel-index='2']) {
			height: 40px;
			min-height: 40px !important;
		}
		.overview-panels > :global([data-resizable-handle]) {
			display: none;
		}
		.browser-rail,
		.details-rail {
			border: 0;
		}
		.panel-rail :global(.restore-button) {
			height: 100%;
			flex-direction: row;
			padding: 0.5rem 1rem;
		}
		.rail-label {
			writing-mode: horizontal-tb;
		}
	}
</style>
