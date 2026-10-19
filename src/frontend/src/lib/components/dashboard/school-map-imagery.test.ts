import { describe, expect, it } from 'vitest';
import { mapImagery, attributionHtml } from './school-map-imagery';

describe('approved imagery configuration', () => {
	it('does not select a provider by default and requires both settings', () => {
		expect(mapImagery()).toBeNull();
		expect(mapImagery('https://approved.test/{z}/{x}/{y}')).toBeNull();
		expect(mapImagery('', 'Approved provider')).toBeNull();
	});
	it('requires HTTPS XYZ templates without credentials or unsupported tokens', () => {
		for (const url of [
			'http://approved.test/{z}/{x}/{y}',
			'https://user:secret@approved.test/{z}/{x}/{y}',
			'https://approved.test/{z}/{x}',
			'https://approved.test/{z}/{x}/{y}/{key}',
			'javascript:alert(1)'
		])
			expect(mapImagery(url, 'Provider')).toBeNull();
		expect(mapImagery('https://approved.test/{z}/{x}/{y}.jpg?publicKey=123', 'Provider')).toEqual({
			url: 'https://approved.test/{z}/{x}/{y}.jpg?publicKey=123',
			attribution: 'Provider'
		});
	});
	it('escapes configured attribution instead of injecting HTML', () => {
		expect(attributionHtml('<img onerror="alert(1)"> & provider')).toBe(
			'&lt;img onerror=&quot;alert(1)&quot;&gt; &amp; provider'
		);
	});
});
