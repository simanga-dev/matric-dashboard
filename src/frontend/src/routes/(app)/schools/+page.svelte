<script lang="ts">
	import { page } from '$app/state';
	import { SchoolsTable } from '$lib/components/dashboard';
	import * as m from '$lib/paraglide/messages';

	// Deep links such as /schools?search=pretoria keep their query when the
	// page is opened directly or refreshed.
	let search = $derived(page.url.searchParams.get('search') ?? undefined);
</script>

<svelte:head>
	<title>{m.meta_titleTemplate({ title: m.meta_schools_title() })}</title>
	<meta name="description" content={m.meta_schools_description()} />
</svelte:head>

<div class="space-y-6">
	<header class="space-y-2">
		<h1 class="text-2xl font-semibold tracking-tight sm:text-3xl">{m.schools_title()}</h1>
		<p class="max-w-2xl text-sm leading-relaxed text-muted-foreground">
			{m.schools_subtitle()}
		</p>
	</header>

	<SchoolsTable title={m.schools_allTitle()} pageSize={25} sortable externalSearch={search} />
</div>
