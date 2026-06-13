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
</script>

<svelte:head>
	<title>{m.meta_titleTemplate({ title: m.meta_overview_title() })}</title>
	<meta name="description" content={m.meta_overview_description()} />
</svelte:head>

<ResizablePanelGroup orientation="horizontal" class="h-[calc(100vh-12rem)] rounded-lg border">
	<ResizablePanel defaultSize={15}>
			<SchoolTree bind:selectedSchoolId />
	</ResizablePanel>
	<ResizableHandle />

		<ResizablePanel defaultSize={60}>
			<SchoolMap />
		</ResizablePanel>

		<ResizableHandle />
		<ResizablePanel defaultSize={15}>
				<SchoolCards bind:selectedSchoolId />
		</ResizablePanel>
</ResizablePanelGroup
>
