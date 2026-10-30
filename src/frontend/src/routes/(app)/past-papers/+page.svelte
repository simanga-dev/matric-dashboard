<script lang="ts">
	import { goto, invalidateAll } from '$app/navigation';
	import { navigating } from '$app/state';
	import { resolve } from '$app/paths';
	import * as Card from '$lib/components/ui/card';
	import { Button } from '$lib/components/ui/button';
	import * as DropdownMenu from '$lib/components/ui/dropdown-menu';
	import { cn } from '$lib/utils';
	import { pastPaperFileUrl } from '$lib/api/past-papers';
	import {
		PastPaperCard,
		PaperPreviewDialog,
		FilterMenu,
		PaperThumbnail,
		categoryStyles,
		formatFileSize,
		isPreviewable,
		kindLabels,
		languageLabels,
		paperLabel,
		sessionLabels,
		subjectCategory
	} from '$lib/components/past-papers';
	import {
		ALL_KINDS,
		DEFAULT_KIND,
		hasActiveFilters,
		pastPapersHref,
		type PastPaperFilters
	} from '$lib/components/past-papers/papers-url';
	import type { PastPaper, PastPaperSort } from '$lib/types/past-papers';
	import {
		ArrowUpDown,
		BookOpen,
		CalendarDays,
		Check,
		ChevronDown,
		ChevronLeft,
		ChevronRight,
		Download,
		Eye,
		FileCheck,
		FileStack,
		FileText,
		Files,
		GraduationCap,
		Languages,
		LayoutGrid,
		List,
		LoaderCircle,
		RotateCw,
		Search,
		Tags,
		Timer,
		X
	} from '@lucide/svelte';
	import type { PageProps } from './$types';

	let { data }: PageProps = $props();

	const filters = $derived(data.filters);
	const papers = $derived(data.papers);
	const facets = $derived(data.facets);

	const sortOptions: { value: PastPaperSort; label: string }[] = [
		{ value: 'newest', label: 'Newest first' },
		{ value: 'oldest', label: 'Oldest first' },
		{ value: 'subject', label: 'Subject A-Z' }
	];

	const subjectOptions = $derived([
		{ value: 'all', label: 'All subjects' },
		...(facets?.subjects ?? []).map((subject) => ({ value: subject, label: subject }))
	]);
	const yearOptions = $derived([
		{ value: 'all', label: 'All years' },
		...(facets?.years ?? []).map((year) => ({ value: String(year), label: String(year) }))
	]);
	const sessionOptions = $derived([
		{ value: 'all', label: 'All sessions' },
		...(facets?.sessions ?? []).map((session) => ({
			value: session,
			label: sessionLabels[session]
		}))
	]);
	const languageOptions = $derived([
		{ value: 'all', label: 'All languages' },
		...(facets?.languages ?? []).map((language) => ({
			value: language,
			label: languageLabels[language]
		}))
	]);
	const paperOptions = $derived([
		{ value: 'all', label: 'All papers' },
		...(facets?.paperNumbers ?? []).map((n) => ({ value: String(n), label: `Paper ${n}` }))
	]);
	const gradeOptions = $derived([
		{ value: 'all', label: 'All grades' },
		...(facets?.grades ?? [])
			.toSorted((a, b) => b - a)
			.map((grade) => ({ value: String(grade), label: `Grade ${grade}` }))
	]);
	// Question papers come first because they are the default view.
	const kindOptions = $derived([
		{ value: DEFAULT_KIND, label: kindLabels[DEFAULT_KIND] },
		{ value: ALL_KINDS, label: 'All documents' },
		...(facets?.kinds ?? [])
			.filter((kind) => kind !== DEFAULT_KIND)
			.map((kind) => ({ value: kind, label: kindLabels[kind] }))
	]);

	let view = $state<'grid' | 'list'>('grid');
	let previewPaper = $state<PastPaper | null>(null);
	let previewMemo = $state(false);

	// Local copy of the search box so typing stays responsive; the URL is
	// updated after a short pause.
	let search = $derived(filters.search);

	const loading = $derived(navigating.to?.url.pathname === '/past-papers');
	const filtersActive = $derived(hasActiveFilters(filters));
	const sortLabel = $derived(
		sortOptions.find((option) => option.value === filters.sort)?.label ?? ''
	);
	const firstItem = $derived(papers ? (papers.pageNumber - 1) * papers.pageSize + 1 : 0);
	const lastItem = $derived(papers ? firstItem + papers.items.length - 1 : 0);

	function apply(changes: Partial<PastPaperFilters>, resetPage = true) {
		const next: PastPaperFilters = { ...filters, ...changes };
		if (resetPage && !('page' in changes)) next.page = 1;
		void goto(resolve(pastPapersHref(next)), {
			noScroll: !('page' in changes),
			keepFocus: true
		});
	}

	let searchTimer: ReturnType<typeof setTimeout> | undefined;
	function onSearchInput() {
		clearTimeout(searchTimer);
		searchTimer = setTimeout(() => apply({ search: search.trim() }), 300);
	}

	function toNumber(value: string): number | null {
		return value === 'all' ? null : Number(value);
	}

	function resetFilters() {
		search = '';
		apply({
			search: '',
			subject: null,
			year: null,
			session: null,
			grade: null,
			language: null,
			paperNumber: null,
			kind: DEFAULT_KIND
		});
	}

	function preview(paper: PastPaper) {
		previewMemo = false;
		previewPaper = paper;
	}

	function openMemo(paper: PastPaper) {
		previewMemo = true;
		previewPaper = paper;
	}
