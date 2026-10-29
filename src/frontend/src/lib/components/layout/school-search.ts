/** A slice of a label, flagged when it matches part of the search query. */
export interface HighlightPart {
	text: string;
	match: boolean;
}

function escapeRegExp(value: string): string {
	return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

/**
 * Splits a label into matched and unmatched parts so the search terms can be
 * emphasised. Each whitespace-separated term is matched case-insensitively at
 * the start of a word, mirroring how the search index matches prefixes.
 */
export function highlightParts(text: string, query: string): HighlightPart[] {
	const terms = query
		.trim()
		.split(/\s+/)
		.filter(Boolean)
		.sort((a, b) => b.length - a.length)
		.map(escapeRegExp);
	if (terms.length === 0) return [{ text, match: false }];

	const pattern = new RegExp(`(?<![\\p{L}\\p{N}])(${terms.join('|')})`, 'giu');
	const parts: HighlightPart[] = [];
	let last = 0;
	for (const found of text.matchAll(pattern)) {
		const start = found.index;
		if (start > last) parts.push({ text: text.slice(last, start), match: false });
		parts.push({ text: found[0], match: true });
		last = start + found[0].length;
	}
	if (last < text.length) parts.push({ text: text.slice(last), match: false });
	return parts;
}
