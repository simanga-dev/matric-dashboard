<script lang="ts">
	import * as Sidebar from '$lib/components/ui/sidebar';
	import { page } from '$app/state';
	import { resolve } from '$app/paths';
	import { routes } from '$lib/config';
	import { adminRoutes, type AdminRoute } from '$lib/config';
	import { hasPermission } from '$lib/utils';
	import {
		LayoutDashboard,
		Eye,
		Users,
		Shield,
		Clock,
		KeyRound,
		Package2,
		CircleHelp,
		LogIn,
		BookOpen,
		GraduationCap,
		Trophy,
		List,
		type IconProps
	} from '@lucide/svelte';
	import * as m from '$lib/paraglide/messages';
	import { ThemeToggle, LanguageSelector, UserNav } from '$lib/components/layout';
	import { LoginForm } from '$lib/components/auth';
	import * as Dialog from '$lib/components/ui/dialog';
	import { shortcutsState, ShortcutAction, getShortcutSymbol } from '$lib/state/shortcuts.svelte';
	import type { Component } from 'svelte';
	import type { User } from '$lib/types';

	interface Props {
		user: User | null | undefined;
	}

	let { user }: Props = $props();

	const sidebar = Sidebar.useSidebar();
	let collapsed = $derived(sidebar.state === 'collapsed');

	type NavItem = { title: () => string; href: string; icon: Component<IconProps> };
	type AdminNavItem = NavItem & { permission: AdminRoute['permission'] };

	const navMain: NavItem[] = [
		{
			title: m.nav_overview,
			href: resolve(routes.overview),
			icon: Eye
		},
		{
			title: m.nav_dashboard,
			href: resolve(routes.dashboard),
			icon: LayoutDashboard
		},
		{
			title: () => 'Schools',
			href: '/schools',
			icon: GraduationCap
		},
		{
			title: () => 'Top Achievers',
			href: '/top-achievers',
			icon: Trophy
		}
	];

	const documents: NavItem[] = [
		{
			title: () => 'Past Papers',
			href: '/past-papers',
			icon: BookOpen
		},
		{
			title: () => 'Study Guide',
			href: '/study-guide',
			icon: List
		}
	];

	const adminItems: AdminNavItem[] = [
		{
			title: m.nav_adminUsers,
			href: resolve(adminRoutes.users.path),
			icon: Users,
			permission: adminRoutes.users.permission
		},
		{
			title: m.nav_adminRoles,
			href: resolve(adminRoutes.roles.path),
			icon: Shield,
			permission: adminRoutes.roles.permission
		},
		{
			title: m.nav_adminJobs,
			href: resolve(adminRoutes.jobs.path),
			icon: Clock,
			permission: adminRoutes.jobs.permission
		},
		{
			title: m.nav_adminOAuthProviders,
			href: resolve(adminRoutes.oauthProviders.path),
			icon: KeyRound,
			permission: adminRoutes.oauthProviders.permission
		}
	];


	let visibleAdminItems = $derived(
		user ? adminItems.filter((item) => hasPermission(user, item.permission)) : []
	);

	let loginDialogOpen = $state(false);

	function isActive(href: string): boolean {
		const pathname = page.url.pathname;
		if (
			href === resolve(routes.dashboard) ||
			href === resolve(routes.overview)
		) {
			return pathname === href;
		}
		return pathname.startsWith(href);
	}

	function handleNavigate() {
		if (sidebar.isMobile) {
			sidebar.setOpenMobile(false);
		}
	}
</script>

