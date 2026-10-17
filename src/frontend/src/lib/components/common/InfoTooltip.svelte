<script lang="ts">
	import { CircleHelp } from '@lucide/svelte';
	import * as Tooltip from '$lib/components/ui/tooltip';

	let { label, text }: { label: string; text: string } = $props();
	let open = $state(false);
</script>

<Tooltip.Root bind:open delayDuration={200} disableCloseOnTriggerClick>
	<Tooltip.Trigger
		type="button"
		aria-label={`About ${label}`}
		onclick={() => (open = !open)}
		class="inline-flex size-6 shrink-0 items-center justify-center rounded-full align-middle text-muted-foreground transition-colors hover:bg-muted hover:text-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
	>
		<CircleHelp class="size-3.5" aria-hidden="true" />
	</Tooltip.Trigger>
	<Tooltip.Content
		side="top"
		sideOffset={6}
		class="max-w-[min(18rem,calc(100vw-2rem))] text-left text-xs leading-relaxed font-normal text-pretty"
	>
		{text}
	</Tooltip.Content>
</Tooltip.Root>
