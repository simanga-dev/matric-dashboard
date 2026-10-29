import { describe, expect, it } from 'vitest';
import {
	parseSubjectScope,
	subjectHref,
	subjectScopeSearch,
	subjectsListHref
} from './subject-url';

describe('subject scope URLs', () => {
	it('reads province, district and year from the query string', () => {
		expect(
			parseSubjectScope(new URLSearchParams('province=GAUTENG&district=Tshwane%20South&year=2024'))
		).toEqual({ province: 'GAUTENG', district: 'Tshwane South', year: 2024 });
	});

	it('ignores a district without its province and rejects malformed years', () => {
		expect(parseSubjectScope(new URLSearchParams('district=Tshwane&year=20x4'))).toEqual({
			province: null,
			district: null,
			year: null
		});
		expect(parseSubjectScope(new URLSearchParams('year=-2024')).year).toBeNull();
		expect(parseSubjectScope(new URLSearchParams('year=2024.5')).year).toBeNull();
	});

	it('serialises only the filters that are set and encodes their values', () => {
		expect(subjectScopeSearch()).toBe('');
		expect(subjectScopeSearch({ province: null, district: 'Ignored', year: 2023 })).toBe(
			'?year=2023'
		);
		expect(subjectScopeSearch({ province: 'NORTH WEST', district: 'Bojanala & Co' })).toBe(
			'?province=NORTH+WEST&district=Bojanala+%26+Co'
		);
	});

	it('links to the subject detail page with an encoded code and the scope', () => {
		expect(subjectHref('mathematics')).toBe('/subjects/mathematics');
		expect(subjectHref('life sciences', { province: 'LIMPOPO', year: 2024 })).toBe(
			'/subjects/life%20sciences?province=LIMPOPO&year=2024'
		);
	});

	it('links back to the subject list with the same scope', () => {
		expect(subjectsListHref()).toBe('/subjects');
		expect(subjectsListHref({ province: 'GAUTENG', district: 'Tshwane', year: 2024 })).toBe(
			'/subjects?province=GAUTENG&district=Tshwane&year=2024'
		);
	});
});
