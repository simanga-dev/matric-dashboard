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
	class="paper-card group relative flex aspect-[2/3] min-h-64 flex-col overflow-hidden rounded-lg border bg-transparent text-foreground transition-colors duration-200 hover:border-primary/30"
>
	<div class="absolute inset-0 overflow-hidden bg-muted/40">
		{#if previewable}
			<button
				type="button"
				class="block h-full w-full focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-ring"
				onclick={() => onPreview(paper)}
				aria-label={`Preview ${title}, ${session}`}
			>
				<PaperThumbnail {paper} variant={theme.thumbnail} fill />
			</button>
		{:else}
			<PaperThumbnail {paper} variant={theme.thumbnail} fill />
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

	<div
		class="relative mt-auto flex flex-col bg-background/80 p-2 backdrop-blur-sm transition-colors group-hover:bg-background/90 focus-within:bg-background/90"
	>
		<h3 class="line-clamp-2 text-[13px] leading-4 font-medium tracking-tight">
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

		<div class="paper-details">
			<div class="min-h-0 overflow-hidden">
				<dl class="space-y-0.5 text-[11px] leading-[14px] text-muted-foreground">
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
						<dd class="truncate">
							{paper.fileType.toUpperCase()} · {formatFileSize(paper.sizeBytes)}
						</dd>
					</div>
				</dl>

				<div class="mt-auto flex items-center gap-0.5 pt-1">
					<Button
						variant="ghost"
						size="icon-sm"
						class="size-6 rounded-[4px]"
						disabled={!previewable}
						onclick={() => onPreview(paper)}
						aria-label={previewable ? `Preview ${title}` : `Preview not available for ${title}`}
						title={previewable ? 'Preview' : 'Preview not available for this file type'}
					>
						<Eye class="size-3.5" />
					</Button>
					<Button
						variant="ghost"
						size="icon-sm"
						class="size-6 rounded-[4px]"
						href={pastPaperFileUrl(paper.id, true)}
						download
						aria-label={`Download ${title}`}
						title="Download"
					>
						<Download class="size-3.5" />
					</Button>
					{#if isQuestionPaper}
						<Button
							variant="ghost"
							size="icon-sm"
							class="size-6 rounded-[4px]"
							disabled={!paper.memoId}
							onclick={() => onMemo(paper)}
							aria-label={paper.memoId
								? `Open memo for ${title}`
								: `No memo available for ${title}`}
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
		</div>
	</div>
</article>

<style>
	.paper-details {
		display: grid;
		grid-template-rows: 1fr;
		opacity: 1;
		transition:
			grid-template-rows 220ms ease,
			opacity 180ms ease;
	}

	.paper-details dl {
		padding-top: 4px;
	}

	@media (hover: hover) and (pointer: fine) {
		.paper-details {
			grid-template-rows: 0fr;
			opacity: 0;
		}

		.paper-card:hover .paper-details,
		.paper-card:focus-within .paper-details {
			grid-template-rows: 1fr;
			opacity: 1;
		}
	}

	@media (prefers-reduced-motion: reduce) {
		.paper-details {
			transition: none;
		}
	}
</style>
