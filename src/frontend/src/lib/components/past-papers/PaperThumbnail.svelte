<script lang="ts">
	import { cn } from '$lib/utils';
	import type { PastPaper } from '$lib/types/past-papers';
	import { FileText } from '@lucide/svelte';
	import { paperLabel, type ThumbnailVariant } from './papers';

	interface Props {
		paper: PastPaper;
		variant?: ThumbnailVariant;
		compact?: boolean;
		fill?: boolean;
	}

	let { paper, compact = false, fill = false }: Props = $props();
	let failed = $state(false);
	$effect(() => {
		void paper.id;
		failed = false;
	});
</script>

<div
	class={cn(
		'flex h-full w-full items-start justify-center overflow-hidden bg-muted/40',
		fill ? 'p-0' : compact ? 'px-0.5 pt-1' : 'px-2 pt-2.5'
	)}
>
	{#if paper.fileType.toLowerCase() === 'pdf' && !failed}
		<img
			src={`/api/v1/past-papers/${encodeURIComponent(paper.id)}/thumbnail`}
			alt={`First page of ${paper.subject} ${paperLabel(paper)}, ${paper.year}`}
			loading="lazy"
			decoding="async"
			onerror={() => (failed = true)}
			class={cn(
				'bg-white shadow-sm transition-transform duration-300 group-hover:scale-[1.02]',
				fill
					? 'h-full w-full object-cover object-top'
					: compact
						? 'h-auto w-[94%]'
						: 'h-auto w-[80%]'
			)}
		/>
	{:else}
		<div
			class="flex h-full flex-col items-center justify-center gap-2 text-xs text-muted-foreground"
		>
			<FileText class="size-8" aria-hidden="true" />
			<span>Preview unavailable</span>
		</div>
	{/if}
</div>
