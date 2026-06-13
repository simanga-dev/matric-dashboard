<script lang="ts">
	import { SidebarTrigger } from '$lib/components/ui/sidebar';
	import { Separator } from '$lib/components/ui/separator';
	import { Button } from '$lib/components/ui/button';
	import * as Breadcrumb from '$lib/components/ui/breadcrumb';
	import { page } from '$app/state';
	import { resolve } from '$app/paths';
	import { breadcrumbState } from '$lib/state/breadcrumb.svelte';
	import { shortcutsState, ShortcutAction, getShortcutSymbol } from '$lib/state/shortcuts.svelte';
	import { routes } from '$lib/config';
	import { adminRoutes } from '$lib/config';
	import * as m from '$lib/paraglide/messages';
	import { Search } from '@lucide/svelte';

	const segmentLabels: Record<string, () => string> = {
		dashboard: m.nav_dashboard,
		profile: m.nav_profile,
		settings: m.nav_settings,
		users: m.nav_adminUsers,
		roles: m.nav_adminRoles,
		jobs: m.nav_adminJobs,
		'oauth-providers': m.nav_adminOAuthProviders
	};

	const segmentHrefs: Record<string, string> = {
		dashboard: resolve(routes.dashboard),
		profile: resolve(routes.profile),
		settings: resolve(routes.settings),
		users: resolve(adminRoutes.users.path),
		roles: resolve(adminRoutes.roles.path),
		jobs: resolve(adminRoutes.jobs.path),
		'oauth-providers': resolve(adminRoutes.oauthProviders.path)
	};

	interface Crumb {
		label: string;
		href?: string;
	}

	let isDetailPage = $derived.by(() => {
		const segments = page.url.pathname.split('/').filter(Boolean);
		const meaningful = segments.filter((s) => s !== 'admin');
		return meaningful.length > 1;
	});

	let crumbs = $derived.by((): Crumb[] => {
		const pathname = page.url.pathname;
		const segments = pathname.split('/').filter(Boolean);

		// Filter out "admin" - it's not a navigable page
		const meaningful = segments.filter((s) => s !== 'admin');

		const result: Crumb[] = [];
		for (let i = 0; i < meaningful.length; i++) {
			const segment = meaningful[i];
			if (!segment) continue;
			const isLast = i === meaningful.length - 1;
			const labelFn = segmentLabels[segment];

			if (labelFn) {
				if (isLast) {
					result.push({ label: labelFn() });
				} else {
					result.push({ label: labelFn(), href: segmentHrefs[segment] });
				}
			} else if (isLast && breadcrumbState.dynamicLabel) {
				result.push({ label: breadcrumbState.dynamicLabel });
			} else if (isLast) {
				result.push({ label: segment });
			}
		}

		return result;
	});
</script>

<header
	class={isDetailPage
		? 'flex h-10 shrink-0 items-center gap-2 border-b bg-background px-4 md:h-12'
		: 'hidden h-12 shrink-0 items-center gap-2 border-b bg-background px-4 md:flex'}
>
	<div class="flex flex-1 items-center gap-2">
		<SidebarTrigger class="hidden size-7 md:inline-flex" />
		<Separator orientation="vertical" class="hidden h-4 md:block" />
		{#key page.url.pathname}
			<Breadcrumb.Root class="motion-safe:duration-200 motion-safe:animate-in motion-safe:fade-in">
				<Breadcrumb.List>
					{#each crumbs as crumb, i (crumb.href ?? crumb.label)}
						<Breadcrumb.Item>
							{#if crumb.href}
								<Breadcrumb.Link href={crumb.href}>{crumb.label}</Breadcrumb.Link>
							{:else}
								<Breadcrumb.Page>{crumb.label}</Breadcrumb.Page>
							{/if}
						</Breadcrumb.Item>
						{#if i < crumbs.length - 1}
							<Breadcrumb.Separator />
						{/if}
					{/each}
				</Breadcrumb.List>
			</Breadcrumb.Root>
		{/key}
	</div>
	<Button
		variant="outline"
		class="h-8 w-full max-w-xs justify-start gap-2 text-muted-foreground"
		onclick={() => (shortcutsState.isCommandPaletteOpen = true)}
		aria-label={m.shortcuts_commandPalette()}
	>
		<Search class="size-4" />
		<span class="flex-1 truncate text-start">{m.commandPalette_search()}</span>
		<kbd
			class="pointer-events-none ms-auto inline-flex h-5 shrink-0 items-center rounded border bg-muted px-1 font-mono text-[10px] font-medium text-muted-foreground select-none"
		>
			{getShortcutSymbol(ShortcutAction.CommandPalette)}
		</kbd>
	</Button>
	<div class="flex flex-1 items-center justify-end"></div>
</header>
