const CANONICAL_NAMES: Record<string, string> = {
	'KWAZULU-NATAL': 'KwaZulu-Natal'
};

export function toTitleCase(value: string): string {
	const canonical = CANONICAL_NAMES[value.trim().toUpperCase()];
	if (canonical) return canonical;
	return value
		.toLowerCase()
		.split(/([\s\-/.()]+)/)
		.map((part) => (/^\p{L}/u.test(part) ? part.charAt(0).toUpperCase() + part.slice(1) : part))
		.join('');
}
