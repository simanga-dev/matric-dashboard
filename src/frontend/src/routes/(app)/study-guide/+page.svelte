<script lang="ts">
	import * as Card from '$lib/components/ui/card';
	import { Badge } from '$lib/components/ui/badge';
	import { Button } from '$lib/components/ui/button';
	import { toast } from '$lib/components/ui/sonner';
	import {
		NotebookPen,
		Lightbulb,
		Clock,
		Target,
		BookOpen,
		Brain,
		ListChecks,
		ArrowRight
	} from '@lucide/svelte';

	interface StudyGuide {
		id: string;
		subject: string;
		description: string;
		topics: number;
		hours: number;
		level: 'Core' | 'Elective';
	}

	// Illustrative sample study guides aligned to the NSC curriculum.
	const guides: StudyGuide[] = [
		{
			id: 'mathematics',
			subject: 'Mathematics',
			description: 'Algebra, functions, calculus, trigonometry, sequences and probability.',
			topics: 11,
			hours: 42,
			level: 'Core'
		},
		{
			id: 'mathematical-literacy',
			subject: 'Mathematical Literacy',
			description: 'Numbers, finance, measurement, maps, plans and data handling.',
			topics: 8,
			hours: 32,
			level: 'Core'
		},
		{
			id: 'physical-sciences',
			subject: 'Physical Sciences',
			description: 'Mechanics, waves, electricity, chemical change and matter.',
			topics: 12,
			hours: 46,
			level: 'Core'
		},
		{
			id: 'life-sciences',
			subject: 'Life Sciences',
			description: 'Cells, genetics, human systems, ecology and evolution.',
			topics: 10,
			hours: 38,
			level: 'Core'
		},
		{
			id: 'accounting',
			subject: 'Accounting',
			description: 'Financial statements, reconciliations, cost accounting and ethics.',
			topics: 9,
			hours: 36,
			level: 'Elective'
		},
		{
			id: 'english-hl',
			subject: 'English Home Language',
			description: 'Comprehension, summary, essays, and literature study.',
			topics: 7,
			hours: 30,
			level: 'Core'
		},
		{
			id: 'geography',
			subject: 'Geography',
			description: 'Climatology, geomorphology, settlement and economic geography.',
			topics: 8,
			hours: 34,
			level: 'Elective'
		},
		{
			id: 'economics',
			subject: 'Economics',
			description: 'Microeconomics, macroeconomics, economic pursuits and policy.',
			topics: 7,
			hours: 30,
			level: 'Elective'
		},
		{
			id: 'business-studies',
			subject: 'Business Studies',
			description: 'Business environments, operations, ventures and roles.',
			topics: 8,
			hours: 30,
			level: 'Elective'
		},
		{
			id: 'history',
			subject: 'History',
			description: 'Source-based analysis, essays and key 20th century themes.',
			topics: 6,
			hours: 28,
			level: 'Elective'
		},
		{
			id: 'information-technology',
			subject: 'Information Technology',
			description: 'Programming, data structures, databases and networking.',
			topics: 9,
			hours: 40,
			level: 'Elective'
		},
		{
			id: 'agricultural-sciences',
			subject: 'Agricultural Sciences',
			description: 'Soil, plant and animal sciences, and agricultural management.',
			topics: 8,
			hours: 34,
			level: 'Elective'
		}
	];

	const tips = [
		'Build a weekly timetable and protect your study blocks.',
		'Work through past papers under timed exam conditions.',
		'Aim to understand concepts rather than memorise answers.',
		'Keep an error log and revisit the questions you got wrong.',
		'Take regular breaks and guard your sleep before the exam.',
		'Study in short, focused sessions rather than long marathons.'
	];

	function openGuide(guide: StudyGuide) {
		toast.info(`${guide.subject} study guide`, {
			description: 'Sample guide. Full study notes are not available in this demo yet.'
		});
	}
</script>

<svelte:head>
	<title>Study Guide • MatricDashboard</title>
	<meta
		name="description"
		content="Subject study guides and practical revision tips for the NSC examinations."
	/>
</svelte:head>

<div class="space-y-6">
	<div class="flex flex-col gap-2">
		<h1 class="flex items-center gap-2 text-2xl font-semibold tracking-tight">
			<NotebookPen class="size-6 text-primary" />
			Study Guide
		</h1>
		<p class="text-sm text-muted-foreground">
			Revision material and a topic breakdown for each NSC subject, plus practical tips to help you
			prepare.
		</p>
	</div>

	<div class="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
		{#each guides as guide (guide.id)}
			<Card.Root class="flex flex-col">
				<Card.Header>
					<div class="flex items-start justify-between gap-2">
						<div
							class="flex size-10 items-center justify-center rounded-lg bg-primary/10 text-primary"
						>
							<BookOpen class="size-5" />
						</div>
						<Badge variant={guide.level === 'Core' ? 'secondary' : 'outline'}>
							{guide.level}
						</Badge>
					</div>
					<Card.Title class="mt-2 text-base">{guide.subject}</Card.Title>
					<Card.Description class="text-xs leading-relaxed">
						{guide.description}
					</Card.Description>
				</Card.Header>
				<Card.Content class="mt-auto space-y-4">
					<div class="flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground">
						<span class="inline-flex items-center gap-1">
							<ListChecks class="size-3.5" />
							{guide.topics} topics
						</span>
						<span class="inline-flex items-center gap-1">
							<Clock class="size-3.5" />
							{guide.hours}h suggested
						</span>
					</div>
					<Button variant="outline" size="sm" class="w-full" onclick={() => openGuide(guide)}>
						Open guide
						<ArrowRight class="size-4" />
					</Button>
				</Card.Content>
			</Card.Root>
		{/each}
	</div>

	<Card.Root>
		<Card.Header>
			<Card.Title class="flex items-center gap-2 text-base">
				<Lightbulb class="size-4 text-amber-500" />
				Study tips
			</Card.Title>
			<Card.Description>Small habits that make a measurable difference.</Card.Description>
		</Card.Header>
		<Card.Content>
			<ul class="grid gap-x-6 gap-y-3 sm:grid-cols-2">
				{#each tips as tip (tip)}
					<li class="flex items-start gap-2 text-sm">
						<Target class="mt-0.5 size-4 shrink-0 text-primary" />
						<span>{tip}</span>
					</li>
				{/each}
			</ul>
		</Card.Content>
	</Card.Root>

	<div class="grid gap-4 sm:grid-cols-2">
		<Card.Root>
			<Card.Content class="flex items-start gap-3 p-4">
				<div
					class="flex size-10 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary"
				>
					<Brain class="size-5" />
				</div>
				<div>
					<p class="text-sm font-medium">Active recall</p>
					<p class="text-xs text-muted-foreground">
						Test yourself from memory instead of re-reading notes. It is harder, and that is why it
						works.
					</p>
				</div>
			</Card.Content>
		</Card.Root>
		<Card.Root>
			<Card.Content class="flex items-start gap-3 p-4">
				<div
					class="flex size-10 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary"
				>
					<Clock class="size-5" />
				</div>
				<div>
					<p class="text-sm font-medium">Spaced repetition</p>
					<p class="text-xs text-muted-foreground">
						Revisit a topic after a day, then a week. Spacing beats cramming every time.
					</p>
				</div>
			</Card.Content>
		</Card.Root>
	</div>

	<p class="text-xs text-muted-foreground">Sample data for illustration.</p>
</div>
