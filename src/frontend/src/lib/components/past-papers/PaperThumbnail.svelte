<script lang="ts">
	import { cn } from '$lib/utils';
	import type { PastPaper } from '$lib/types/past-papers';
	import { kindLabels, paperLabel, sessionLabels, type ThumbnailVariant } from './papers';

	interface Props {
		paper: PastPaper;
		variant: ThumbnailVariant;
		compact?: boolean;
	}

	let { paper, variant, compact = false }: Props = $props();

	const paperName = $derived(paperLabel(paper));
	const sessionName = $derived(sessionLabels[paper.session]);
	const documentLine = $derived(
		paper.kind === 'QuestionPaper' ? '' : kindLabels[paper.kind].toUpperCase()
	);

	// Split long subject names over two centred lines, the way the cover page
	// of a real NSC paper does.
	const subjectLines = $derived.by((): string[] => {
		if (paper.subject.length <= 16) return [paper.subject];
		const words = paper.subject.split(' ');
		if (words.length < 2) return [paper.subject];
		let best = 1;
		let bestScore = Number.POSITIVE_INFINITY;
		for (let i = 1; i < words.length; i++) {
			const first = words.slice(0, i).join(' ').length;
			const second = words.slice(i).join(' ').length;
			const score = Math.max(first, second);
			if (score < bestScore) {
				bestScore = score;
				best = i;
			}
		}
		return [words.slice(0, best).join(' '), words.slice(best).join(' ')];
	});

	const instructions: [number, number][] = [
		[178, 96],
		[164, 132],
		[188, 74],
		[148, 110]
	];

	const proseLines = [188, 176, 190, 154, 182, 168, 121, 186, 173, 149, 184, 160, 128, 177];

	const questionLines: [number, number][] = [
		[62, 90],
		[48, 116],
		[74, 98],
		[56, 108],
		[80, 72]
	];

	const graphPoints = [
		{ x: 24, y: 38 },
		{ x: 40, y: 41 },
		{ x: 56, y: 26 },
		{ x: 72, y: 30 },
		{ x: 88, y: 15 },
		{ x: 104, y: 19 }
	];
</script>

<div
	class={cn(
		'flex h-full w-full items-start justify-center overflow-hidden bg-gradient-to-b from-muted/70 via-muted/40 to-muted/20',
		compact ? 'px-0.5 pt-1' : 'px-2 pt-2.5'
	)}
