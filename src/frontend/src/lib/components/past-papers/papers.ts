/** Presentation metadata for the past-papers library. */

import type {
	PastPaper,
	PastPaperKind,
	PastPaperLanguage,
	PastPaperSession
} from '$lib/types/past-papers';

export type SubjectCategory =
	| 'mathematics'
	| 'sciences'
	| 'languages'
	| 'humanities'
	| 'commerce'
	| 'technology';

export type ThumbnailVariant = 'questions' | 'table' | 'figure' | 'prose';

interface CategoryStyle {
	label: string;
	badgeClass: string;
	dotClass: string;
	thumbnail: ThumbnailVariant;
}

export const categoryStyles: Record<SubjectCategory, CategoryStyle> = {
	mathematics: {
		label: 'Maths',
		badgeClass: 'bg-emerald-600 text-white',
		dotClass: 'bg-emerald-600',
		thumbnail: 'figure'
	},
	sciences: {
		label: 'Science',
		badgeClass: 'bg-sky-600 text-white',
		dotClass: 'bg-sky-600',
		thumbnail: 'figure'
	},
	languages: {
		label: 'Language',
		badgeClass: 'bg-rose-500 text-white',
		dotClass: 'bg-rose-500',
		thumbnail: 'prose'
	},
	humanities: {
		label: 'Humanities',
		badgeClass: 'bg-violet-500 text-white',
		dotClass: 'bg-violet-500',
		thumbnail: 'prose'
	},
	commerce: {
		label: 'Commerce',
		badgeClass: 'bg-amber-500 text-amber-950',
		dotClass: 'bg-amber-500',
		thumbnail: 'table'
	},
	technology: {
		label: 'Tech',
		badgeClass: 'bg-cyan-600 text-white',
		dotClass: 'bg-cyan-600',
		thumbnail: 'questions'
	}
};

export function subjectCategory(subject: string): SubjectCategory {
	if (/^(mathematics|mathematical literacy|technical mathematics)/i.test(subject)) {
		return 'mathematics';
	}
	if (/(physical|life|agricultural|technical|marine) sciences/i.test(subject)) return 'sciences';
	if (/(accounting|business studies|economics|consumer studies)/i.test(subject)) return 'commerce';
	if (
		/(information technology|computer applications?|engineering graphic|design|technology)/i.test(
			subject
		)
	) {
		return 'technology';
	}
	if (
		/(english|afrikaans|isi|sesotho|setswana|sepedi|siswati|tshivenda|xitsonga|sign language)/i.test(
			subject
		)
	) {
		return 'languages';
	}
	// Geography, history, tourism and anything unfamiliar share the neutral
	// humanities treatment rather than a random hue.
	return 'humanities';
}

export const sessionLabels: Record<PastPaperSession, string> = {
	November: 'November',
	MayJune: 'May/June',
	FebMarch: 'Feb/March',
	Exemplar: 'Exemplar',
	CommonPaper: 'Common Paper'
};

export const kindLabels: Record<PastPaperKind, string> = {
	QuestionPaper: 'Question paper',
	Memo: 'Memo',
	AnswerBook: 'Answer book',
	Addendum: 'Addendum',
	Other: 'Other document'
};

export const languageLabels: Record<PastPaperLanguage, string> = {
	English: 'English',
	Afrikaans: 'Afrikaans',
	EnglishAndAfrikaans: 'English & Afrikaans'
};

/** Short label for which paper this is, e.g. "Paper 1 (Woodworking)". */
export function paperLabel(paper: Pick<PastPaper, 'paperNumber' | 'variant'>): string {
	const base = paper.paperNumber === null ? '' : `Paper ${paper.paperNumber}`;
	if (!paper.variant) return base || 'Paper';
	return base ? `${base} (${paper.variant})` : paper.variant;
}

/** Human readable file size, e.g. "512 KB" or "1.3 MB". */
export function formatFileSize(bytes: number): string {
	if (bytes < 1024) return `${bytes} B`;
	if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
	return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

/** Whether the browser can show the file inline instead of downloading it. */
export function isPreviewable(paper: Pick<PastPaper, 'fileType'>): boolean {
	return paper.fileType.toLowerCase() === 'pdf';
}