</script>

<svelte:head>
	<title>Past Papers • MatricDashboard</title>
	<meta
		name="description"
		content="Browse NSC past exam papers by subject and year, with memos where available."
	/>
</svelte:head>

<div class="space-y-4">
	<div class="flex flex-wrap items-start justify-between gap-3">
		<div class="min-w-0">
			<h1 class="flex items-center gap-2 text-xl font-semibold tracking-tight">
				<BookOpen class="size-5 text-primary" />
				Past Papers
			</h1>
			<p class="mt-1 text-xs text-muted-foreground">
				Browse NSC question papers and memos by subject, session and language.
			</p>
		</div>

		<div class="flex flex-wrap items-center gap-2">
			<DropdownMenu.Root>
				<DropdownMenu.Trigger>
					{#snippet child({ props })}
						<button
							type="button"
							class="inline-flex h-8 items-center gap-1.5 rounded-md border bg-background px-2.5 text-xs font-medium shadow-xs transition-colors hover:bg-accent"
							{...props}
						>
							<ArrowUpDown class="size-3.5 opacity-70" />
							<span class="text-muted-foreground">Sort</span>
							<span>{sortLabel}</span>
							<ChevronDown class="size-3 opacity-60" />
						</button>
					{/snippet}
				</DropdownMenu.Trigger>
				<DropdownMenu.Content align="end" class="w-44">
					<DropdownMenu.Label class="text-xs text-muted-foreground">Sort papers</DropdownMenu.Label>
					{#each sortOptions as option (option.value)}
						<DropdownMenu.Item class="gap-2 text-xs" onclick={() => apply({ sort: option.value })}>
							<Check
								class={cn('size-3.5', option.value === filters.sort ? 'opacity-100' : 'opacity-0')}
							/>
							{option.label}
						</DropdownMenu.Item>
					{/each}
				</DropdownMenu.Content>
			</DropdownMenu.Root>

			<div
				class="flex items-center rounded-md border bg-background p-0.5 shadow-xs"
				role="group"
				aria-label="View"
			>
				<button
					type="button"
					aria-pressed={view === 'grid'}
					aria-label="Grid view"
					onclick={() => (view = 'grid')}
					class={cn(
						'inline-flex size-7 items-center justify-center rounded-[4px] transition-colors',
						view === 'grid'
							? 'bg-primary text-primary-foreground'
							: 'text-muted-foreground hover:bg-muted'
					)}
				>
					<LayoutGrid class="size-3.5" />
				</button>
				<button
					type="button"
					aria-pressed={view === 'list'}
					aria-label="List view"
					onclick={() => (view = 'list')}
					class={cn(
						'inline-flex size-7 items-center justify-center rounded-[4px] transition-colors',
						view === 'list'
							? 'bg-primary text-primary-foreground'
							: 'text-muted-foreground hover:bg-muted'
					)}
				>
					<List class="size-3.5" />
				</button>
			</div>
		</div>
	</div>

	<div class="flex flex-wrap items-center gap-2">
		<div class="relative w-full sm:w-64">
			<Search
				class="pointer-events-none absolute top-2 left-2.5 size-3.5 text-muted-foreground"
				aria-hidden="true"
			/>
			<input
				type="search"
				aria-label="Search past papers by title or subject"
				placeholder="Search title & subject…"
				maxlength={100}
				bind:value={search}
				oninput={onSearchInput}
				class="h-8 w-full rounded-md border bg-background pr-2 pl-8 text-xs outline-none placeholder:text-muted-foreground focus-visible:ring-2 focus-visible:ring-ring/50"
			/>
		</div>

		<FilterMenu
			label="Subject"
			value={filters.subject ?? 'all'}
			options={subjectOptions}
			icon={Tags}
			onChange={(value) => apply({ subject: value === 'all' ? null : value })}
		/>
		<FilterMenu
			label="Year"
			value={filters.year === null ? 'all' : String(filters.year)}
			options={yearOptions}
			icon={CalendarDays}
			onChange={(value) => apply({ year: toNumber(value) })}
		/>
		<FilterMenu
			label="Session"
			value={filters.session ?? 'all'}
			options={sessionOptions}
			icon={Timer}
			onChange={(value) =>
				apply({
					session: value === 'all' ? null : (value as PastPaperFilters['session'])
				})}
		/>
		<FilterMenu
			label="Language"
			value={filters.language ?? 'all'}
			options={languageOptions}
			icon={Languages}
			onChange={(value) =>
				apply({
					language: value === 'all' ? null : (value as PastPaperFilters['language'])
				})}
		/>
		<FilterMenu
			label="Paper"
			value={filters.paperNumber === null ? 'all' : String(filters.paperNumber)}
			options={paperOptions}
			icon={Files}
			onChange={(value) => apply({ paperNumber: toNumber(value) })}
		/>
		<FilterMenu
			label="Grade"
			value={filters.grade === null ? 'all' : String(filters.grade)}
			options={gradeOptions}
			icon={GraduationCap}
			onChange={(value) => apply({ grade: toNumber(value) })}
		/>
		<FilterMenu
			label="Type"
			value={filters.kind ?? ALL_KINDS}
			options={kindOptions}
			icon={FileStack}
			onChange={(value) =>
				apply({
					kind: value === ALL_KINDS ? null : (value as PastPaperFilters['kind'])
				})}
		/>

		{#if filtersActive}
			<Button
				variant="ghost"
				size="sm"
				class="h-8 gap-1 text-xs text-muted-foreground"
				onclick={resetFilters}
			>
				<X class="size-3.5" />
				Reset filters
			</Button>
		{/if}

		{#if loading}
			<LoaderCircle
				class="size-4 text-muted-foreground motion-safe:animate-spin"
				aria-label="Loading papers"
			/>
		{/if}
	</div>

	{#if !papers}
		<Card.Root>
			<Card.Content class="flex flex-col items-center gap-2 py-12 text-center" role="alert">
				<FileText class="size-6 text-muted-foreground" />
				<p class="text-sm font-medium">{data.loadError}</p>
				<p class="text-xs text-muted-foreground">The paper library could not be reached.</p>
				<Button variant="outline" size="sm" class="mt-2" onclick={() => invalidateAll()}>
					<RotateCw class="size-3.5" />
					Try again
				</Button>
			</Card.Content>
		</Card.Root>
	{:else}
		<div class="flex items-center justify-between gap-2">
			<p class="text-xs text-muted-foreground">
				{#if papers.totalCount > 0}
					<span class="font-semibold text-foreground tabular-nums">{firstItem}-{lastItem}</span>
					of
					<span class="font-semibold text-foreground tabular-nums">
						{papers.totalCount.toLocaleString()}
					</span>
					{papers.totalCount === 1 ? 'document' : 'documents'}
				{:else}
					No documents
				{/if}
			</p>
			<p class="hidden text-xs text-muted-foreground sm:block">
				Published by the Department of Basic Education.
			</p>
		</div>

		{#if papers.items.length === 0}
			<Card.Root>
				<Card.Content class="flex flex-col items-center gap-2 py-12 text-center">
					<FileText class="size-6 text-muted-foreground" />
					<p class="text-sm font-medium">No papers match your filters</p>
					<p class="text-xs text-muted-foreground">Try another subject, year or language.</p>
					<Button variant="outline" size="sm" class="mt-2" onclick={resetFilters}>
						<X class="size-3.5" />
						Reset filters
					</Button>
				</Card.Content>
			</Card.Root>
		{:else if view === 'grid'}
			<div
				class={cn(
					'grid grid-cols-2 gap-2.5 transition-opacity sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 2xl:grid-cols-6',
					loading && 'opacity-60'
				)}
			>
				{#each papers.items as paper (paper.id)}
					<PastPaperCard {paper} onPreview={preview} onMemo={openMemo} />
				{/each}
			</div>
		{:else}
			<ul
				class={cn(
					'divide-y overflow-hidden rounded-lg border bg-card transition-opacity',
					loading && 'opacity-60'
				)}
			>
				{#each papers.items as paper (paper.id)}
					{@const title = `${paper.subject} · ${paperLabel(paper)}`}
					{@const previewable = isPreviewable(paper)}
					<li
						class="group flex items-center gap-3 p-2.5 transition-colors hover:bg-muted/40 sm:p-3"
					>
						<button
							type="button"
							class="relative h-16 w-12 shrink-0 overflow-hidden rounded border bg-muted/40 disabled:cursor-default"
							disabled={!previewable}
							onclick={() => preview(paper)}
							aria-label={`Preview ${title}, ${sessionLabels[paper.session]} ${paper.year}`}
						>
							<PaperThumbnail
								{paper}
								variant={categoryStyles[subjectCategory(paper.subject)].thumbnail}
								compact
							/>
						</button>

						<div class="min-w-0 flex-1">
							<div class="flex flex-wrap items-center gap-x-2 gap-y-1">
								<p class="truncate text-sm font-medium">{title}</p>
								{#if paper.kind !== 'QuestionPaper'}
									<span
										class="rounded-[3px] bg-slate-700 px-1.5 py-0.5 text-[9px] font-bold tracking-wide text-white uppercase"
									>
										{kindLabels[paper.kind]}
									</span>
								{:else if paper.memoId}
									<span
										class="rounded-[3px] bg-emerald-600 px-1.5 py-0.5 text-[9px] font-bold tracking-wide text-white uppercase"
									>
										Memo
									</span>
								{:else}
									<span
										class="rounded-[3px] border px-1.5 py-0.5 text-[9px] font-semibold tracking-wide text-muted-foreground uppercase"
									>
										No memo
									</span>
								{/if}
							</div>
							<p
								class="mt-0.5 flex flex-wrap items-center gap-x-2 gap-y-0.5 text-[11px] text-muted-foreground"
							>
								<span class="inline-flex items-center gap-1">
									<CalendarDays class="size-3" aria-hidden="true" />
									{sessionLabels[paper.session]}
									{paper.year}
								</span>
								<span aria-hidden="true">·</span>
								<span>Grade {paper.grade}</span>
								{#if paper.language}
									<span aria-hidden="true">·</span>
									<span>{languageLabels[paper.language]}</span>
								{/if}
								<span aria-hidden="true">·</span>
								<span>{paper.fileType.toUpperCase()} {formatFileSize(paper.sizeBytes)}</span>
							</p>
						</div>

						<div class="flex shrink-0 items-center gap-1">
							<Button
								variant="outline"
								size="icon-sm"
								class="size-7 rounded-[4px]"
								disabled={!previewable}
								onclick={() => preview(paper)}
								aria-label={`Preview ${title}`}
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
							{#if paper.kind === 'QuestionPaper'}
								<Button
									variant="outline"
									size="icon-sm"
									class="hidden size-7 rounded-[4px] sm:inline-flex"
									disabled={!paper.memoId}
									onclick={() => openMemo(paper)}
									aria-label={paper.memoId
										? `Open memo for ${title}`
										: `No memo available for ${title}`}
									title={paper.memoId ? 'Open memo' : 'No memo available'}
								>
									<FileCheck class="size-3.5" />
								</Button>
							{/if}
						</div>
					</li>
				{/each}
			</ul>
		{/if}

		{#if papers.totalPages > 1}
			<nav class="flex items-center justify-between gap-2 pt-1" aria-label="Pagination">
				<Button
					variant="outline"
					size="sm"
					class="h-8 gap-1 text-xs"
					disabled={!papers.hasPreviousPage || loading}
					onclick={() => apply({ page: filters.page - 1 })}
				>
					<ChevronLeft class="size-3.5" />
					Previous
				</Button>
				<p class="text-xs text-muted-foreground tabular-nums">
					Page {papers.pageNumber} of {papers.totalPages}
				</p>
				<Button
					variant="outline"
					size="sm"
					class="h-8 gap-1 text-xs"
					disabled={!papers.hasNextPage || loading}
					onclick={() => apply({ page: filters.page + 1 })}
				>
					Next
					<ChevronRight class="size-3.5" />
				</Button>
			</nav>
		{/if}
	{/if}
</div>

<PaperPreviewDialog
	paper={previewPaper}
	startWithMemo={previewMemo}
	onClose={() => (previewPaper = null)}
/>
