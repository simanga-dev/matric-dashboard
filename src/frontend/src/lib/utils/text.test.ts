import { describe, expect, it } from 'vitest';
import { toTitleCase } from './text';

describe('toTitleCase', () => {
	it('converts all-caps school names to title case', () => {
		expect(toTitleCase('AZALEA COMBINED SCHOOL')).toBe('Azalea Combined School');
		expect(toTitleCase('BANTFWABETHU SECONDARY SCHOOL')).toBe('Bantfwabethu Secondary School');
	});

	it('keeps existing mixed case readable', () => {
		expect(toTitleCase('Alpha High')).toBe('Alpha High');
	});

	it('capitalises each part of hyphenated and dotted names', () => {
		expect(toTitleCase('SEDIBENG-THABO HIGH')).toBe('Sedibeng-Thabo High');
		expect(toTitleCase('P.O. BOX 123')).toBe('P.O. Box 123');
	});

	it('leaves possessives and bracketed words intact', () => {
		expect(toTitleCase("ST MARY'S PRIMARY")).toBe("St Mary's Primary");
		expect(toTitleCase('PRIMARY (FARM) SCHOOL')).toBe('Primary (Farm) School');
	});

	it('handles empty and whitespace-only input', () => {
		expect(toTitleCase('')).toBe('');
		expect(toTitleCase('   ')).toBe('   ');
	});

	it('uses the canonical spelling for known region names', () => {
		expect(toTitleCase('KWAZULU-NATAL')).toBe('KwaZulu-Natal');
		expect(toTitleCase('  kwaZulu-natal ')).toBe('KwaZulu-Natal');
	});
});
