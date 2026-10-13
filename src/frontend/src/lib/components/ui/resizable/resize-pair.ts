/** Resize adjacent panels while preserving their combined share of the workspace. */
export function resizePair(
	first: number,
	second: number,
	delta: number,
	minimum = 8
): [number, number] {
	const limit = Math.min(minimum, (first + second) / 2);
	const adjustment = Math.max(limit - first, Math.min(second - limit, delta));
	return [first + adjustment, second - adjustment];
}
