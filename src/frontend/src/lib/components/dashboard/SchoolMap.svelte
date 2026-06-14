<script lang="ts">
	import { Card, CardContent, CardHeader, CardTitle } from '$lib/components/ui/card';
	import { Skeleton } from '$lib/components/ui/skeleton';
	import { Badge } from '$lib/components/ui/badge';
	import { fetchSchools } from '$lib/api/dashboard';
	import { onMount } from 'svelte';
	import { Map } from '@lucide/svelte';

	interface ProvinceData {
		name: string;
		schools: number;
		avgPassRate: number;
		totalWrote: number;
		totalPassed: number;
	}

	const provinceIds: Record<string, string> = {
		'Western Cape': 'WC',
		'Eastern Cape': 'EC',
		'Free State': 'FS',
		Gauteng: 'GT',
		'KwaZulu-Natal': 'KZ',
		Limpopo: 'NP',
		Mpumalanga: 'MP',
		'Northern Cape': 'NC',
		'North West': 'NW'
	};

	const provinceNamesById: Record<string, string> = Object.fromEntries(
		Object.entries(provinceIds).map(([name, id]) => [id, name])
	);

	const mapAsset = '/sa_provinces.svg';
	const provinceOrder = ['WC', 'EC', 'FS', 'GT', 'KZ', 'NP', 'MP', 'NC', 'NW'];

	let { selectedProvince = $bindable(null) }: { selectedProvince?: string | null } = $props();
	let loading = $state(true);
	let provinces: ProvinceData[] = $state([]);
	let svgMarkup = $state('');
	let mapHost: HTMLDivElement | null = $state(null);

	onMount(async () => {
		try {
			const [svgResponse, schoolsResponse] = await Promise.all([
				fetch(mapAsset),
				fetchSchools(1, 1000)
			]);

			svgMarkup = await svgResponse.text();

			const grouped: Record<string, { schools: number; wrote: number; passed: number }> = {};
			for (const s of schoolsResponse.items) {
				const existing = grouped[s.province] ?? { schools: 0, wrote: 0, passed: 0 };
				existing.schools++;
				existing.wrote += s.totalWrote;
				existing.passed += s.totalPassed;
				grouped[s.province] = existing;
			}

			provinces = Object.entries(grouped).map(([name, d]) => ({
				name,
				schools: d.schools,
				totalWrote: d.wrote,
				totalPassed: d.passed,
				avgPassRate: d.wrote > 0 ? Math.round((d.passed / d.wrote) * 1000) / 10 : 0
			}));
		} catch {
			// silently fail
		} finally {
			loading = false;
		}
	});

	let provinceLookup = $derived.by(
		() =>
			Object.fromEntries(provinces.map((province) => [province.name, province])) as Record<
				string,
				ProvinceData
			>
	);

	let overallPassRate = $derived.by(() => {
		const totalWrote = provinces.reduce((sum, province) => sum + province.totalWrote, 0);
		const totalPassed = provinces.reduce((sum, province) => sum + province.totalPassed, 0);
		return totalWrote > 0 ? Math.round((totalPassed / totalWrote) * 1000) / 10 : 0;
	});

	let totalSchools = $derived(provinces.reduce((sum, province) => sum + province.schools, 0));

	function provinceFill(id: string): string {
		const name = provinceNamesById[id] ?? '';
		const province = provinceLookup[name];
		if (!province) return 'hsl(var(--muted))';
		if (selectedProvince === name) return 'hsl(var(--primary))';
		const ratio = province.avgPassRate / 100;
		if (ratio >= 0.85) return '#16a34a';
		if (ratio >= 0.7) return '#22c55e';
		if (ratio >= 0.55) return '#eab308';
		if (ratio >= 0.4) return '#f97316';
		return '#ef4444';
	}

	function syncSelection() {
		const svg = mapHost?.querySelector('svg');
		if (!svg) return;

		for (const id of provinceOrder) {
			const path = svg.querySelector<SVGPathElement>(`#${id}`);
			if (!path) continue;
			path.style.fill = provinceFill(id);
			path.style.opacity =
				selectedProvince && selectedProvince !== provinceNamesById[id] ? '0.45' : '1';
			path.style.strokeWidth = selectedProvince === provinceNamesById[id] ? '4' : '2';
			path.style.cursor = 'pointer';
		}
	}

	$effect(() => {
		selectedProvince;
		if (!svgMarkup) return;
		void syncSelection();
	});

	function handleMapClick(event: MouseEvent) {
		const target = event.target as Element | null;
		const id = target?.id;
		const provinceName = provinceNamesById[id ?? ''];
		if (!id || !provinceName) return;
		selectedProvince = selectedProvince === provinceName ? null : provinceName;
		syncSelection();
	}
</script>

<Card class="h-full">
	<CardHeader class="pb-3">
		<CardTitle class="flex items-center gap-2 text-base">
			<Map class="size-4" />
			Schools Map
		</CardTitle>
	</CardHeader>
	<CardContent class="flex flex-col gap-4 p-4">
		{#if loading}
			<div class="flex items-center justify-center py-12">
				<Skeleton class="h-80 w-full" />
			</div>
		{:else}
			<div class="overflow-hidden rounded-xl border bg-background shadow-sm">
				<div
					bind:this={mapHost}
					onclick={handleMapClick}
					onkeydown={(event) => {
						if (event.key === 'Enter' || event.key === ' ') {
							event.preventDefault();
							handleMapClick(event as unknown as MouseEvent);
						}
					}}
					class="sa-map"
					role="button"
					tabindex="0"
					aria-label="South Africa provinces map"
				>
					{@html svgMarkup.replace(
						'<svg',
						'<svg class="h-auto w-full" style="width:100%;height:auto" role="img" aria-label="South Africa provinces map"'
					)}
				</div>
			</div>

			<div class="grid gap-2 sm:grid-cols-4">
				<div class="rounded-lg border bg-card p-3">
					<div class="text-xs text-muted-foreground">Schools</div>
					<div class="text-lg font-semibold tabular-nums">{totalSchools.toLocaleString()}</div>
				</div>
				<div class="rounded-lg border bg-card p-3">
					<div class="text-xs text-muted-foreground">Pass rate</div>
					<div class="text-lg font-semibold tabular-nums">{overallPassRate}%</div>
				</div>
				<div class="rounded-lg border bg-card p-3 sm:col-span-2">
					<div class="text-xs text-muted-foreground">Selected province</div>
					<div class="text-lg font-semibold">{selectedProvince ?? 'All provinces'}</div>
				</div>
			</div>
		{/if}
	</CardContent>
</Card>

<style>
	.sa-map :global(#WC),
	.sa-map :global(#EC),
	.sa-map :global(#FS),
	.sa-map :global(#GT),
	.sa-map :global(#KZ),
	.sa-map :global(#NP),
	.sa-map :global(#MP),
	.sa-map :global(#NC),
	.sa-map :global(#NW) {
		transition:
			fill 180ms ease,
			opacity 180ms ease,
			stroke-width 180ms ease;
	}
</style>
