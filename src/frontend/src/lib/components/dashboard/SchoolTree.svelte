<script lang="ts">
	import { Card, CardContent, CardHeader, CardTitle } from '$lib/components/ui/card';
	import { Skeleton } from '$lib/components/ui/skeleton';
	import { Badge } from '$lib/components/ui/badge';
	import { fetchSchools } from '$lib/api/dashboard';
	import {
		ChevronRight,
		ChevronDown,
		School as SchoolIcon,
		Building2,
		MapPin
	} from '@lucide/svelte';
	import { onMount } from 'svelte';
	import type { School } from '$lib/types/dashboard';

	let {
		selectedSchoolId = $bindable(null),
		selectedProvince = $bindable(null)
	}: { selectedSchoolId?: number | null; selectedProvince?: string | null } = $props();

	let schools: School[] = $state([]);
	let loading = $state(true);
	let expandedProvinces = $state<Set<string>>(new Set());
	let expandedCircuits = $state<Set<string>>(new Set());

	onMount(async () => {
		try {
			const data = await fetchSchools(1, 1000);
			schools = data.items;
		} catch {
			// silently fail
		} finally {
			loading = false;
		}
	});

	let grouped = $derived.by(() => {
		const map = new Map<string, Map<string, School[]>>();
		for (const s of schools) {
			if (!map.has(s.province)) {
				map.set(s.province, new Map());
			}
			const circuitMap = map.get(s.province)!;
			if (!circuitMap.has(s.circuit)) {
				circuitMap.set(s.circuit, []);
			}
			circuitMap.get(s.circuit)!.push(s);
		}
		return map;
	});

	function toggleProvince(province: string) {
		if (expandedProvinces.has(province)) {
			expandedProvinces.delete(province);
			expandedProvinces = new Set(expandedProvinces);
		} else {
			expandedProvinces.add(province);
			expandedProvinces = new Set(expandedProvinces);
		}
	}

	function toggleCircuit(circuit: string) {
		if (expandedCircuits.has(circuit)) {
			expandedCircuits.delete(circuit);
			expandedCircuits = new Set(expandedCircuits);
		} else {
			expandedCircuits.add(circuit);
			expandedCircuits = new Set(expandedCircuits);
		}
	}

	function selectSchool(school: School) {
		selectedSchoolId = selectedSchoolId === school.id ? null : school.id;
	}

	function selectProvince(province: string) {
		selectedProvince = selectedProvince === province ? null : province;
	}
</script>

<Card class="h-full">
	<CardHeader class="pb-3">
		<CardTitle class="flex items-center gap-2 text-base">
			<Building2 class="size-4" />
			Schools Hierarchy
		</CardTitle>
	</CardHeader>
	<CardContent class="overflow-y-auto p-0">
		{#if loading}
			<div class="space-y-2 p-4">
				<Skeleton class="h-5 w-full" />
				<Skeleton class="h-5 w-3/4" />
				<Skeleton class="h-5 w-5/6" />
			</div>
		{:else}
			<div class="divide-y p-0">
				{#each [...grouped.entries()] as [province, circuits]}
					<div>
						<button
							onclick={() => {
								toggleProvince(province);
								selectProvince(province);
							}}
							data-selected={selectedProvince === province}
							class="flex w-full items-center gap-2 px-4 py-2 text-left text-sm font-medium transition-colors hover:bg-muted/50 data-[selected=true]:bg-primary/10 data-[selected=true]:text-primary"
						>
							{#if expandedProvinces.has(province)}
								<ChevronDown class="size-3.5 shrink-0 text-muted-foreground" />
							{:else}
								<ChevronRight class="size-3.5 shrink-0 text-muted-foreground" />
							{/if}
							<MapPin class="size-3.5 shrink-0 text-muted-foreground" />
							<span class="truncate">{province}</span>
							<Badge variant="secondary" class="ml-auto shrink-0 text-xs">
								{[...circuits.values()].reduce((sum, s) => sum + s.length, 0)}
							</Badge>
						</button>
						{#if expandedProvinces.has(province)}
							<div class="ml-5 border-l-2 border-muted">
								{#each [...circuits.entries()] as [circuit, schoolList]}
									<div>
										<button
											onclick={() => toggleCircuit(circuit)}
											class="flex w-full items-center gap-2 py-1.5 pr-4 pl-3 text-left text-xs text-muted-foreground transition-colors hover:bg-muted/50"
										>
											{#if expandedCircuits.has(circuit)}
												<ChevronDown class="size-3 shrink-0" />
											{:else}
												<ChevronRight class="size-3 shrink-0" />
											{/if}
											<span class="truncate">{circuit}</span>
											<Badge variant="outline" class="ml-auto shrink-0 px-1.5 text-[10px]">
												{schoolList.length}
											</Badge>
										</button>
										{#if expandedCircuits.has(circuit)}
											<div class="ml-3 border-l border-muted">
												{#each schoolList as school}
													<button
														onclick={() => selectSchool(school)}
														data-selected={selectedSchoolId === school.id}
														class="flex w-full items-center gap-2 px-4 py-1.5 text-left text-xs transition-colors hover:bg-muted/50 data-[selected=true]:bg-primary/10 data-[selected=true]:text-primary"
													>
														<SchoolIcon class="size-3 shrink-0 text-muted-foreground" />
														<span class="truncate">{school.name}</span>
													</button>
												{/each}
											</div>
										{/if}
									</div>
								{/each}
							</div>
						{/if}
					</div>
				{/each}
			</div>
		{/if}
	</CardContent>
</Card>
