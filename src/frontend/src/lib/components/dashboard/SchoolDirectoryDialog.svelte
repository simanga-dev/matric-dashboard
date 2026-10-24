<script lang="ts">
	import * as Dialog from '$lib/components/ui/dialog';
	import SchoolsTable from './SchoolsTable.svelte';
	import type { School } from '$lib/types/dashboard';
	import * as m from '$lib/paraglide/messages';

	let {
		open = $bindable(false),
		schools = []
	}: {
		/** Mirrors the `schools` query parameter of the overview URL. */
		open?: boolean;
		/** The same directory the overview tree browses, used for province options. */
		schools?: School[];
	} = $props();

	let provinces = $derived(
		[...new Set(schools.map((school) => school.province))].sort((a, b) => a.localeCompare(b))
	);
</script>

<Dialog.Root bind:open>
	<Dialog.Content
		class="flex max-h-[90dvh] w-[90vw] max-w-[90vw] flex-col gap-3 overflow-hidden p-4 sm:max-w-[90vw] lg:p-6"
	>
		<Dialog.Header class="shrink-0 pe-8">
			<Dialog.Title>{m.schools_allTitle()}</Dialog.Title>
			<Dialog.Description>{m.meta_schools_description()}</Dialog.Description>
		</Dialog.Header>
		<div class="min-h-0 flex-1 overflow-y-auto overscroll-contain pe-1">
			<SchoolsTable title={m.schools_allTitle()} pageSize={25} {provinces} />
		</div>
	</Dialog.Content>
</Dialog.Root>
