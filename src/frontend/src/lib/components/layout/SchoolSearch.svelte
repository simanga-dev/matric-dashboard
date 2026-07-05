<script lang="ts">
	import { Search, Loader2, School as SchoolIcon, MapPin, TrendingUp } from '@lucide/svelte';
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { fetchSchools } from '$lib/api/dashboard';
	import { schoolPath } from '$lib/config';
	import type { School } from '$lib/types/dashboard';

	let query = $state('');
	let results = $state<School[]>([]);
	let loading = $state(false);
	let open = $state(false);
	let selectedIndex = $state(-1);
	let debounceTimer: ReturnType<typeof setTimeout>;

	function handleInput() {
		clearTimeout(debounceTimer);
		selectedIndex = -1;

		if (!query.trim()) {
			results = [];
			open = false;
			return;
		}

		debounceTimer = setTimeout(async () => {
			loading = true;
			try {
				const data = await fetchSchools(1, 8, query);
				results = data.items;
				open = results.length > 0;
			} catch {
				results = [];
				open = false;
			} finally {
				loading = false;
			}
		}, 300);
	}

	function handleKeydown(e: KeyboardEvent) {
		if (!open) return;

		if (e.key === 'ArrowDown') {
			e.preventDefault();
			selectedIndex = Math.min(selectedIndex + 1, results.length - 1);
		} else if (e.key === 'ArrowUp') {
			e.preventDefault();
			selectedIndex = Math.max(selectedIndex - 1, 0);
		} else if (e.key === 'Enter' && selectedIndex >= 0) {
			e.preventDefault();
			const school = results[selectedIndex];
			if (school) navigateToSchool(school);
		} else if (e.key === 'Escape') {
			open = false;
		}
	}

	function navigateToSchool(school: School) {
		open = false;
		query = school.name;
		results = [];
		goto(resolve(schoolPath(school.id)));
	}

	function formatPassRate(rate: number): string {
		return `${rate.toFixed(1)}%`;
	}

	function handleBlur() {
		// Delay close to allow click on result
		setTimeout(() => {
			open = false;
		}, 200);
	}
</script>

<div class="relative">
	<div
		class="flex items-center gap-1.5 rounded-lg border bg-muted/50 px-3 py-1.5 transition-colors focus-within:bg-accent focus-within:text-accent-foreground focus-within:ring-2 focus-within:ring-ring"
	>
		{#if loading}
			<Loader2 class="size-4 shrink-0 animate-spin text-muted-foreground" />
		{:else}
			<Search class="size-4 shrink-0 text-muted-foreground" />
		{/if}
		<input
			type="text"
			bind:value={query}
			oninput={handleInput}
			onkeydown={handleKeydown}
			onfocus={() => {
				if (results.length > 0) open = true;
			}}
			onblur={handleBlur}
			placeholder="Search schools..."
			class="w-40 bg-transparent text-sm outline-none placeholder:text-muted-foreground md:w-56"
			role="combobox"
			aria-expanded={open}
			aria-autocomplete="list"
		/>
		<kbd
			class="pointer-events-none hidden h-5 shrink-0 items-center rounded border bg-background px-1 font-mono text-[10px] font-medium text-muted-foreground sm:inline-flex"
		>
			/
		</kbd>
	</div>

	{#if open}
		<div
			class="absolute top-full left-0 z-50 mt-1 w-80 rounded-lg border bg-popover p-1 shadow-lg md:w-96"
			role="listbox"
		>
			{#if results.length === 0 && !loading}
				<div class="px-3 py-6 text-center text-sm text-muted-foreground">No schools found</div>
			{:else}
				{#each results as school, i (school.id)}
					<button
						class="flex w-full items-start gap-3 rounded-md px-3 py-2.5 text-left transition-colors hover:bg-accent"
						class:bg-accent={i === selectedIndex}
						role="option"
						aria-selected={i === selectedIndex}
						onclick={() => navigateToSchool(school)}
						onmouseenter={() => (selectedIndex = i)}
					>
						<SchoolIcon class="mt-0.5 size-4 shrink-0 text-muted-foreground" />
						<div class="min-w-0 flex-1">
							<div class="truncate text-sm font-medium">{school.name}</div>
							<div class="flex items-center gap-3 text-xs text-muted-foreground">
								<span class="inline-flex items-center gap-1">
									<MapPin class="size-3" />
									{school.province}
								</span>
								<span class="inline-flex items-center gap-1">
									<TrendingUp class="size-3" />
									{formatPassRate(school.passRate)}
								</span>
							</div>
						</div>
					</button>
				{/each}
			{/if}
		</div>
	{/if}
</div>
