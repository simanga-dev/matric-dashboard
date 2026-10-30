<script lang="ts">
	import { Button } from '$lib/components/ui/button';
	import { pastPaperFileUrl } from '$lib/api/past-papers';
	import { cn } from '$lib/utils';
	import type { PastPaper } from '$lib/types/past-papers';
	import { CalendarDays, Download, Eye, FileCheck, FileText, HardDrive } from '@lucide/svelte';
	import PaperThumbnail from './PaperThumbnail.svelte';
	import {
		categoryStyles,
		formatFileSize,
		isPreviewable,
		kindLabels,
		languageLabels,
		paperLabel,
		sessionLabels,
		subjectCategory
	} from './papers';

	interface Props {
		paper: PastPaper;
		onPreview: (paper: PastPaper) => void;
		onMemo: (paper: PastPaper) => void;
	}

	let { paper, onPreview, onMemo }: Props = $props();

	const theme = $derived(categoryStyles[subjectCategory(paper.subject)]);
	const title = $derived(`${paper.subject} · ${paperLabel(paper)}`);
	const session = $derived(`${sessionLabels[paper.session]} ${paper.year}`);
	const previewable = $derived(isPreviewable(paper));
	const isQuestionPaper = $derived(paper.kind === 'QuestionPaper');
</script>

<article
	class="group flex h-full flex-col overflow-hidden rounded-lg border bg-card text-card-foreground shadow-xs transition-all duration-200 hover:border-primary/30 hover:shadow-md motion-safe:hover:-translate-y-0.5"
>
	<div class="relative aspect-[5/4] overflow-hidden border-b bg-muted/40">
		{#if previewable}
			<button
				type="button"
				class="block h-full w-full focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-ring"
				onclick={() => onPreview(paper)}
				aria-label={`Preview ${title}, ${session}`}
			>
				<PaperThumbnail {paper} variant={theme.thumbnail} />
			</button>
		{:else}
			<PaperThumbnail {paper} variant={theme.thumbnail} />
		{/if}

		<div class="pointer-events-none absolute top-1.5 right-1.5 flex flex-col items-end gap-1">
			<span
				class={cn(
					'rounded-[3px] px-1.5 py-1 text-[9px] leading-none font-bold tracking-wide uppercase shadow-sm',
					theme.badgeClass
				)}
			>
				{theme.label}
			</span>
			{#if !isQuestionPaper}
				<span
					class="rounded-[3px] bg-slate-700 px-1.5 py-1 text-[9px] leading-none font-bold tracking-wide text-white uppercase shadow-sm"
				>
					{kindLabels[paper.kind]}
				</span>
			{:else if paper.memoId}
				<span
					class="rounded-[3px] bg-emerald-600 px-1.5 py-1 text-[9px] leading-none font-bold tracking-wide text-white uppercase shadow-sm"
				>
					Memo
				</span>
			{:else}
				<span
					class="rounded-[3px] bg-rose-600 px-1.5 py-1 text-[9px] leading-none font-bold tracking-wide text-white uppercase shadow-sm"
				>
					No memo
				</span>
			{/if}
		</div>
	</div>

	<div class="flex flex-1 flex-col gap-1.5 p-2.5">
		<h3 class="line-clamp-2 min-h-8 text-[13px] leading-snug font-medium tracking-tight">
			{#if previewable}
				<button
					type="button"
					class="text-left transition-colors hover:text-primary hover:underline focus-visible:text-primary focus-visible:underline"
					onclick={() => onPreview(paper)}
				>
					{title}
				</button>
			{:else}
				{title}
			{/if}
		</h3>

		<dl class="space-y-0.5 text-[11px] text-muted-foreground">
			<div class="flex items-center gap-1.5">
				<FileText class="size-3 shrink-0" aria-hidden="true" />
				<dt class="sr-only">Document</dt>
				<dd class="truncate">
					{kindLabels[paper.kind]}{paper.language ? ` · ${languageLabels[paper.language]}` : ''}
				</dd>
			</div>
			<div class="flex items-center gap-1.5">
				<CalendarDays class="size-3 shrink-0" aria-hidden="true" />
				<dt class="sr-only">Session</dt>
				<dd class="truncate">{session} · Grade {paper.grade}</dd>
			</div>
			<div class="flex items-center gap-1.5">
				<HardDrive class="size-3 shrink-0" aria-hidden="true" />
				<dt class="sr-only">File</dt>
				<dd class="truncate">{paper.fileType.toUpperCase()} · {formatFileSize(paper.sizeBytes)}</dd>
			</div>
		</dl>

		<div class="mt-auto flex items-center gap-1 border-t pt-2">
			<Button
				variant="outline"
				size="icon-sm"
				class="size-7 rounded-[4px]"
				disabled={!previewable}
				onclick={() => onPreview(paper)}
				aria-label={previewable ? `Preview ${title}` : `Preview not available for ${title}`}
				title={previewable ? 'Preview' : 'Preview not available for this file type'}
			>
				<Eye class="size-3.5" />
			</Button>
			<Button
				variant="outline"
				size="icon-sm"
				class="size-7 rounded-[4px]"
				href={pastPaperFileUrl(paper.id, true)}
				download
				aria-label={`Download ${title}`}
				title="Download"
			>
				<Download class="size-3.5" />
			</Button>
			{#if isQuestionPaper}
				<Button
					variant="outline"
					size="icon-sm"
					class="size-7 rounded-[4px]"
					disabled={!paper.memoId}
					onclick={() => onMemo(paper)}
					aria-label={paper.memoId ? `Open memo for ${title}` : `No memo available for ${title}`}
					title={paper.memoId ? 'Open memo' : 'No memo available'}
				>
					<FileCheck class="size-3.5" />
				</Button>
			{/if}

			<span
				class="ms-auto hidden items-center gap-1 text-[10px] text-muted-foreground sm:inline-flex"
			>
				<span class={cn('size-1.5 rounded-full', theme.dotClass)} aria-hidden="true"></span>
				{paper.grade === 12 ? 'NSC' : `Gr ${paper.grade}`}
			</span>
		</div>
	</div>
</article>
