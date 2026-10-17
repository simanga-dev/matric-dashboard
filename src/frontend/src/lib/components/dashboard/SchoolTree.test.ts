// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { mount, tick, unmount } from 'svelte';
import SchoolTree from './SchoolTree.svelte';
import source from './SchoolTree.svelte?raw';
import type { School } from '$lib/types/dashboard';

vi.mock('$app/navigation', () => ({ goto: vi.fn() }));
vi.mock('$app/paths', () => ({ resolve: (path: string) => path }));
vi.mock('./school-detail-cache', () => ({ schoolDetailCache: { prefetch: vi.fn() } }));

const schools: School[] = Array.from({ length: 1000 }, (_, i) => ({
	id: String(i),
	name: `School ${String(i).padStart(4, '0')}`,
	province: 'Gauteng',
	circuit: 'Central',
	passRate: 85,
	totalWrote: 100,
	totalPassed: 85,
	totalAchieved: null
}));

let component: ReturnType<typeof mount>;

function button(name: string): HTMLButtonElement {
	const found = [...document.querySelectorAll('button')].find(
		(button) => (button.getAttribute('aria-label') ?? button.textContent?.trim()) === name
	);
	if (!found) throw new Error(`Button not found: ${name}`);
	return found;
}

function scroller(): HTMLDivElement {
	const element = document.querySelector<HTMLDivElement>('.tree-scroll');
	if (!element) throw new Error('Tree scroll area not found');
	return element;
}

function input(): HTMLInputElement {
	const element = document.querySelector('input');
	if (!element) throw new Error('Filter not found');
	return element;
}

async function filter(value: string) {
	input().value = value;
	input().dispatchEvent(new Event('input', { bubbles: true }));
	await tick();
}

async function scroll(top: number) {
	scroller().scrollTop = top;
	scroller().dispatchEvent(new Event('scroll'));
	await tick();
}

async function key(element: Element, key: string) {
	element.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }));
	await tick();
}

beforeEach(async () => {
	vi.useFakeTimers();
	vi.stubGlobal(
		'ResizeObserver',
		class {
			observe() {}
			unobserve() {}
			disconnect() {}
		}
	);
	vi.spyOn(HTMLElement.prototype, 'clientHeight', 'get').mockImplementation(function (
		this: HTMLElement
	) {
		return this.classList.contains('row-measure') ? 44 : 400;
	});
	const target = document.createElement('div');
	document.body.append(target);
	component = mount(SchoolTree, { target, props: { initialSchools: schools } });
	await tick();
});

afterEach(async () => {
	await unmount(component);
	document.body.replaceChildren();
	vi.useRealTimers();
	vi.unstubAllGlobals();
});

describe('SchoolTree interactions', () => {
	it('keeps a visible Tab entry after scrolling without first focusing the tree', async () => {
		button('Expand all').click();
		await tick();
		await scroll(22_000);
		const entries = document.querySelectorAll<HTMLElement>('[role="treeitem"][tabindex="0"]');
		expect(entries).toHaveLength(1);
		const entry = entries[0]!;
		const index = Number(entry.dataset.rowIndex);
		expect(index).toBe(500);
		entry.focus();
		await key(entry, 'ArrowDown');
		expect((document.activeElement as HTMLElement).dataset.rowIndex).toBe('501');
		expect(document.querySelectorAll('[role="treeitem"]').length).toBeLessThan(30);
	});

	it('keeps a focused row mounted while the viewport moves away', async () => {
		button('Expand all').click();
		await tick();
		const root = document.querySelector<HTMLElement>('[role="treeitem"]')!;
		root.focus();
		await tick();
		await scroll(22_000);
		expect(document.activeElement).toBe(root);
		expect(root.isConnected).toBe(true);
		expect(root.tabIndex).toBe(0);
		input().focus();
		await tick();
		expect(
			document.querySelector<HTMLElement>('[role="treeitem"][tabindex="0"]')?.dataset.rowIndex
		).toBe('500');
	});

	it('enters filtered schools from the input and selects the focused result', async () => {
		await filter('School 000');
		input().focus();
		await key(input(), 'ArrowDown');
		expect(document.activeElement?.getAttribute('aria-label')).toBe('School 0000');
		await key(document.activeElement!, 'ArrowDown');
		expect(document.activeElement?.getAttribute('aria-label')).toBe('School 0001');
		await key(document.activeElement!, 'Enter');
		expect(document.activeElement?.getAttribute('aria-selected')).toBe('true');
		await key(document.activeElement!, 'Home');
		await key(document.activeElement!, 'ArrowUp');
		expect(document.activeElement).toBe(input());
	});

	it('selects the current province and district from the pinned breadcrumbs', async () => {
		button('Expand all').click();
		await tick();
		await scroll(22_000);
		button('District overview for Central in Gauteng').click();
		await tick();
		await scroll(0);
		expect(
			document
				.querySelector('[role="treeitem"][aria-label="Central"]')
				?.getAttribute('aria-selected')
		).toBe('true');
		await scroll(22_000);
		button('Province overview for Gauteng').click();
		await tick();
		await scroll(0);
		expect(
			document
				.querySelector('[role="treeitem"][aria-label="Gauteng"]')
				?.getAttribute('aria-selected')
		).toBe('true');
		button('Country overview').click();
		await tick();
		expect(
			document
				.querySelector('[role="treeitem"][aria-label="South Africa"]')
				?.getAttribute('aria-selected')
		).toBe('true');
	});

	it('preserves browse scroll and surviving row nodes across filter edits', async () => {
		button('Expand all').click();
		await tick();
		await scroll(22_000);
		await filter('School');
		const row = document.querySelector('[data-school-id="0"]');
		await filter('School 000');
		expect(document.querySelector('[data-school-id="0"]')).toBe(row);
		button('Clear school filter').click();
		await tick();
		await tick();
		expect(scroller().scrollTop).toBe(22_000);
	});

	it('settles the shared icon/text motion without any opacity-reset keyframes', async () => {
		await filter('School 000');
		expect(document.querySelector('.tree-reveal')).toBeNull();
		await vi.advanceTimersByTimeAsync(180);
		await tick();
		expect(document.querySelector('.tree-reveal')).not.toBeNull();
		// jsdom does not animate. Inspect the actual stylesheet keyframes so
		// a future opacity reset cannot reintroduce the delayed disappearance.
		const style = document.createElement('style');
		style.textContent = source.split('<style>')[1]!.split('</style>')[0]!;
		document.head.append(style);
		try {
			const frames = [...style.sheet!.cssRules].find((rule) => rule.type === 7) as CSSKeyframesRule;
			expect(frames).toBeDefined();
			for (const frame of frames.cssRules) {
				expect(Number((frame as CSSKeyframeRule).style.opacity || '1')).toBe(1);
			}
		} finally {
			style.remove();
		}
	});
});
