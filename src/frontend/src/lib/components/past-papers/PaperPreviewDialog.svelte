<script lang="ts">
	import * as Dialog from '$lib/components/ui/dialog';
	import { Button } from '$lib/components/ui/button';
	import { pastPaperFileUrl } from '$lib/api/past-papers';
	import { cn } from '$lib/utils';
	import type { PastPaper } from '$lib/types/past-papers';
	import { Download, ExternalLink, FileWarning, LoaderCircle, RotateCw } from '@lucide/svelte';
	import { languageLabels, paperLabel, sessionLabels } from './papers';

	interface Props {
		/** Paper to preview; the dialog is open while this is set. */
		paper: PastPaper | null;
		/** Show the memo first instead of the question paper. */
		startWithMemo?: boolean;
		onClose: () => void;
	}

	let { paper, startWithMemo = false, onClose }: Props = $props();

	type Tab = 'paper' | 'memo';
	type LoadState =
		| { status: 'loading' }
		| { status: 'ready'; url: string }
		| { status: 'error'; message: string };

	let tab = $state<Tab>('paper');
	// Blob URLs cached per document id so switching between the paper and its
	// memo reuses an already-loaded document instead of refetching.
	let cache = $state<Map<string, LoadState>>(new Map());
	let doc = $state<LoadState>({ status: 'loading' });
	let attempt = $state(0);

	// Reset the tab and clear the document cache whenever a different paper is
	// opened (or the dialog is closed and reopened), so blobs from a previous
	// paper are not reused or leaked.
	let paperId = $state<string | null>(null);
	$effect.pre(() => {
		if (!paper) return;
		if (paperId !== paper.id) {
			paperId = paper.id;
			cache = new Map();
			doc = { status: 'loading' };
		}
		tab = startWithMemo && paper.memoId ? 'memo' : 'paper';
	});

	const fileId = $derived(
		paper ? (tab === 'memo' && paper.memoId ? paper.memoId : paper.id) : null
	);
	const title = $derived(paper ? `${paper.subject} · ${paperLabel(paper)}` : '');
	const subtitle = $derived(
		paper
			? [
					`${sessionLabels[paper.session]} ${paper.year}`,
					`Grade ${paper.grade}`,
					paper.language ? languageLabels[paper.language] : null
				]
					.filter(Boolean)
					.join(' · ')
			: ''
	);
	const loading = $derived(doc.status === 'loading');

	// The API forbids framing its responses, so the PDF is fetched and shown
	// from a local blob URL. Each load owns its URL and revokes it on cleanup.
	$effect(() => {
		const id = fileId;
		void attempt;
		if (!id) return;

		const cached = cache.get(id);
		if (cached && (cached.status === 'ready' || cached.status === 'error')) {
			doc = cached;
			return;
		}

		const controller = new AbortController();
		let objectUrl: string | null = null;
		const loadingDoc: LoadState = { status: 'loading' };
		doc = loadingDoc;
		cache.set(id, loadingDoc);

		fetch(pastPaperFileUrl(id), { signal: controller.signal })
			.then(async (response) => {
				if (response.status === 429) {
					throw new Error('Too many previews in a short time. Wait a minute and try again.');
				}
				if (!response.ok) throw new Error('This document could not be loaded.');
				if (!response.headers.get('content-type')?.startsWith('application/pdf')) {
					throw new Error('This file cannot be previewed. Download it instead.');
				}
				const blob = await response.blob();
				objectUrl = URL.createObjectURL(new Blob([blob], { type: 'application/pdf' }));
				const ready: LoadState = { status: 'ready', url: objectUrl };
				cache.set(id, ready);
				if (fileId === id) doc = ready;
			})
			.catch((error: unknown) => {
				if (controller.signal.aborted) return;
				const failed: LoadState = {
					status: 'error',
					message: error instanceof Error ? error.message : 'This document could not be loaded.'
				};
				cache.set(id, failed);
				if (fileId === id) doc = failed;
			});

		return () => {
			controller.abort();
		};
	});
</script>

<Dialog.Root
	open={paper !== null}
	onOpenChange={(open) => {
		if (!open) onClose();
	}}
>
	<Dialog.Content
		class="flex h-[92dvh] w-full max-w-[calc(100%-1rem)] flex-col gap-0 overflow-hidden p-0 sm:max-w-5xl"
	>
		<Dialog.Header
			class="gap-2 border-b px-3 py-2 pe-12 text-start sm:flex-row sm:items-center sm:justify-between sm:gap-4"
		>
			<div class="min-w-0">
				<Dialog.Title class="truncate text-sm leading-5 font-semibold">{title}</Dialog.Title>
				<Dialog.Description class="text-xs leading-4">{subtitle}</Dialog.Description>
			</div>

			<div class="flex shrink-0 flex-wrap items-center gap-2">
				{#if paper?.memoId}
					<div
						class="flex items-center rounded-md border bg-background p-0.5 shadow-xs"
						role="group"
						aria-label="Document"
					>
						{#each [{ value: 'paper', label: 'Question paper' }, { value: 'memo', label: 'Memo' }] as option (option.value)}
							<button
								type="button"
								aria-pressed={tab === option.value}
								disabled={loading}
								onclick={() => (tab = option.value as Tab)}
								class={cn(
									'h-7 rounded-[4px] px-2.5 text-xs font-medium transition-colors disabled:cursor-wait disabled:opacity-60',
									tab === option.value
										? 'bg-primary text-primary-foreground'
										: 'text-muted-foreground hover:bg-muted'
								)}
							>
								{option.label}
							</button>
						{/each}
					</div>
				{/if}

				<div class="ms-auto flex items-center gap-1.5">
					{#if doc.status === 'ready'}
						<Button
							variant="outline"
							size="sm"
							class="h-7 gap-1.5 text-xs"
							href={doc.url}
							target="_blank"
							rel="noopener"
						>
							<ExternalLink class="size-3.5" />
							Open in new tab
						</Button>
					{/if}
					{#if fileId}
						<Button
							variant="outline"
							size="sm"
							class="h-7 gap-1.5 text-xs"
							href={pastPaperFileUrl(fileId, true)}
							download
						>
							<Download class="size-3.5" />
							Download
						</Button>
					{/if}
				</div>
			</div>
		</Dialog.Header>

		<div class="relative min-h-0 flex-1 bg-muted/40">
			{#if doc.status === 'loading'}
				<div
					class="absolute inset-0 flex flex-col items-center justify-center gap-2 text-xs text-muted-foreground"
					role="status"
				>
					<LoaderCircle class="size-5 motion-safe:animate-spin" />
					Loading document…
				</div>
			{:else if doc.status === 'error'}
				<div
					class="absolute inset-0 flex flex-col items-center justify-center gap-2 p-6 text-center"
					role="alert"
				>
					<FileWarning class="size-6 text-muted-foreground" />
					<p class="text-sm font-medium">{doc.message}</p>
					<Button variant="outline" size="sm" class="mt-1" onclick={() => attempt++}>
						<RotateCw class="size-3.5" />
						Try again
					</Button>
				</div>
			{:else}
				<iframe src={doc.url} title={`${title} (${tab})`} class="h-full w-full border-0"></iframe>
			{/if}
		</div>
	</Dialog.Content>
</Dialog.Root>
