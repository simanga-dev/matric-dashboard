/** URL query string <-> past paper filter state. The URL is the source of truth. */

import type {
	PastPaperKind,
	PastPaperLanguage,
	PastPaperQuery,
	PastPaperSession,
	PastPaperSort
} from '$lib/types/past-papers';

export const PAST_PAPERS_PAGE_SIZE = 48;

/** Question papers are the default view; memos are reached through each card. */
export const DEFAULT_KIND: PastPaperKind = 'QuestionPaper';

/** Query value meaning "every kind", since an absent `kind` means question papers. */
export const ALL_KINDS = 'all';

const SESSIONS: readonly PastPaperSession[] = [
	'November',
	'MayJune',
	'FebMarch',
	'Exemplar',
	'CommonPaper'
];
const KINDS: readonly PastPaperKind[] = [
	'QuestionPaper',
	'Memo',
	'AnswerBook',
	'Addendum',
	'Other'
];
const LANGUAGES: readonly PastPaperLanguage[] = ['English', 'Afrikaans', 'EnglishAndAfrikaans'];
const SORTS: readonly PastPaperSort[] = ['newest', 'oldest', 'subject'];

/** Filter state for the page. `kind` of `null` means every kind. */
export interface PastPaperFilters {
	search: string;
	subject: string | null;
	year: number | null;
	session: PastPaperSession | null;
	grade: number | null;
	language: PastPaperLanguage | null;
	paperNumber: number | null;
	kind: PastPaperKind | null;
	sort: PastPaperSort;
	page: number;
}

function oneOf<T extends string>(value: string | null, allowed: readonly T[]): T | null {
	return value !== null && (allowed as readonly string[]).includes(value) ? (value as T) : null;
}

function intIn(value: string | null, min: number, max: number): number | null {
	if (value === null || !/^\d+$/.test(value)) return null;
	const parsed = Number(value);
	return parsed >= min && parsed <= max ? parsed : null;
}

/** Reads filters from the query string, dropping anything the API would reject. */
export function parsePastPaperFilters(params: URLSearchParams): PastPaperFilters {
	const rawKind = params.get('kind');
	return {
		search: (params.get('search') ?? '').trim().slice(0, 100),
		subject: params.get('subject')?.trim() || null,
		year: intIn(params.get('year'), 2000, 2100),
		session: oneOf(params.get('session'), SESSIONS),
		grade: intIn(params.get('grade'), 10, 12),
		language: oneOf(params.get('language'), LANGUAGES),
		paperNumber: intIn(params.get('paper'), 1, 4),
		kind: rawKind === ALL_KINDS ? null : (oneOf(rawKind, KINDS) ?? DEFAULT_KIND),
		sort: oneOf(params.get('sort'), SORTS) ?? 'newest',
		page: intIn(params.get('page'), 1, 100_000) ?? 1
	};
}

/** Maps page filters onto the list endpoint's query parameters. */
export function toPastPaperQuery(filters: PastPaperFilters): PastPaperQuery {
	return {
		search: filters.search || undefined,
		subject: filters.subject ?? undefined,
		year: filters.year ?? undefined,
		session: filters.session ?? undefined,
		grade: filters.grade ?? undefined,
		language: filters.language ?? undefined,
		paperNumber: filters.paperNumber ?? undefined,
		kind: filters.kind ?? undefined,
		sort: filters.sort,
		pageNumber: filters.page,
		pageSize: PAST_PAPERS_PAGE_SIZE
	};
}

/** Builds the page URL for a filter state, omitting defaults to keep links short. */
export function pastPapersHref(
	filters: PastPaperFilters
): '/past-papers' | `/past-papers?${string}` {
	const params = new URLSearchParams();
	if (filters.search) params.set('search', filters.search);
	if (filters.subject) params.set('subject', filters.subject);
	if (filters.year !== null) params.set('year', String(filters.year));
	if (filters.session) params.set('session', filters.session);
	if (filters.grade !== null) params.set('grade', String(filters.grade));
	if (filters.language) params.set('language', filters.language);
	if (filters.paperNumber !== null) params.set('paper', String(filters.paperNumber));
	if (filters.kind === null) params.set('kind', ALL_KINDS);
	else if (filters.kind !== DEFAULT_KIND) params.set('kind', filters.kind);
	if (filters.sort !== 'newest') params.set('sort', filters.sort);
	if (filters.page > 1) params.set('page', String(filters.page));
	const query = params.toString();
	return query ? `/past-papers?${query}` : '/past-papers';
}

/** Whether any filter narrows the default view. */
export function hasActiveFilters(filters: PastPaperFilters): boolean {
	return (
		filters.search !== '' ||
		filters.subject !== null ||
		filters.year !== null ||
		filters.session !== null ||
		filters.grade !== null ||
		filters.language !== null ||
		filters.paperNumber !== null ||
		filters.kind !== DEFAULT_KIND
	);
}
