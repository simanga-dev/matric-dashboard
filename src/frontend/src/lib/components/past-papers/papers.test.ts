import { describe, expect, it } from 'vitest';
import { formatFileSize, isPreviewable, paperLabel, subjectCategory } from './papers';

describe('past paper presentation', () => {
	it('groups subjects into their card categories', () => {
		expect(subjectCategory('Mathematics')).toBe('mathematics');
		expect(subjectCategory('Mathematical Literacy')).toBe('mathematics');
		expect(subjectCategory('Physical Sciences')).toBe('sciences');
		expect(subjectCategory('Life Sciences')).toBe('sciences');
		expect(subjectCategory('English')).toBe('languages');
		expect(subjectCategory('Afrikaans')).toBe('languages');
		expect(subjectCategory('Accounting')).toBe('commerce');
		expect(subjectCategory('Geography')).toBe('humanities');
		expect(subjectCategory('Information Technology')).toBe('technology');
		expect(subjectCategory('Civil Technology')).toBe('technology');
	});

	it('labels papers by number and variant', () => {
		expect(paperLabel({ paperNumber: 1, variant: null })).toBe('Paper 1');
		expect(paperLabel({ paperNumber: 2, variant: 'Chemistry' })).toBe('Paper 2 (Chemistry)');
		expect(paperLabel({ paperNumber: null, variant: 'Woodworking' })).toBe('Woodworking');
		expect(paperLabel({ paperNumber: null, variant: null })).toBe('Paper');
	});

	it('formats file sizes', () => {
		expect(formatFileSize(512)).toBe('512 B');
		expect(formatFileSize(380 * 1024)).toBe('380 KB');
		expect(formatFileSize(1.3 * 1024 * 1024)).toBe('1.3 MB');
	});

	it('only previews PDFs inline', () => {
		expect(isPreviewable({ fileType: 'pdf' })).toBe(true);
		expect(isPreviewable({ fileType: 'PDF' })).toBe(true);
		expect(isPreviewable({ fileType: 'docx' })).toBe(false);
	});
});
