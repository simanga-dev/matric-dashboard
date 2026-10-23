<script lang="ts">
	import * as Sidebar from '$lib/components/ui/sidebar';
	import { page } from '$app/state';
	import { resolve } from '$app/paths';
	import { routes } from '$lib/config';
	import { adminRoutes, type AdminRoute } from '$lib/config';
	import { hasPermission } from '$lib/utils';
	import {
		Users,
		Shield,
		Clock,
		KeyRound,
		GraduationCap,
		LogIn,
		BookOpen,
		BookMarked,
		MapPinned,
		Trophy,
		List,
		UserRound,
		Settings,
		type IconProps
	} from '@lucide/svelte';
	import * as m from '$lib/paraglide/messages';
	import UserNav from './UserNav.svelte';
	import { LoginForm } from '$lib/components/auth';
	import * as Dialog from '$lib/components/ui/dialog';
	import type { Component } from 'svelte';
	import type { User } from '$lib/types';

	interface Props {
		user: User | null | undefined;
	}

	let { user }: Props = $props();

	const sidebar = Sidebar.useSidebar();

	// Dokploy-style rows: compact height, quiet icons, and a translucent
	// foreground overlay for hover and the active item. Using the foreground
	// token keeps the contrast right in both light and dark themes.
	const navigationButtonClass =
		'h-9 min-h-9 gap-2.5 rounded-md px-2 text-sm font-normal text-sidebar-foreground hover:bg-sidebar-foreground/[0.06] data-[active=true]:bg-sidebar-foreground/10 data-[active=true]:font-medium data-[active=true]:text-sidebar-foreground [&>svg]:text-sidebar-foreground/80 [&>svg]:transition-colors group-data-[collapsible=icon]:min-h-0';

	const groupLabelClass = 'h-8 px-2 text-xs font-medium text-sidebar-foreground/60';

	type NavItem = { title: () => string; href: string; icon: Component<IconProps> };
	type AdminNavItem = NavItem & { permission: AdminRoute['permission'] };

	const navMain: NavItem[] = [
		{
			title: m.nav_overview,
			href: resolve(routes.overview),
			icon: MapPinned
		},
		{
			title: m.nav_schools,
			href: resolve(routes.schools),
			icon: GraduationCap
		},
		{
			title: () => 'Top Achievers',
			href: '/top-achievers',
			icon: Trophy
		},
		{
			title: () => 'Subjects',
			href: '/subjects',
			icon: BookMarked
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

	const accountItems: NavItem[] = [
		{
			title: m.nav_profile,
			href: resolve(routes.profile),
			icon: UserRound
		},
		{
			title: m.nav_settings,
			href: resolve(routes.settings),
			icon: Settings
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
		if (href === resolve(routes.overview)) {
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

<!-- eslint-disable svelte/no-navigation-without-resolve -- hrefs are pre-resolved using resolve() for routes -->
<Sidebar.Root variant="floating" collapsible="icon" class="ps-[env(safe-area-inset-left,0px)]">
	<Sidebar.Header class="p-2">
		<Sidebar.Menu class="flex-row items-center gap-1 group-data-[collapsible=icon]:flex-col">
			<Sidebar.MenuItem class="min-w-0 flex-1 group-data-[collapsible=icon]:flex-none">
				<Sidebar.MenuButton
					size="lg"
					class="h-10 gap-2 rounded-md px-1.5 hover:bg-sidebar-foreground/[0.06]"
				>
					{#snippet child({ props })}
						<a
							href={resolve(routes.overview)}
							onclick={handleNavigate}
							class="flex min-w-0 items-center gap-2"
							aria-label={m.app_name()}
							{...props}
						>
							<div
								class="flex size-7 shrink-0 items-center justify-center rounded-md border border-sidebar-border bg-sidebar-accent/50 text-sidebar-foreground"
							>
								<GraduationCap class="size-4" />
							</div>
							<span class="truncate text-sm font-medium group-data-[collapsible=icon]:hidden">
								{m.app_name()}
							</span>
						</a>
					{/snippet}
				</Sidebar.MenuButton>
			</Sidebar.MenuItem>
			<Sidebar.MenuItem>
				<Sidebar.Trigger class="size-8 shrink-0" />
			</Sidebar.MenuItem>
		</Sidebar.Menu>
	</Sidebar.Header>

	<Sidebar.Content class="gap-2">
		<Sidebar.Group>
			<Sidebar.GroupLabel class={groupLabelClass}>Home</Sidebar.GroupLabel>
			<Sidebar.GroupContent>
				<Sidebar.Menu>
					{#each navMain as item (item.href)}
						<Sidebar.MenuItem>
							<Sidebar.MenuButton
								class={navigationButtonClass}
								tooltipContent={item.title()}
								isActive={isActive(item.href)}
							>
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

		<Sidebar.Group>
			<Sidebar.GroupLabel class={groupLabelClass}>Documents</Sidebar.GroupLabel>
			<Sidebar.GroupContent>
				<Sidebar.Menu>
					{#each documents as item (item.href)}
						<Sidebar.MenuItem>
							<Sidebar.MenuButton
								class={navigationButtonClass}
								tooltipContent={item.title()}
								isActive={isActive(item.href)}
							>
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

		{#if user}
			<Sidebar.Group>
				<Sidebar.GroupLabel class={groupLabelClass}>Settings</Sidebar.GroupLabel>
				<Sidebar.GroupContent>
					<Sidebar.Menu>
						{#each accountItems as item (item.href)}
							<Sidebar.MenuItem>
								<Sidebar.MenuButton
									class={navigationButtonClass}
									tooltipContent={item.title()}
									isActive={isActive(item.href)}
								>
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
		{/if}

		{#if visibleAdminItems.length > 0}
			<Sidebar.Group>
				<Sidebar.GroupLabel class={groupLabelClass}>{m.nav_admin()}</Sidebar.GroupLabel>
				<Sidebar.GroupContent>
					<Sidebar.Menu>
						{#each visibleAdminItems as item (item.href)}
							<Sidebar.MenuItem>
								<Sidebar.MenuButton
									class={navigationButtonClass}
									tooltipContent={item.title()}
									isActive={isActive(item.href)}
								>
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
		{/if}
	</Sidebar.Content>

	<Sidebar.Footer class="pb-[max(0.5rem,env(safe-area-inset-bottom,0px))]">
		<Sidebar.Menu>
			<Sidebar.MenuItem>
				{#if user}
					<UserNav {user} variant="sidebar" />
				{:else}
					<Sidebar.MenuButton
						size="lg"
						class="h-10 gap-2 rounded-md px-2 text-sidebar-foreground hover:bg-sidebar-foreground/[0.06]"
						tooltipContent={m.auth_login_submit()}
						aria-label={m.auth_login_submit()}
						onclick={() => (loginDialogOpen = true)}
					>
						<LogIn class="size-4" />
						<span class="truncate text-sm font-normal group-data-[collapsible=icon]:hidden">
							{m.auth_login_submit()}
						</span>
					</Sidebar.MenuButton>
				{/if}
			</Sidebar.MenuItem>
		</Sidebar.Menu>
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
