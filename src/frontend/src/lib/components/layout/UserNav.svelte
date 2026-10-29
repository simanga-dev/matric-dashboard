<script lang="ts">
	import * as Avatar from '$lib/components/ui/avatar';
	import { Button } from '$lib/components/ui/button';
	import * as DropdownMenu from '$lib/components/ui/dropdown-menu';
	import { SidebarMenuButton } from '$lib/components/ui/sidebar';
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { routes } from '$lib/config';
	import * as m from '$lib/paraglide/messages';
	import type { User } from '$lib/types';
	import { getShortcutSymbol, ShortcutAction } from '$lib/state/shortcuts.svelte';
	import { logout } from '$lib/auth';
	import { ChevronsUpDown } from '@lucide/svelte';

	interface Props {
		user: User | null | undefined;
		/** `icon` is the compact header trigger; `sidebar` is the full account row. */
		variant?: 'icon' | 'sidebar';
	}

	let { user, variant = 'icon' }: Props = $props();

	// Avatar URL - stable until user object changes via invalidateAll
	const avatarUrl = $derived(user?.hasAvatar && user?.id ? `/api/users/${user.id}/avatar` : null);

	const displayName = $derived(
		[user?.firstName, user?.lastName].filter(Boolean).join(' ') || m.common_account()
	);

	function getInitials(name: string) {
		return name
			.split(' ')
			.map((n) => n[0])
			.join('')
			.toUpperCase()
			.slice(0, 2);
	}
</script>

<DropdownMenu.Root>
	<DropdownMenu.Trigger>
		{#snippet child({ props })}
			{#if variant === 'sidebar'}
				<SidebarMenuButton
					size="lg"
					class="h-12 gap-2.5 rounded-md px-2 data-[state=open]:bg-sidebar-foreground/10"
					aria-label={user?.email ? `${displayName} (${user.email})` : displayName}
					{...props}
				>
					<Avatar.Root class="size-8 shrink-0 rounded-lg">
						{#if avatarUrl}
							<Avatar.Image src={avatarUrl} alt={user?.username || m.common_user()} />
						{/if}
						<Avatar.Fallback class="rounded-lg">
							{getInitials(user?.username || m.common_user())}
						</Avatar.Fallback>
					</Avatar.Root>
					<div
						class="grid min-w-0 flex-1 text-start text-sm leading-tight group-data-[collapsible=icon]:hidden"
					>
						<span class="truncate font-semibold">{displayName}</span>
						<span class="truncate text-xs font-normal text-sidebar-foreground/60">
							{user?.email}
						</span>
					</div>
					<ChevronsUpDown
						class="ms-auto size-4 shrink-0 text-sidebar-foreground/50 group-data-[collapsible=icon]:hidden"
					/>
				</SidebarMenuButton>
			{:else}
				<Button variant="ghost" size="icon" class="min-h-11 min-w-11 rounded-full" {...props}>
					<Avatar.Root class="h-7 w-7">
						{#if avatarUrl}
							<Avatar.Image src={avatarUrl} alt={user?.username || m.common_user()} />
						{/if}
						<Avatar.Fallback>{getInitials(user?.username || m.common_user())}</Avatar.Fallback>
					</Avatar.Root>
				</Button>
			{/if}
		{/snippet}
	</DropdownMenu.Trigger>
	<DropdownMenu.Content class="w-56" align="end">
		<DropdownMenu.Label class="font-normal">
			<div class="flex flex-col gap-0.5">
				<p class="text-sm leading-none font-medium">{displayName}</p>
				<p class="truncate text-xs leading-none text-muted-foreground">{user?.email}</p>
			</div>
		</DropdownMenu.Label>
		<DropdownMenu.Separator />
		<DropdownMenu.Group>
			<DropdownMenu.Item onclick={() => goto(resolve(routes.profile))}>
				{m.nav_profile()}
			</DropdownMenu.Item>
			<DropdownMenu.Item onclick={() => goto(resolve(routes.settings))}>
				{m.nav_settings()}
				<DropdownMenu.Shortcut>{getShortcutSymbol(ShortcutAction.Settings)}</DropdownMenu.Shortcut>
			</DropdownMenu.Item>
		</DropdownMenu.Group>
		<DropdownMenu.Separator />
		<DropdownMenu.Item onclick={logout}>
			{m.nav_logout()}
			<DropdownMenu.Shortcut>{getShortcutSymbol(ShortcutAction.Logout)}</DropdownMenu.Shortcut>
		</DropdownMenu.Item>
	</DropdownMenu.Content>
</DropdownMenu.Root>
