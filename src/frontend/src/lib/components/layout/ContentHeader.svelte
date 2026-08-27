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
	import { ThemeToggle, LanguageSelector, SchoolSearch } from '$lib/components/layout';
	import { CircleHelp } from '@lucide/svelte';
	import type { User } from '$lib/types';

	interface Props {
		user: User | null | undefined;
	}

	let { user }: Props = $props();

	const segmentLabels: Record<string, () => string> = {
		dashboard: m.nav_dashboard,
		overview: m.nav_overview,
		profile: m.nav_profile,
		settings: m.nav_settings,
		users: m.nav_adminUsers,
		roles: m.nav_adminRoles,
		jobs: m.nav_adminJobs,
		'oauth-providers': m.nav_adminOAuthProviders
	};

	const segmentHrefs: Record<string, string> = {
		dashboard: resolve(routes.dashboard),
		overview: resolve(routes.overview),
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

<header class="flex h-(--header-height) shrink-0 items-center gap-2 border-b px-4 lg:px-6">
	<div class="flex flex-1 items-center gap-2">
		<SidebarTrigger class="-ml-1" />
		<Separator orientation="vertical" class="mx-2 h-4" />
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

	<SchoolSearch />

	<button
		onclick={() => (shortcutsState.isCommandPaletteOpen = true)}
		aria-label={m.shortcuts_commandPalette()}
		class="inline-flex items-center gap-1.5 rounded-lg px-1.5 py-1.5 text-sm text-muted-foreground transition-colors hover:bg-accent hover:text-accent-foreground"
	>
		<kbd
			class="pointer-events-none inline-flex h-5 shrink-0 items-center rounded border bg-background px-1 font-mono text-[10px] font-medium text-muted-foreground"
		>
			{getShortcutSymbol(ShortcutAction.CommandPalette)}
		</kbd>
	</button>

	<div class="flex flex-1 items-center justify-end gap-1 lg:gap-2">
		<LanguageSelector />
		<ThemeToggle />
		<Button
			variant="ghost"
			size="icon"
			class="hidden sm:flex"
			onclick={() => (shortcutsState.isHelpOpen = true)}
			aria-label={m.shortcuts_help()}
		>
			<CircleHelp class="size-4" />
		</Button>
	</div>
</header>
