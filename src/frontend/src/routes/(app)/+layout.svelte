<script lang="ts">
	import * as Sidebar from '$lib/components/ui/sidebar';
	import { AppSidebar, ContentHeader, CommandPalette } from '$lib/components/layout';
	import { EmailVerificationBanner } from '$lib/components/auth';
	import { page } from '$app/state';
	import { invalidateAll } from '$app/navigation';
	import { healthState } from '$lib/state';

	let { children, data } = $props();

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
	class="h-dvh overflow-hidden"
	style="--sidebar-width: calc(var(--spacing) * 72); --header-height: calc(var(--spacing) * 12);"
>
	<AppSidebar user={data.user} />
	<Sidebar.Inset>
		<ContentHeader user={data.user} />
		{#if data.user && !data.user.emailConfirmed}
			<EmailVerificationBanner />
		{/if}

		<div class="flex flex-1 flex-col bg-background md:m-2 md:rounded-xl md:shadow-sm">
			<div
				class="flex flex-1 flex-col gap-4 overflow-y-auto overscroll-contain p-4 pb-[max(4rem,calc(env(safe-area-inset-bottom,0px)+2rem))] lg:gap-6 lg:p-6 lg:pb-[max(4rem,calc(env(safe-area-inset-bottom,0px)+2rem))]"
			>
				{#key page.url.pathname}
					<div
						class="w-full motion-safe:duration-300 motion-safe:animate-in motion-safe:fade-in motion-safe:slide-in-from-bottom-4"
					>
						{@render children()}
					</div>
				{/key}
			</div>
		</div>
	</Sidebar.Inset>
</Sidebar.Provider>
