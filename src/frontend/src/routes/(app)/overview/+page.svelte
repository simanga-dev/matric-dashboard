<script lang="ts">
	import { PageHeader } from '$lib/components/common';
	import { SchoolTree, SchoolMap, SchoolCards } from '$lib/components/dashboard';
	import {
		ResizablePanelGroup,
		ResizablePanel,
		ResizableHandle
	} from '$lib/components/ui/resizable';
	import * as m from '$lib/paraglide/messages';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	let user = $derived(data.user);
	let greeting = $derived(
		user?.firstName ? m.dashboard_welcome({ name: user.firstName }) : m.dashboard_welcomeGeneric()
	);

	let selectedSchoolId: number | null = $state(null);
	let selectedProvince: string | null = $state(null);
</script>

<svelte:head>
	<title>{m.meta_titleTemplate({ title: m.meta_overview_title() })}</title>
	<meta name="description" content={m.meta_overview_description()} />
</svelte:head>

<ResizablePanelGroup orientation="horizontal" class="h-[calc(100vh-12rem)] rounded-lg">
	<ResizablePanel defaultSize={18}>
		<SchoolTree bind:selectedSchoolId bind:selectedProvince />
	</ResizablePanel>
	<ResizableHandle />
	<ResizablePanel defaultSize={54}>
		<SchoolMap bind:selectedProvince />
	</ResizablePanel>
	<ResizableHandle />
	<ResizablePanel defaultSize={28}>
		<SchoolCards bind:selectedSchoolId bind:selectedProvince />
	</ResizablePanel>
</ResizablePanelGroup>
