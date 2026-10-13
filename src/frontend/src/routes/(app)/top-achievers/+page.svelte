<script lang="ts">
	import * as Card from '$lib/components/ui/card';
	import { Badge } from '$lib/components/ui/badge';
	import { cn } from '$lib/utils';
	import {
		Trophy,
		Medal,
		MapPin,
		Sparkles,
		GraduationCap,
		Star,
		Quote,
		Award
	} from '@lucide/svelte';

	interface SubjectResult {
		name: string;
		mark: number;
	}

	interface TopAchiever {
		id: string;
		name: string;
		school: string;
		province: string;
		year: number;
		distinctions: number;
		average: number;
		subjects: SubjectResult[];
		note?: string;
	}

	// Illustrative sample. The official list of NSC top achievers is published
	// each year by the Department of Basic Education.
	const achievers: TopAchiever[] = [
		{
			id: '2024-naledi-mokoena',
			name: 'Naledi Mokoena',
			school: 'Pretoria High School for Girls',
			province: 'Gauteng',
			year: 2024,
			distinctions: 7,
			average: 95.4,
			subjects: [
				{ name: 'Mathematics', mark: 100 },
				{ name: 'Physical Sciences', mark: 98 },
				{ name: 'Life Sciences', mark: 96 }
			],
			note: 'Plans to study Actuarial Science.'
		},
		{
			id: '2024-sipho-dlamini',
			name: 'Sipho Dlamini',
			school: 'Durban High School',
			province: 'KwaZulu-Natal',
			year: 2024,
			distinctions: 7,
			average: 94.1,
			subjects: [
				{ name: 'Accounting', mark: 100 },
				{ name: 'Mathematics', mark: 95 },
				{ name: 'Economics', mark: 94 }
			]
		},
		{
			id: '2024-amy-van-wyk',
			name: 'Amy van Wyk',
			school: 'Paarl Girls High School',
			province: 'Western Cape',
			year: 2024,
			distinctions: 6,
			average: 93.2,
			subjects: [
				{ name: 'English Home Language', mark: 100 },
				{ name: 'History', mark: 97 },
				{ name: 'Life Orientation', mark: 95 }
			],
			note: 'Head girl and debate captain.'
		},
		{
			id: '2024-thabo-ndlovu',
			name: 'Thabo Ndlovu',
			school: 'Soweto Secondary School',
			province: 'Gauteng',
			year: 2024,
			distinctions: 6,
			average: 91.8,
			subjects: [
				{ name: 'Mathematics', mark: 100 },
				{ name: 'Information Technology', mark: 96 },
				{ name: 'Physical Sciences', mark: 90 }
			]
		},
		{
			id: '2023-lerato-mahlangu',
			name: 'Lerato Mahlangu',
			school: 'Mamelodi High School',
			province: 'Gauteng',
			year: 2023,
			distinctions: 7,
			average: 96.0,
			subjects: [
				{ name: 'Life Sciences', mark: 100 },
				{ name: 'Mathematics', mark: 99 },
				{ name: 'Physical Sciences', mark: 97 }
			],
			note: 'Awarded a national bursary for medicine.'
		},
		{
			id: '2023-jonathan-botha',
			name: 'Jonathan Botha',
			school: 'Grey College',
			province: 'Free State',
			year: 2023,
			distinctions: 6,
			average: 92.7,
			subjects: [
				{ name: 'Mathematics', mark: 100 },
				{ name: 'Accounting', mark: 95 },
				{ name: 'Business Studies', mark: 91 }
			]
		},
		{
			id: '2023-zanele-khumalo',
			name: 'Zanele Khumalo',
			school: 'Pietermaritzburg Girls High School',
			province: 'KwaZulu-Natal',
			year: 2023,
			distinctions: 6,
			average: 91.3,
			subjects: [
				{ name: 'English Home Language', mark: 100 },
				{ name: 'Geography', mark: 94 },
				{ name: 'Life Sciences', mark: 90 }
			]
		},
		{
			id: '2023-kabelo-maseko',
			name: 'Kabelo Maseko',
			school: 'Polokwane High School',
			province: 'Limpopo',
			year: 2023,
			distinctions: 5,
			average: 90.5,
			subjects: [
				{ name: 'Physical Sciences', mark: 100 },
				{ name: 'Mathematics', mark: 93 },
				{ name: 'Agriculture', mark: 89 }
			]
		},
		{
			id: '2022-aisha-patel',
			name: 'Aisha Patel',
			school: 'Lenasia Secondary School',
			province: 'Gauteng',
			year: 2022,
			distinctions: 7,
			average: 95.1,
			subjects: [
				{ name: 'Mathematics', mark: 100 },
				{ name: 'Life Sciences', mark: 98 },
				{ name: 'Chemistry', mark: 96 }
			],
			note: 'Top achiever in the Gauteng region.'
		},
		{
			id: '2022-lwazi-mthembu',
			name: 'Lwazi Mthembu',
			school: 'Umlazi Comtech',
			province: 'KwaZulu-Natal',
			year: 2022,
			distinctions: 6,
			average: 92.4,
			subjects: [
				{ name: 'Technical Mathematics', mark: 100 },
				{ name: 'Engineering Graphics and Design', mark: 97 },
				{ name: 'Physical Sciences', mark: 91 }
			]
		},
		{
			id: '2022-elsa-jacobs',
			name: 'Elsa Jacobs',
			school: 'Kimberley Boys High School',
			province: 'Northern Cape',
			year: 2022,
			distinctions: 5,
			average: 90.9,
			subjects: [
				{ name: 'Accounting', mark: 100 },
				{ name: 'Economics', mark: 93 },
				{ name: 'Mathematics', mark: 90 }
			]
		},
		{
			id: '2022-mpho-mabaso',
			name: 'Mpho Mabaso',
			school: 'Nelspruit Secondary School',
			province: 'Mpumalanga',
			year: 2022,
			distinctions: 5,
			average: 90.2,
			subjects: [
				{ name: 'Life Sciences', mark: 100 },
				{ name: 'Geography', mark: 92 },
				{ name: 'English First Additional Language', mark: 90 }
			]
		}
	];

	const years = [2024, 2023, 2022];
	let selectedYear = $state<number | 'all'>(2024);

	const filtered = $derived(
		selectedYear === 'all' ? achievers : achievers.filter((a) => a.year === selectedYear)
	);

	const provinceCount = $derived(new Set(filtered.map((a) => a.province)).size);
	const perfectScores = $derived(
		filtered.reduce((total, a) => total + a.subjects.filter((s) => s.mark === 100).length, 0)
	);
	const distinctionCount = $derived(filtered.reduce((total, a) => total + a.distinctions, 0));
	const topAverage = $derived(filtered.length ? Math.max(...filtered.map((a) => a.average)) : 0);

	const perfectClub = $derived(
		filtered
			.map((a) => ({ achiever: a, subjects: a.subjects.filter((s) => s.mark === 100) }))
			.filter((entry) => entry.subjects.length > 0)
	);

	function initials(name: string): string {
		return name
			.split(' ')
			.filter(Boolean)
			.slice(0, 2)
			.map((part) => part.charAt(0))
			.join('')
			.toUpperCase();
	}

	function markVariant(mark: number): 'success' | 'secondary' {
		return mark === 100 ? 'success' : 'secondary';
	}
