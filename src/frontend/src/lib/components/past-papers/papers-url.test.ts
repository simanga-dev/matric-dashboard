import { describe, expect, it } from 'vitest';
import {
	hasActiveFilters,
	parsePastPaperFilters,
	pastPapersHref,
	toPastPaperQuery
} from './papers-url';

const parse = (query: string) => parsePastPaperFilters(new URLSearchParams(query));

describe('past paper URL state', () => {
	it('defaults to question papers, newest first, page 1', () => {
		const filters = parse('');
		expect(filters.kind).toBe('QuestionPaper');
		expect(filters.sort).toBe('newest');
		expect(filters.page).toBe(1);
		expect(hasActiveFilters(filters)).toBe(false);
		expect(pastPapersHref(filters)).toBe('/past-papers');
	});

	it('reads valid filters from the query string', () => {
		const filters = parse(
			'search=algebra&subject=Mathematics&year=2024&session=MayJune&grade=12&language=English&paper=2&kind=Memo&sort=oldest&page=3'
		);
		expect(filters).toEqual({
			search: 'algebra',
			subject: 'Mathematics',
			year: 2024,
			session: 'MayJune',
			grade: 12,
			language: 'English',
			paperNumber: 2,
			kind: 'Memo',
			sort: 'oldest',
			page: 3
		});
		expect(hasActiveFilters(filters)).toBe(true);
	});

	it('drops values the API would reject', () => {
		const filters = parse('year=1999&session=Winter&grade=9&paper=7&kind=Poster&sort=x&page=0');
		expect(filters.year).toBeNull();
		expect(filters.session).toBeNull();
		expect(filters.grade).toBeNull();
		expect(filters.paperNumber).toBeNull();
		expect(filters.kind).toBe('QuestionPaper');
		expect(filters.sort).toBe('newest');
		expect(filters.page).toBe(1);
	});

	it('treats kind=all as every document type', () => {
		const filters = parse('kind=all');
		expect(filters.kind).toBeNull();
		expect(toPastPaperQuery(filters).kind).toBeUndefined();
		expect(pastPapersHref(filters)).toBe('/past-papers?kind=all');
	});

	it('round-trips filters through the href', () => {
		const filters = parse('subject=Physical Sciences&year=2023&paper=1&page=2');
		const href = pastPapersHref(filters);
		expect(parse(href.split('?')[1] ?? '')).toEqual(filters);
	});

	it('maps filters to the list query with a fixed page size', () => {
		expect(toPastPaperQuery(parse('subject=Accounting&page=2'))).toEqual({
			search: undefined,
			subject: 'Accounting',
			year: undefined,
			session: undefined,
			grade: undefined,
			language: undefined,
			paperNumber: undefined,
			kind: 'QuestionPaper',
			sort: 'newest',
			pageNumber: 2,
			pageSize: 48
		});
	});
});
