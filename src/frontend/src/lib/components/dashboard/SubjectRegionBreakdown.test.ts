// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from 'vitest';
import { mount, tick, unmount } from 'svelte';
import SubjectRegionBreakdown from './SubjectRegionBreakdown.svelte';
import type { SubjectRegionBreakdown as Row } from '$lib/types/dashboard';

let component: ReturnType<typeof mount> | undefined;

const rows: Row[] = [
	{ name: 'LIMPOPO', schoolCount: 10, totalWrote: 500, achieved: null, percentAchieved: null },
	{ name: 'GAUTENG', schoolCount: 20, totalWrote: 900, achieved: 720, percentAchieved: 80 },
	{ name: 'FREE STATE', schoolCount: 8, totalWrote: 300, achieved: 255, percentAchieved: 85 }
];

async function render(props: Record<string, unknown>) {
	const target = document.createElement('div');
	document.body.append(target);
	component = mount(SubjectRegionBreakdown, {
		target,
		props: { rows, kind: 'province', onSelect: vi.fn(), ...props }
	});
	await tick();
	return target;
}

afterEach(async () => {
	if (component) await unmount(component);
	component = undefined;
	document.body.replaceChildren();
});

describe('subject regional breakdown', () => {
	it('orders regions by pass rate with unavailable results last', async () => {
		const target = await render({});
		const names = [...target.querySelectorAll('[data-region-row]')].map((row) =>
			row.getAttribute('data-region-row')
		);
		expect(names).toEqual(['FREE STATE', 'GAUTENG', 'LIMPOPO']);
		expect(target.textContent).toContain('Free State');
	});

	it('drills into a region by click or keyboard and marks the selected one', async () => {
		const onSelect = vi.fn();
		const target = await render({ onSelect, selected: 'GAUTENG', kind: 'district' });
		const gauteng = target.querySelector<HTMLElement>('[data-region-row="GAUTENG"]')!;
		const limpopo = target.querySelector<HTMLElement>('[data-region-row="LIMPOPO"]')!;

		gauteng.click();
		limpopo.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));

		expect(onSelect.mock.calls).toEqual([['GAUTENG'], ['LIMPOPO']]);
		expect(gauteng.getAttribute('aria-current')).toBe('true');
		expect(limpopo.hasAttribute('aria-current')).toBe(false);
		expect(target.querySelector('th')?.textContent).toBe('District');
	});
});