</script>

<svelte:head>
	<title>Top Achievers • MatricDashboard</title>
	<meta
		name="description"
		content="A spotlight on the learners recognised nationally for outstanding NSC results."
	/>
</svelte:head>

<div class="space-y-6">
	<section
		class="relative overflow-hidden rounded-xl border bg-gradient-to-br from-primary/10 via-card to-card p-6 lg:p-8"
	>
		<div class="relative flex flex-col gap-6 sm:flex-row sm:items-start sm:justify-between">
			<div class="space-y-3">
				<span
					class="inline-flex items-center gap-2 rounded-full border bg-background/60 px-3 py-1 text-xs font-medium text-muted-foreground"
				>
					<Trophy class="size-3.5 text-amber-500" />
					National Senior Certificate
				</span>
				<h1 class="text-2xl font-semibold tracking-tight sm:text-3xl">Top Achievers</h1>
				<p class="max-w-2xl text-sm leading-relaxed text-muted-foreground">
					Every year the Department of Basic Education recognises learners who achieve outstanding
					results in the NSC examinations. This is a spotlight for those learners and the schools
					that carried them.
				</p>
			</div>
			<div
				class="flex size-14 shrink-0 items-center justify-center rounded-2xl border bg-background/60"
			>
				<Medal class="size-7 text-amber-500" />
			</div>
		</div>
		<p
			class="relative mt-6 rounded-lg border border-dashed bg-background/50 px-3 py-2 text-xs text-muted-foreground"
		>
			Sample data for illustration. The official top-achiever list is published each year by the
			Department of Basic Education.
		</p>
	</section>

	<div class="flex flex-wrap items-center gap-2">
		<span class="text-xs font-medium text-muted-foreground">Cohort</span>
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
			All years
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

	<div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
		{#each [{ label: 'Top achievers', value: filtered.length, icon: Award }, { label: 'Provinces represented', value: provinceCount, icon: MapPin }, { label: 'Distinctions', value: distinctionCount, icon: GraduationCap }, { label: 'Perfect subject marks', value: perfectScores, icon: Sparkles }] as stat (stat.label)}
			<Card.Root>
				<Card.Content class="flex items-center gap-4 p-4">
					<div
						class="flex size-10 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary"
					>
						<stat.icon class="size-5" />
					</div>
					<div class="min-w-0">
						<p class="text-xl font-semibold tabular-nums">{stat.value.toLocaleString()}</p>
						<p class="truncate text-xs text-muted-foreground">{stat.label}</p>
					</div>
				</Card.Content>
			</Card.Root>
		{/each}
	</div>

	<div class="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
		{#each filtered as achiever (achiever.id)}
			<Card.Root class="flex flex-col">
				<Card.Header>
					<div class="flex items-start gap-3">
						<div
							class="flex size-11 shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-primary/20 to-primary/5 text-sm font-semibold text-primary"
						>
							{initials(achiever.name)}
						</div>
						<div class="min-w-0 flex-1">
							<Card.Title class="truncate text-base">{achiever.name}</Card.Title>
							<Card.Description class="truncate text-xs">{achiever.school}</Card.Description>
							<p class="mt-0.5 flex items-center gap-1 text-xs text-muted-foreground">
								<MapPin class="size-3" />
								{achiever.province}
							</p>
						</div>
						<Badge variant="outline" class="shrink-0 tabular-nums">{achiever.year}</Badge>
					</div>
				</Card.Header>
				<Card.Content class="flex flex-1 flex-col gap-3">
					<div class="flex items-center gap-2">
						{#if achiever.average === topAverage && topAverage > 0}
							<Badge variant="warning" class="gap-1">
								<Star class="size-3" />
								Highest average
							</Badge>
						{/if}
						<Badge variant="secondary" class="tabular-nums"
							>{achiever.distinctions} distinctions</Badge
						>
						<Badge variant="secondary" class="tabular-nums"
							>{achiever.average.toFixed(1)}% average</Badge
						>
					</div>
					<div class="flex flex-wrap gap-1.5">
						{#each achiever.subjects as subject (subject.name)}
							<Badge variant={markVariant(subject.mark)} class="gap-1 font-normal">
								{subject.name}
								<span class="font-semibold tabular-nums">{subject.mark}%</span>
							</Badge>
						{/each}
					</div>
					{#if achiever.note}
						<p class="mt-auto flex items-start gap-1.5 text-xs text-muted-foreground">
							<Quote class="mt-0.5 size-3 shrink-0" />
							{achiever.note}
						</p>
					{/if}
				</Card.Content>
			</Card.Root>
		{/each}
	</div>

	<Card.Root>
		<Card.Header>
			<Card.Title class="flex items-center gap-2 text-base">
				<Sparkles class="size-4 text-primary" />
				The 100% Club
			</Card.Title>
			<Card.Description>
				Learners in this cohort who scored a perfect mark in at least one subject.
			</Card.Description>
		</Card.Header>
		<Card.Content>
			{#if perfectClub.length === 0}
				<p class="text-sm text-muted-foreground">No perfect marks in this cohort yet.</p>
			{:else}
				<ul class="divide-y">
					{#each perfectClub as entry (entry.achiever.id)}
						<li class="flex flex-wrap items-center justify-between gap-3 py-3">
							<div class="min-w-0">
								<p class="truncate text-sm font-medium">{entry.achiever.name}</p>
								<p class="truncate text-xs text-muted-foreground">
									{entry.achiever.school} · {entry.achiever.province}
								</p>
							</div>
							<div class="flex flex-wrap gap-1.5">
								{#each entry.subjects as subject (subject.name)}
									<Badge variant="success" class="gap-1 font-normal">
										{subject.name}
										<span class="font-semibold tabular-nums">100%</span>
									</Badge>
								{/each}
							</div>
						</li>
					{/each}
				</ul>
			{/if}
		</Card.Content>
	</Card.Root>
</div>
