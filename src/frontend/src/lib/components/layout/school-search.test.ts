import { describe, expect, it } from 'vitest';
import { highlightParts } from './school-search';

describe('highlightParts', () => {
	it('marks a prefix match at the start of a word', () => {
		expect(highlightParts('Sicelokuhle High School', 'sic')).toEqual([
			{ text: 'Sic', match: true },
			{ text: 'elokuhle High School', match: false }
		]);
	});

	it('marks every term of a multi-word query', () => {
		expect(highlightParts('Maputle Secondary School', 'sec maputle')).toEqual([
			{ text: 'Maputle', match: true },
			{ text: ' ', match: false },
			{ text: 'Sec', match: true },
			{ text: 'ondary School', match: false }
		]);
	});

	it('ignores matches in the middle of a word', () => {
		expect(highlightParts('Basic School', 'sic')).toEqual([{ text: 'Basic School', match: false }]);
	});

	it('treats regex characters in the query literally', () => {
		expect(highlightParts('St. Mary (Girls)', '(girls')).toEqual([
			{ text: 'St. Mary ', match: false },
			{ text: '(Girls', match: true },
			{ text: ')', match: false }
		]);
	});

	it('returns the label untouched for an empty query', () => {
		expect(highlightParts('Alpha High', '   ')).toEqual([{ text: 'Alpha High', match: false }]);
	});
});
