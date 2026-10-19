export interface MapImagery {
	url: string;
	attribution: string;
}

/** Only approved, explicitly configured HTTPS XYZ sources may be used. */
export function mapImagery(url?: string, attribution?: string): MapImagery | null {
	if (!url?.trim() || !attribution?.trim()) return null;
	try {
		const parsed = new URL(url);
		if (parsed.protocol !== 'https:' || parsed.username || parsed.password || parsed.hash)
			return null;
		if (!['{z}', '{x}', '{y}'].every((token) => url.includes(token))) return null;
		if (url.replaceAll('{z}', '').replaceAll('{x}', '').replaceAll('{y}', '').match(/[{}]/))
			return null;
		return { url: url.trim(), attribution: attribution.trim() };
	} catch {
		return null;
	}
}

/** OpenLayers attribution is HTML. Treat configured provider text as plain text. */
export function attributionHtml(text: string): string {
	return text.replace(
		/[&<>"']/g,
		(character) =>
			({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[character] ??
			character
	);
}
