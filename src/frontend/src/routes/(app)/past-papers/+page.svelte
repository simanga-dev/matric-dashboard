<script lang="ts">
	import * as Card from '$lib/components/ui/card';
	import { Badge } from '$lib/components/ui/badge';
	import { Button } from '$lib/components/ui/button';
	import { toast } from '$lib/components/ui/sonner';
	import { cn } from '$lib/utils';
	import { FileText, Download, Search, BookOpen, Calendar, CheckCircle2 } from '@lucide/svelte';

	interface PastPaper {
		id: string;
		subject: string;
		paper: string;
		year: number;
		language: string;
		pages: number;
		memo: boolean;
	}

	// Illustrative sample. Real past papers are published by the Department of
	// Basic Education and the provincial education departments.
	const papers: PastPaper[] = [
		{
			id: 'math-p1-2024',
			subject: 'Mathematics',
			paper: 'Paper 1',
			year: 2024,
			language: 'English',
			pages: 12,
			memo: true
		},
		{
			id: 'math-p2-2024',
			subject: 'Mathematics',
			paper: 'Paper 2',
			year: 2024,
			language: 'English',
			pages: 14,
			memo: true
		},
		{
			id: 'phys-p1-2024',
			subject: 'Physical Sciences',
			paper: 'Paper 1 (Physics)',
			year: 2024,
			language: 'English',
			pages: 16,
			memo: true
		},
		{
			id: 'phys-p2-2024',
			subject: 'Physical Sciences',
			paper: 'Paper 2 (Chemistry)',
			year: 2024,
			language: 'English',
			pages: 15,
			memo: false
		},
		{
			id: 'life-p1-2024',
			subject: 'Life Sciences',
			paper: 'Paper 1',
			year: 2024,
			language: 'English',
			pages: 18,
			memo: true
		},
		{
			id: 'acc-p1-2024',
			subject: 'Accounting',
			paper: 'Paper 1',
			year: 2024,
			language: 'English',
			pages: 11,
			memo: true
		},
		{
			id: 'eng-hl-p1-2024',
			subject: 'English Home Language',
			paper: 'Paper 1',
			year: 2024,
			language: 'English',
			pages: 9,
			memo: false
		},
		{
			id: 'geo-p1-2023',
			subject: 'Geography',
			paper: 'Paper 1',
			year: 2023,
			language: 'English',
			pages: 17,
			memo: true
		},
		{
			id: 'geo-p2-2023',
			subject: 'Geography',
			paper: 'Paper 2',
			year: 2023,
			language: 'Afrikaans',
			pages: 17,
			memo: true
		},
		{
			id: 'econ-p1-2023',
			subject: 'Economics',
			paper: 'Paper 1',
			year: 2023,
			language: 'English',
			pages: 12,
			memo: true
		},
		{
			id: 'bus-p1-2023',
			subject: 'Business Studies',
			paper: 'Paper 1',
			year: 2023,
			language: 'English',
			pages: 13,
			memo: false
		},
		{
			id: 'math-lit-p1-2023',
			subject: 'Mathematical Literacy',
			paper: 'Paper 1',
			year: 2023,
			language: 'English',
			pages: 14,
			memo: true
		},
		{
			id: 'hist-p1-2022',
			subject: 'History',
			paper: 'Paper 1',
			year: 2022,
			language: 'English',
			pages: 10,
			memo: true
		},
		{
			id: 'math-p1-2022',
			subject: 'Mathematics',
			paper: 'Paper 1',
			year: 2022,
			language: 'English',
			pages: 12,
			memo: true
		},
		{
			id: 'life-p2-2022',
			subject: 'Life Sciences',
			paper: 'Paper 2',
			year: 2022,
			language: 'Afrikaans',
			pages: 18,
			memo: true
		}
	];

	const years = [2024, 2023, 2022];

	let search = $state('');
	let selectedYear = $state<number | 'all'>('all');

	const filtered = $derived(
		papers.filter((p) => {
			const matchesYear = selectedYear === 'all' || p.year === selectedYear;
			const term = search.trim().toLowerCase();
			const matchesSearch =
				!term || p.subject.toLowerCase().includes(term) || p.paper.toLowerCase().includes(term);
			return matchesYear && matchesSearch;
		})
	);

	function download(paper: PastPaper) {
		toast.info(`${paper.subject} · ${paper.paper} (${paper.year})`, {
			description: 'Sample document. Past papers are not available in this demo yet.'
		});
	}
