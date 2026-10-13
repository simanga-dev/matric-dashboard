import { describe, expect, it } from 'vitest';
import { resizePair } from './resize-pair';

describe('workspace panel resizing', () => {
	it('preserves the pair total so the third panel stays the same size', () => {
		expect(resizePair(26, 46, 10)).toEqual([36, 36]);
		expect(resizePair(46, 28, -10)).toEqual([36, 38]);
	});

	it('clamps either panel at its minimum without increasing the pair total', () => {
		expect(resizePair(26, 46, 100)).toEqual([64, 8]);
		expect(resizePair(26, 46, -100)).toEqual([8, 64]);
	});

	it('supports repeated keyboard-sized adjustments', () => {
		const first = resizePair(26, 46, 2);
		expect(resizePair(...first, 2)).toEqual([30, 42]);
		expect(resizePair(...first, -2)).toEqual([26, 46]);
	});
});
