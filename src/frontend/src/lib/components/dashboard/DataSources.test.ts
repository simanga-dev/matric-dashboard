// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { mount, tick, unmount } from 'svelte';
import DataSources from './DataSources.svelte';

let component: ReturnType<typeof mount> | undefined;

beforeEach(() => {
	vi.stubGlobal(
		'matchMedia',
		() =>
			({
				matches: false,
				addEventListener() {},
				removeEventListener() {},
				addListener() {},
				removeListener() {}
			}) as unknown as MediaQueryList
	);
	vi.stubGlobal(
		'ResizeObserver',
		class {
			observe() {}
			unobserve() {}
			disconnect() {}
		}
	);
});

afterEach(async () => {
	if (component) await unmount(component);
	component = undefined;
	document.body.replaceChildren();
	vi.unstubAllGlobals();
});

function triggerButton(): HTMLButtonElement {
	const button = [...document.querySelectorAll('button')].find((candidate) =>
		candidate.textContent?.includes('Data sources')
	);
	if (!button) throw new Error('Data sources trigger not found');
	return button;
}

describe('DataSources', () => {
	it('names the DBE as the source on the page itself', () => {
		component = mount(DataSources, { target: document.body });

		expect(document.body.textContent).toContain('Source');
		expect(document.body.textContent).toContain('Department of Basic Education');
		expect(triggerButton().getAttribute('aria-expanded')).toBe('false');
	});

	it('opens a dialog that explains each source', async () => {
		component = mount(DataSources, { target: document.body });

		triggerButton().click();
		await tick();
		await vi.waitFor(() => expect(document.querySelector('[role="dialog"]')).not.toBeNull());

		const dialog = document.querySelector('[role="dialog"]');
		expect(dialog?.textContent).toContain('Where this data comes from');
		expect(dialog?.textContent).toContain('NSC results');
		expect(dialog?.textContent).toContain('School directory');
		expect(dialog?.textContent).toContain('Locations and boundaries');
		expect(dialog?.textContent).toContain('Basemap');
		expect(triggerButton().getAttribute('aria-expanded')).toBe('true');

		const links = [...(dialog?.querySelectorAll('a') ?? [])];
		expect(links).toHaveLength(4);
		for (const link of links) {
			expect(link.getAttribute('href')).toMatch(/^https:\/\//);
			expect(link.getAttribute('target')).toBe('_blank');
			expect(link.getAttribute('rel')).toBe('noreferrer');
		}
	});
});