</script>

<svelte:head>
	<title>Past Papers • MatricDashboard</title>
	<meta
		name="description"
		content="Browse NSC past exam papers by subject and year, with memos where available."
	/>
</svelte:head>

<div class="space-y-6">
	<div class="flex flex-col gap-2">
		<h1 class="flex items-center gap-2 text-2xl font-semibold tracking-tight">
			<BookOpen class="size-6 text-primary" />
			Past Papers
		</h1>
		<p class="text-sm text-muted-foreground">
			Practise with previous NSC exam papers, organised by subject and year. Memos are included
			where available.
		</p>
	</div>

	<div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
		<div class="relative w-full sm:max-w-xs">
			<Search class="pointer-events-none absolute top-2.5 left-3 size-4 text-muted-foreground" />
			<input
				aria-label="Search past papers by subject or paper"
				placeholder="Search subject or paper…"
				bind:value={search}
				class="h-9 w-full rounded-md border bg-background pr-3 pl-9 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
			/>
		</div>
		<div class="flex flex-wrap items-center gap-2">
			<span class="text-xs font-medium text-muted-foreground">Year</span>
			<button
				type="button"
				aria-pressed={selectedYear === 'all'}
				onclick={() => (selectedYear = 'all')}
				class={cn(
					'rounded-full border px-3 py-1 text-xs font-medium transition-colors',
					selectedYear === 'all'
						? 'border-transparent bg-primary text-primary-foreground'
						: 'bg-background hover:bg-muted'
				)}
			>
				All
			</button>
			{#each years as year (year)}
				<button
					type="button"
					aria-pressed={selectedYear === year}
					onclick={() => (selectedYear = year)}
					class={cn(
						'rounded-full border px-3 py-1 text-xs font-medium tabular-nums transition-colors',
						selectedYear === year
							? 'border-transparent bg-primary text-primary-foreground'
							: 'bg-background hover:bg-muted'
					)}
				>
					{year}
				</button>
			{/each}
		</div>
	</div>

	{#if filtered.length === 0}
		<Card.Root>
			<Card.Content class="flex flex-col items-center gap-2 py-12 text-center">
				<FileText class="size-6 text-muted-foreground" />
				<p class="text-sm font-medium">No papers match your filters</p>
				<p class="text-xs text-muted-foreground">Try another subject or year.</p>
			</Card.Content>
		</Card.Root>
	{:else}
		<Card.Root>
			<Card.Content class="p-0">
				<ul class="divide-y">
					{#each filtered as paper (paper.id)}
						<li class="flex flex-wrap items-center gap-3 p-4">
							<div
								class="flex size-10 shrink-0 items-center justify-center rounded-lg bg-muted text-muted-foreground"
							>
								<FileText class="size-5" />
							</div>
							<div class="min-w-0 flex-1">
								<p class="truncate text-sm font-medium">{paper.subject}</p>
								<p
									class="flex flex-wrap items-center gap-x-2 gap-y-0.5 text-xs text-muted-foreground"
								>
									<span>{paper.paper}</span>
									<span class="inline-flex items-center gap-1">
										<Calendar class="size-3" />
										{paper.year}
									</span>
									<span>{paper.language}</span>
									<span>{paper.pages} pages</span>
								</p>
							</div>
							{#if paper.memo}
								<Badge variant="success" class="gap-1">
									<CheckCircle2 class="size-3" />
									Memo
								</Badge>
							{:else}
								<Badge variant="outline">Paper only</Badge>
							{/if}
							<Button size="sm" variant="outline" onclick={() => download(paper)}>
								<Download class="size-4" />
								Download
							</Button>
						</li>
					{/each}
				</ul>
			</Card.Content>
		</Card.Root>
	{/if}

	<p class="text-xs text-muted-foreground">
		Showing {filtered.length} of {papers.length} papers. Sample data for illustration.
	</p>
</div>