<!-- eslint-disable svelte/no-navigation-without-resolve -- hrefs are pre-resolved using resolve() in navMain -->
<Sidebar.Root variant="inset" collapsible="icon" class="ps-[env(safe-area-inset-left,0px)]">
	<Sidebar.Header>
		<Sidebar.Menu>
			<Sidebar.MenuItem>
				<Sidebar.MenuButton size="lg">
					{#snippet child({ props })}
						<a href={resolve(routes.dashboard)} onclick={handleNavigate} {...props}>
							<div
								class="flex aspect-square size-8 items-center justify-center rounded-lg bg-sidebar-primary text-sidebar-primary-foreground"
							>
								<Package2 class="size-4" />
							</div>
							<span class="truncate font-semibold">{m.app_name()}</span>
						</a>
					{/snippet}
				</Sidebar.MenuButton>
			</Sidebar.MenuItem>
		</Sidebar.Menu>
	</Sidebar.Header>
	<Sidebar.Content>
		<Sidebar.Group>
			<Sidebar.GroupContent>
				<Sidebar.Menu>
					{#each navMain as item (item.href)}
						<Sidebar.MenuItem>
							<Sidebar.MenuButton tooltipContent={item.title()} isActive={isActive(item.href)}>
								{#snippet child({ props })}
									<a
										href={item.href}
										onclick={handleNavigate}
										aria-current={isActive(item.href) ? 'page' : undefined}
										{...props}
									>
										<item.icon />
										<span>{item.title()}</span>
									</a>
								{/snippet}
							</Sidebar.MenuButton>
						</Sidebar.MenuItem>
					{/each}
				</Sidebar.Menu>
			</Sidebar.GroupContent>
		</Sidebar.Group>

		<Sidebar.Group class="group-data-[collapsible=icon]:hidden">
			<Sidebar.GroupLabel>Documents</Sidebar.GroupLabel>
			<Sidebar.Menu>
				{#each documents as item (item.href)}
					<Sidebar.MenuItem>
						<Sidebar.MenuButton tooltipContent={item.title()}>
							{#snippet child({ props })}
								<a
									href={item.href}
									onclick={handleNavigate}
									aria-current={isActive(item.href) ? 'page' : undefined}
									{...props}
								>
									<item.icon />
									<span>{item.title()}</span>
								</a>
							{/snippet}
						</Sidebar.MenuButton>
					</Sidebar.MenuItem>
				{/each}
			</Sidebar.Menu>
		</Sidebar.Group>

		{#if visibleAdminItems.length > 0}
			<Sidebar.Group class="group-data-[collapsible=icon]:hidden">
				<Sidebar.GroupLabel>{m.nav_admin()}</Sidebar.GroupLabel>
				<Sidebar.Menu>
					{#each visibleAdminItems as item (item.href)}
						<Sidebar.MenuItem>
							<Sidebar.MenuButton tooltipContent={item.title()} isActive={isActive(item.href)}>
								{#snippet child({ props })}
									<a
										href={item.href}
										onclick={handleNavigate}
										aria-current={isActive(item.href) ? 'page' : undefined}
										{...props}
									>
										<item.icon />
										<span>{item.title()}</span>
									</a>
								{/snippet}
							</Sidebar.MenuButton>
						</Sidebar.MenuItem>
					{/each}
				</Sidebar.Menu>
			</Sidebar.Group>
		{/if}

	</Sidebar.Content>
	<Sidebar.Footer class="pb-[max(0.5rem,env(safe-area-inset-bottom,0px))]">
		{#if !sidebar.isMobile}
			<div class="flex items-center gap-1 group-data-[collapsible=icon]:flex-col">
				<LanguageSelector />
				<ThemeToggle {collapsed} />
				<div class="flex-1 group-data-[collapsible=icon]:hidden"></div>
				{#if user}
					<UserNav {user} />
				{:else}
					<Sidebar.MenuButton
						tooltipContent={m.auth_login_submit()}
						onclick={() => (loginDialogOpen = true)}
					>
						<LogIn class="size-5" />
					</Sidebar.MenuButton>
				{/if}
			</div>
			<Sidebar.Separator />
			<Sidebar.Menu>
				<Sidebar.MenuItem>
					<Sidebar.MenuButton
						tooltipContent={m.shortcuts_help()}
						onclick={() => (shortcutsState.isHelpOpen = true)}
					>
						<CircleHelp class="size-5" />
						<span class="flex-1">{m.shortcuts_help()}</span>
						<kbd
							class="pointer-events-none ms-auto inline-flex h-5 shrink-0 items-center rounded border bg-muted px-1 font-mono text-[10px] font-medium text-muted-foreground select-none group-data-[collapsible=icon]:hidden"
						>
							{getShortcutSymbol(ShortcutAction.Help)}
						</kbd>
					</Sidebar.MenuButton>
				</Sidebar.MenuItem>
			</Sidebar.Menu>
		{:else if !user}
			<Sidebar.Menu>
				<Sidebar.MenuItem>
					<Sidebar.MenuButton
						tooltipContent={m.auth_login_submit()}
						onclick={() => (loginDialogOpen = true)}
					>
						<LogIn />
						<span>{m.auth_login_submit()}</span>
					</Sidebar.MenuButton>
				</Sidebar.MenuItem>
			</Sidebar.Menu>
		{/if}
	</Sidebar.Footer>
</Sidebar.Root>

<Dialog.Root bind:open={loginDialogOpen}>
	<Dialog.Content class="sm:max-w-md">
		<Dialog.Header>
			<Dialog.Title>{m.auth_login_title({ name: m.app_name() })}</Dialog.Title>
			<Dialog.Description>{m.auth_login_subtitle()}</Dialog.Description>
		</Dialog.Header>
		<div class="py-4">
			<LoginForm
				inline
				onSuccess={() => {
					loginDialogOpen = false;
				}}
			/>
		</div>
	</Dialog.Content>
</Dialog.Root>
