<script lang="ts">
	import { page } from '$app/state';
	import { DatabaseZap, LoaderCircle } from '@lucide/svelte';
	import { handleMutationError } from '$lib/api';
	import { seedSchoolSearchIndex } from '$lib/api/search-index';
	import { toast } from '$lib/components/ui/sonner';
	import { createCooldown } from '$lib/state';
	import * as m from '$lib/paraglide/messages';

	const cooldown = createCooldown();
	let seeding = $state(false);
	let user = $derived(page.data.user);

	async function seed() {
		if (seeding || cooldown.active) return;
		seeding = true;

		try {
			const { data, error, response } = await seedSchoolSearchIndex();

			if (response.ok && data) {
				toast.success(m.search_index_seedSuccess({ count: data.indexed.toLocaleString() }));
			} else {
				handleMutationError(response, error, {
					cooldown,
					fallback: m.search_index_seedError()
				});
			}
		} catch {
			toast.error(m.search_index_seedError());
		} finally {
			seeding = false;
		}
	}
</script>

{#if user}
	<button
		type="button"
		aria-label={m.search_index_seed()}
		title={seeding ? m.search_index_seeding() : m.search_index_seed()}
		disabled={seeding || cooldown.active}
		onclick={seed}
		class="flex size-8 shrink-0 items-center justify-center rounded-md text-muted-foreground hover:bg-muted hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-50 pointer-coarse:size-11"
	>
		{#if seeding}
			<LoaderCircle class="size-4 animate-spin" aria-hidden="true" />
		{:else}
			<DatabaseZap class="size-4" aria-hidden="true" />
		{/if}
	</button>
{/if}
