<script lang="ts">
	import { Palette, ArrowRight } from '@lucide/svelte';
	import { Dialog } from 'bits-ui';
	import * as Sheet from '$lib/components/ui/sheet';
	import { Button } from '$lib/components/ui/button';
	import ThemePicker from './ThemePicker.svelte';
	import * as m from '$lib/paraglide/messages';
</script>

<Sheet.Root>
	<Sheet.Trigger>
		{#snippet child({ props })}
			<Button
				variant="ghost"
				size="icon"
				class="min-h-11 min-w-11"
				{...props}
				aria-label={m.theme_customize()}
				title={m.theme_customize()}
			>
				<Palette class="size-5" />
			</Button>
		{/snippet}
	</Sheet.Trigger>
	<Sheet.Portal>
		<Sheet.Overlay class="customize-overlay" />
		<Dialog.Content
			class="customize-drawer fixed inset-y-0 end-0 z-50 flex w-[22rem] max-w-full flex-col border-s bg-background text-foreground"
			data-slot="customize-drawer"
		>
			<div class="flex shrink-0 justify-center pt-4">
				<Sheet.Close
					class="flex min-h-[44px] items-center gap-4 rounded-[0.375rem] bg-muted px-4 text-xs font-medium hover:bg-accent focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
				>
					{m.common_close()}
					<ArrowRight class="size-4 rtl:rotate-180" aria-hidden="true" />
				</Sheet.Close>
			</div>
			<div
				class="min-h-0 flex-1 overflow-y-auto overscroll-contain px-4 pt-4 pb-[max(1.5rem,env(safe-area-inset-bottom))]"
			>
				<Sheet.Header class="gap-1 p-0 pb-7">
					<Sheet.Title class="text-2xl tracking-tight">{m.theme_customize()}</Sheet.Title>
					<Sheet.Description>{m.theme_description()}</Sheet.Description>
				</Sheet.Header>
				<ThemePicker />
			</div>
		</Dialog.Content>
	</Sheet.Portal>
</Sheet.Root>
