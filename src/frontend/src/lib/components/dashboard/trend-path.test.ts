import { describe, expect, it } from 'vitest';
import { buildTrendPaths, normalizeTrend } from './trend-path';

describe('trend observations', () => {
	it('sorts years, drops invalid values, and keeps the last duplicate', () => {
		expect(
			normalizeTrend([
				{ year: 2024, passRate: 85 },
				{ year: 2020, passRate: 75 },
				{ year: 2024, passRate: 88 },
				{ year: NaN, passRate: 70 },
				{ year: 2022, passRate: Infinity }
			])
		).toEqual([
			{ year: 2020, passRate: 75 },
			{ year: 2024, passRate: 88 }
		]);
	});
});

describe('smooth trend paths', () => {
	it('handles empty and single-point charts without an area', () => {
		expect(buildTrendPaths([], 100)).toEqual({ line: '', area: '' });
		expect(buildTrendPaths([{ x: 10, y: 20 }], 100)).toEqual({ line: 'M 10 20', area: '' });
	});

	it('uses cubic curves and closes the area along the baseline', () => {
		const paths = buildTrendPaths(
			[
				{ x: 0, y: 10 },
				{ x: 30, y: 40 }
			],
			100
		);
		expect(paths.line).toBe('M 0 10 C 10 20 20 30 30 40');
		expect(paths.area).toBe(`${paths.line} L 30 100 L 0 100 Z`);
	});

	it.each([
		[80, 70, 90, 20, 50],
		[10, 10, 10, 10, 10],
		[0, 20, 40, 80, 100],
		[100, 80, 40, 20, 0],
		[0, 100, 0, 100, 0]
	])('does not overshoot observations for %j', (...rates) => {
		const points = rates.map((y, index) => ({ x: [0, 2, 20, 21, 100][index] ?? 0, y }));
		const { line } = buildTrendPaths(points, 120);
		line
			.split(' C ')
			.slice(1)
			.forEach((segment, index) => {
				const [cx1, cy1, cx2, cy2, x, y] = segment.split(' ').map(Number);
				const start = points[index];
				if (!start || cy1 === undefined || cy2 === undefined || y === undefined)
					throw new Error('Missing curve coordinates');
				expect([cx1, cy1, cx2, cy2, x, y].every(Number.isFinite)).toBe(true);
				for (let step = 0; step <= 100; step++) {
					const t = step / 100;
					const value =
						(1 - t) ** 3 * start.y +
						3 * (1 - t) ** 2 * t * cy1 +
						3 * (1 - t) * t ** 2 * cy2 +
						t ** 3 * y;
					expect(value).toBeGreaterThanOrEqual(Math.min(start.y, y) - 1e-8);
					expect(value).toBeLessThanOrEqual(Math.max(start.y, y) + 1e-8);
				}
			});
	});

	it('rejects duplicate or reversed horizontal coordinates', () => {
		expect(() =>
			buildTrendPaths(
				[
					{ x: 5, y: 1 },
					{ x: 5, y: 2 }
				],
				10
			)
		).toThrow('increasing x');
		expect(() =>
			buildTrendPaths(
				[
					{ x: 5, y: 1 },
					{ x: 0, y: 2 }
				],
				10
			)
		).toThrow('increasing x');
	});
});
