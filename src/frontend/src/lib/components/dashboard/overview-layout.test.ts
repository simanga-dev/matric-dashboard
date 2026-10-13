import { describe, expect, it } from 'vitest';
import { overviewWidths } from './overview-layout';

const defaultSizes = [20, 52, 28];

describe('overview workspace widths', () => {
	it('uses the panel proportions when both sides are expanded', () => {
		const layout = overviewWidths(1216, defaultSizes, false, false);
		expect(layout.rail).toBe(40);
		expect(layout.tree).toBeCloseTo(240);
		expect(layout.map).toBeCloseTo(624);
		expect(layout.details).toBeCloseTo(336);
	});

	it("gives the tree's freed space to the map and keeps the details width", () => {
		const layout = overviewWidths(1216, defaultSizes, true, false);
		expect(layout.rail).toBe(40);
		expect(layout.tree).toBe(40);
		expect(layout.details).toBeCloseTo(336);
		expect(layout.map).toBeCloseTo(832);
	});

	it("gives the details' freed space to the map and keeps the tree width", () => {
		const layout = overviewWidths(1216, defaultSizes, false, true);
		expect(layout.tree).toBeCloseTo(240);
		expect(layout.details).toBe(40);
		expect(layout.map).toBeCloseTo(928);
	});

	it('leaves the map all remaining space when both sides are collapsed', () => {
		const layout = overviewWidths(1216, defaultSizes, true, true);
		expect(layout.rail).toBe(40);
		expect(layout.tree).toBe(40);
		expect(layout.details).toBe(40);
		expect(layout.map).toBeCloseTo(1136);
	});

	it('honours resized panel proportions', () => {
		expect(overviewWidths(1216, [20, 45, 35], false, false).details).toBeCloseTo(420);
		expect(overviewWidths(1216, [30, 42, 28], false, false).tree).toBeCloseTo(360);
	});

	it('never produces negative widths in a narrow workspace', () => {
		expect(overviewWidths(50, defaultSizes, true, true)).toEqual({
			rail: 40,
			tree: 40,
			map: 0,
			details: 40
		});
		expect(overviewWidths(0, defaultSizes, false, false)).toEqual({
			rail: 40,
			tree: 0,
			map: 0,
			details: 0
		});
	});
});
