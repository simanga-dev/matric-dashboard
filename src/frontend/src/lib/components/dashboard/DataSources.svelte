<script lang="ts">
	import * as Dialog from '$lib/components/ui/dialog';
	import * as m from '$lib/paraglide/messages';
	import {
		ArrowRight,
		ArrowUpRight,
		Building2,
		Database,
		FileText,
		GraduationCap,
		Info,
		Landmark,
		Layers,
		MapPinned
	} from '@lucide/svelte';

	let open = $state(false);

	const dbeReportsUrl = 'https://www.education.gov.za/Resources/Reports.aspx';
	const boundariesUrl = 'https://www.geoboundaries.org/';
	const osmCopyrightUrl = 'https://www.openstreetmap.org/copyright';

	const sources = [
		{
			icon: GraduationCap,
			title: () => m.dataSources_resultsTitle(),
			body: () => m.dataSources_resultsBody(),
			href: dbeReportsUrl,
			linkLabel: () => m.dataSources_resultsLink()
		},
		{
			icon: Building2,
			title: () => m.dataSources_directoryTitle(),
			body: () => m.dataSources_directoryBody(),
			href: dbeReportsUrl,
			linkLabel: () => m.dataSources_directoryLink()
		},
		{
			icon: MapPinned,
			title: () => m.dataSources_locationsTitle(),
			body: () => m.dataSources_locationsBody(),
			href: boundariesUrl,
			linkLabel: () => m.dataSources_locationsLink()
		},
		{
			icon: Layers,
			title: () => m.dataSources_basemapTitle(),
			body: () => m.dataSources_basemapBody(),
			href: osmCopyrightUrl,
			linkLabel: () => m.dataSources_basemapLink()
		}
	];

	const flow = [
		{ icon: FileText, label: () => m.dataSources_flowReports() },
		{ icon: Database, label: () => m.dataSources_flowClean() },
		{ icon: MapPinned, label: () => m.dataSources_flowDashboard() }
	];
</script>

<footer
	class="source-bar flex min-h-10 shrink-0 items-center gap-2 border-t bg-card px-3 text-xs text-muted-foreground"
>
	<div class="flex min-w-0 flex-1 items-center gap-1.5">
		<Landmark class="size-3.5 shrink-0 text-primary" aria-hidden="true" />
		<span class="truncate" title={`${m.dataSources_source()}: ${m.dataSources_dbe()}`}>
			<span class="sr-only">{m.dataSources_source()}: </span>
			<a
				href={dbeReportsUrl}
				target="_blank"
				rel="noreferrer"
				class="font-medium text-foreground underline-offset-4 hover:underline"
			>
				{m.dataSources_dbe()}
			</a>
		</span>
	</div>
	<button
		type="button"
		aria-haspopup="dialog"
		aria-expanded={open}
		onclick={() => (open = true)}
		class="inline-flex min-h-8 shrink-0 items-center gap-1.5 rounded-md px-1.5 font-medium text-foreground transition-colors hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
	>
		<Info class="size-3.5" aria-hidden="true" />
		{m.dataSources_trigger()}
	</button>
</footer>

<Dialog.Root bind:open>
	<Dialog.Content class="gap-0 overflow-hidden p-0 sm:max-w-2xl">
		<Dialog.Header class="border-b bg-muted/30 px-5 py-5 sm:px-6">
			<Dialog.Title>{m.dataSources_title()}</Dialog.Title>
			<Dialog.Description class="text-pretty">
				{m.dataSources_description()}
			</Dialog.Description>
		</Dialog.Header>

		<div class="max-h-[min(65vh,38rem)] overflow-y-auto px-5 py-5 sm:px-6">
			<ol
				aria-label={m.dataSources_flowLabel()}
				class="grid grid-cols-1 gap-2 text-xs sm:grid-cols-[1fr_auto_1fr_auto_1fr] sm:items-stretch"
			>
				{#each flow as step, index (step.label())}
					{#if index > 0}
						<li
							class="hidden justify-center text-muted-foreground sm:flex sm:self-center"
							aria-hidden="true"
						>
							<ArrowRight class="size-3.5" />
						</li>
					{/if}
					<li
						class="flex items-center gap-2 rounded-lg border bg-muted/40 px-3 py-2 font-medium text-foreground"
					>
						<step.icon class="size-4 shrink-0 text-primary" aria-hidden="true" />
						<span>{step.label()}</span>
					</li>
				{/each}
			</ol>

			<div class="mt-4 divide-y overflow-hidden rounded-xl border">
				{#each sources as source (source.title())}
					<div class="flex gap-3 p-4">
						<span
							class="mt-0.5 flex size-9 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary"
						>
							<source.icon class="size-4" aria-hidden="true" />
						</span>
						<div class="min-w-0">
							<h3 class="text-sm font-semibold">{source.title()}</h3>
							<p class="mt-1 text-xs leading-relaxed text-pretty text-muted-foreground">
								{source.body()}
							</p>
							<!-- eslint-disable svelte/no-navigation-without-resolve -- source links are external -->
							<a
								href={source.href}
								target="_blank"
								rel="noreferrer"
								class="mt-2 inline-flex items-center gap-1 text-xs font-medium text-primary underline-offset-4 hover:underline"
							>
								{source.linkLabel()}
								<ArrowUpRight class="size-3" aria-hidden="true" />
							</a>
						</div>
					</div>
				{/each}
			</div>
		</div>

		<Dialog.Footer
			class="flex-row items-start gap-2 border-t bg-muted/30 px-5 py-3 text-start text-xs leading-relaxed text-muted-foreground sm:px-6"
		>
			<Info class="mt-0.5 size-3.5 shrink-0" aria-hidden="true" />
			<p class="text-pretty">{m.dataSources_note()}</p>
		</Dialog.Footer>
	</Dialog.Content>
</Dialog.Root>
