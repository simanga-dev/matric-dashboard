export type TrendPoint = { year: number; passRate: number; wrote?: number | null };
export type ChartPoint = { x: number; y: number };

/** Sort valid observations and retain the last observation for duplicate years. */
export function normalizeTrend(points: readonly TrendPoint[]): TrendPoint[] {
	const years = new Map<number, TrendPoint>();
	for (const point of points) {
		if (Number.isFinite(point.year) && Number.isFinite(point.passRate)) {
			years.set(point.year, point);
		}
	}
	return [...years.values()].sort((a, b) => a.year - b.year);
}

/** Monotone cubic interpolation smooths the line without inventing new extrema. */
export function buildTrendPaths(points: readonly ChartPoint[], baseline: number) {
	const first = points[0];
	const last = points.at(-1);
	if (!first || !last) return { line: '', area: '' };
	if (points.length === 1) return { line: `M ${first.x} ${first.y}`, area: '' };

	const slopes = points.slice(1).map((point, index) => {
		const previous = points[index];
		if (!previous || point.x <= previous.x)
			throw new Error('Chart points must have increasing x coordinates');
		return (point.y - previous.y) / (point.x - previous.x);
	});
	const tangents = points.map((point, index) => {
		if (index === 0) return slopes[0] ?? 0;
		if (index === points.length - 1) return slopes.at(-1) ?? 0;
		const before = slopes[index - 1] ?? 0;
		const after = slopes[index] ?? 0;
		const previous = points[index - 1];
		const next = points[index + 1];
		if (!previous || !next || before * after <= 0) return 0;
		const left = point.x - previous.x;
		const right = next.x - point.x;
		const w1 = 2 * right + left;
		const w2 = right + 2 * left;
		return (w1 + w2) / (w1 / before + w2 / after);
	});

	let line = `M ${first.x} ${first.y}`;
	for (let index = 1; index < points.length; index++) {
		const previous = points[index - 1];
		const point = points[index];
		if (!previous || !point) continue;
		const step = (point.x - previous.x) / 3;
		line += ` C ${previous.x + step} ${previous.y + step * (tangents[index - 1] ?? 0)} ${point.x - step} ${point.y - step * (tangents[index] ?? 0)} ${point.x} ${point.y}`;
	}
	return { line, area: `${line} L ${last.x} ${baseline} L ${first.x} ${baseline} Z` };
}