>
	<svg
		viewBox="0 0 210 297"
		class={cn(
			'h-auto drop-shadow-[0_1px_3px_rgba(0,0,0,0.28)] transition-transform duration-300 group-hover:scale-[1.02]',
			compact ? 'w-[94%]' : 'w-[80%]'
		)}
		role="img"
		aria-label={`Cover of ${paper.subject} ${paperName}, ${sessionName} ${paper.year}`}
	>
		<rect width="210" height="297" rx="1.5" fill="#ffffff" stroke="#d4d4d4" stroke-width="0.6" />

		<g fill="#1f2937" font-family="Arial, Helvetica, sans-serif">
			<!-- Coat of arms and department masthead -->
			<g transform="translate(105 9)">
				<path
					d="M0 0 L6.5 1.8 V7 C6.5 11.5 3 14.2 0 16 C-3 14.2 -6.5 11.5 -6.5 7 V1.8 Z"
					fill="none"
					stroke="#374151"
					stroke-width="0.55"
				/>
				<circle cx="0" cy="7" r="1.9" fill="#374151" />
				<path d="M0 3.4 V10.6" stroke="#374151" stroke-width="0.4" />
			</g>
			<text x="105" y="31" text-anchor="middle" font-size="5.4" font-weight="700"
				>basic education</text
			>
			<text x="105" y="35.5" text-anchor="middle" font-size="3.2">Department: Basic Education</text>
			<text x="105" y="39.2" text-anchor="middle" font-size="3" letter-spacing="0.15"
				>REPUBLIC OF SOUTH AFRICA</text
			>

			<line x1="18" y1="44" x2="192" y2="44" stroke="#9ca3af" stroke-width="0.5" />

			<!-- Examination masthead -->
			<text
				x="105"
				y="52.5"
				text-anchor="middle"
				font-size="5.8"
				font-weight="700"
				letter-spacing="0.25">NATIONAL SENIOR CERTIFICATE</text
			>
			<text x="105" y="60.5" text-anchor="middle" font-size="5.2" font-weight="700"
				>GRADE {paper.grade}</text
			>

			{#each subjectLines as line, index (index)}
				<text x="105" y={75 + index * 9} text-anchor="middle" font-size="8.2" font-weight="700"
					>{line}</text
				>
			{/each}
			<text
				x="105"
				y={75 + subjectLines.length * 9 + 5}
				text-anchor="middle"
				font-size="6.2"
				font-weight="700">{paperName}</text
			>
			<text
				x="105"
				y={75 + subjectLines.length * 9 + 16}
				text-anchor="middle"
				font-size="5"
				letter-spacing="0.35">{`${sessionName.toUpperCase()} ${paper.year}`}</text
			>

			{#if documentLine}
				<text
					x="105"
					y={75 + subjectLines.length * 9 + 28}
					text-anchor="middle"
					font-size="4.4"
					font-weight="700"
					letter-spacing="0.3">{documentLine}</text
				>
			{/if}

			<line
				x1="18"
				y1={75 + subjectLines.length * 9 + 35}
				x2="192"
				y2={75 + subjectLines.length * 9 + 35}
				stroke="#9ca3af"
				stroke-width="0.5"
			/>

			<text
				x="18"
				y={75 + subjectLines.length * 9 + 42}
				font-size="4.2"
				font-weight="700"
				letter-spacing="0.1">INSTRUCTIONS AND INFORMATION</text
			>

			{#each instructions as [lineWidth, secondWidth], index (index)}
				<text x="18" y={75 + subjectLines.length * 9 + 49 + index * 6} font-size="3.2"
					>{index + 1}.</text
				>
				<rect
					x="23"
					y={75 + subjectLines.length * 9 + 46.8 + index * 6}
					width={lineWidth}
					height="1.5"
					rx="0.4"
					fill="#9ca3af"
				/>
				<rect
					x="23"
					y={75 + subjectLines.length * 9 + 49.3 + index * 6}
					width={secondWidth}
					height="1.5"
					rx="0.4"
					fill="#d1d5db"
				/>
			{/each}
		</g>

		<!-- Body: the first glimpse of the question paper content -->
		{#if variant === 'table'}
			<g transform="translate(18 176)" font-family="Arial, Helvetica, sans-serif">
				<text x="0" y="-4" font-size="4.2" font-weight="700" fill="#374151">QUESTION 1</text>
				<rect
					x="0"
					y="0"
					width="174"
					height="10"
					fill="#e5e7eb"
					stroke="#9ca3af"
					stroke-width="0.4"
				/>
				<rect
					x="0"
					y="10"
					width="174"
					height="8"
					fill="#ffffff"
					stroke="#9ca3af"
					stroke-width="0.4"
				/>
				<rect
					x="0"
					y="18"
					width="174"
					height="8"
					fill="#ffffff"
					stroke="#9ca3af"
					stroke-width="0.4"
				/>
				<rect
					x="0"
					y="26"
					width="174"
					height="8"
					fill="#ffffff"
					stroke="#9ca3af"
					stroke-width="0.4"
				/>
				<rect
					x="0"
					y="34"
					width="174"
					height="8"
					fill="#ffffff"
					stroke="#9ca3af"
					stroke-width="0.4"
				/>
				<rect
					x="0"
					y="42"
					width="174"
					height="8"
					fill="#ffffff"
					stroke="#9ca3af"
					stroke-width="0.4"
				/>
				{#each [44, 88, 132, 154] as x (x)}
					<line x1={x} y1="0" x2={x} y2="50" stroke="#9ca3af" stroke-width="0.4" />
				{/each}
				{#each [12, 22, 30, 38] as y (y)}
					<line x1="0" y1={y} x2="174" y2={y} stroke="#d1d5db" stroke-width="0.35" />
				{/each}
				{#each [0, 1, 2, 3, 4, 5] as row (row)}
					<rect x="3" y={row * 8 + (row === 0 ? 4 : 2.5)} width="32" height="1.7" fill="#9ca3af" />
					<rect x="47" y={row * 8 + (row === 0 ? 4 : 2.5)} width="30" height="1.7" fill="#c4c8ce" />
					<rect x="91" y={row * 8 + (row === 0 ? 4 : 2.5)} width="28" height="1.7" fill="#c4c8ce" />
					<rect
						x="135"
						y={row * 8 + (row === 0 ? 4 : 2.5)}
						width="16"
						height="1.7"
						fill="#c4c8ce"
					/>
				{/each}
			</g>
		{:else if variant === 'figure'}
			<g transform="translate(18 172)" font-family="Arial, Helvetica, sans-serif">
				<text x="0" y="-4" font-size="4.2" font-weight="700" fill="#374151">QUESTION 1</text>
				<rect
					x="0"
					y="0"
					width="118"
					height="58"
					fill="#ffffff"
					stroke="#9ca3af"
					stroke-width="0.45"
				/>
				<line x1="8" y1="50" x2="110" y2="50" stroke="#4b5563" stroke-width="0.5" />
				<line x1="8" y1="50" x2="8" y2="6" stroke="#4b5563" stroke-width="0.5" />
				<polyline
					points="8,46 24,38 40,41 56,26 72,30 88,15 104,19"
					fill="none"
					stroke="#374151"
					stroke-width="0.9"
				/>
				{#each graphPoints as point (point.x)}
					<circle cx={point.x} cy={point.y} r="1.1" fill="#374151" />
				{/each}
				{#each [0, 1, 2, 3, 4, 5] as i (i)}
					<rect x="126" y={i * 9} width={i % 2 === 0 ? 48 : 36} height="1.7" fill="#9ca3af" />
					<rect x="126" y={i * 9 + 3.5} width={i % 2 === 0 ? 30 : 42} height="1.7" fill="#d1d5db" />
				{/each}
				{#each questionLines as [first, second], index (index)}
					<rect x="0" y={68 + index * 8} width={first} height="1.6" fill="#9ca3af" />
					<rect x={first + 12} y={68 + index * 8} width={second} height="1.6" fill="#d1d5db" />
				{/each}
			</g>
		{:else if variant === 'questions'}
			<g transform="translate(18 172)" font-family="Arial, Helvetica, sans-serif">
				<text x="0" y="-4" font-size="4.2" font-weight="700" fill="#374151">QUESTION 1</text>
				{#each questionLines as [first, second], index (index)}
					<text x="0" y={index * 12 + 5} font-size="3.2" fill="#4b5563"
						>{index + 1}.{index + 1}</text
					>
					<rect x="9" y={index * 12 + 2.5} width={first} height="1.6" fill="#9ca3af" />
					<rect x="9" y={index * 12 + 6} width={second} height="1.6" fill="#d1d5db" />
					<rect x="9" y={index * 12 + 9.5} width="132" height="0.6" fill="#e5e7eb" />
				{/each}
			</g>
		{:else}
			<g transform="translate(18 172)" font-family="Arial, Helvetica, sans-serif">
				<text x="0" y="-4" font-size="4.2" font-weight="700" fill="#374151">QUESTION 1</text>
				{#each proseLines as width, index (index)}
					<rect
						x="0"
						y={index * 7}
						{width}
						height="1.6"
						fill={index % 4 === 0 ? '#6b7280' : '#9ca3af'}
					/>
				{/each}
			</g>
		{/if}

		<!-- Page footer -->
		<g font-family="Arial, Helvetica, sans-serif" fill="#6b7280">
			<line x1="18" y1="286" x2="192" y2="286" stroke="#d1d5db" stroke-width="0.4" />
			<text x="18" y="290.5" font-size="2.8">Copyright reserved</text>
			<text x="192" y="290.5" text-anchor="end" font-size="2.8">Please turn over</text>
		</g>
	</svg>
</div>
