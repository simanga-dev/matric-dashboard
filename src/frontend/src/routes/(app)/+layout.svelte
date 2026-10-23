<script lang="ts">
	import * as Sidebar from '$lib/components/ui/sidebar';
	import { AppSidebar, ContentHeader, CommandPalette } from '$lib/components/layout';
	import { EmailVerificationBanner } from '$lib/components/auth';
	import { page } from '$app/state';
	import { invalidateAll } from '$app/navigation';
	import { healthState } from '$lib/state';
	import { cn } from '$lib/utils';

	let { children, data } = $props();

	// The overview is a full-bleed workspace: it manages its own height and
	// wants no page padding or card frame around it.
	let fullBleed = $derived(page.route.id === '/(app)/overview');

	// The school profile already lives inside the inset surface. Adding a
	// second rounded, shadowed frame around it reads as a card inside a card.
	let framedPage = $derived(!fullBleed && page.route.id !== '/(app)/schools/[id]');

	// When health polling detects the backend went down, re-run server loads.
	// The (app) layout.server.ts will throw 503, showing the error page with
	// auto-recovery. Only trigger on a confirmed online->offline transition.
	let wasOnline = false;
	$effect(() => {
		if (healthState.checked && wasOnline && !healthState.online) {
			invalidateAll();
		}
		if (healthState.checked) wasOnline = healthState.online;
	});
</script>

<CommandPalette user={data.user} />

<Sidebar.Provider
	open={data.sidebarOpen}
	class="h-dvh overflow-hidden bg-sidebar"
	style="--sidebar-width: 19.5rem; --header-height: calc(var(--spacing) * 12);"
>
	<AppSidebar user={data.user} />
	<Sidebar.Inset
		class={fullBleed
			? 'md:m-0 md:rounded-none md:shadow-none'
			: 'md:my-2 md:me-2 md:rounded-xl md:shadow-sm'}
	>
		<ContentHeader borderless={fullBleed} />
		{#if data.user && !data.user.emailConfirmed}
			<EmailVerificationBanner />
		{/if}

		<div
			class={cn(
				'flex min-h-0 flex-1 flex-col bg-background',
				framedPage && 'md:m-2 md:rounded-xl md:shadow-sm'
			)}
		>
			<div
				class={cn(
					'flex flex-1 flex-col overflow-y-auto overscroll-contain',
					!fullBleed &&
						'gap-4 p-4 pb-[max(4rem,calc(env(safe-area-inset-bottom,0px)+2rem))] lg:gap-6 lg:p-6 lg:pb-[max(4rem,calc(env(safe-area-inset-bottom,0px)+2rem))]'
				)}
			>
				{#key page.url.pathname}
					<div
						class={cn(
							'w-full',
							!fullBleed &&
								'motion-safe:duration-300 motion-safe:animate-in motion-safe:fade-in motion-safe:slide-in-from-bottom-4'
						)}
					>
						{@render children()}
					</div>
				{/key}
			</div>
		</div>
	</Sidebar.Inset>
</Sidebar.Provider>
