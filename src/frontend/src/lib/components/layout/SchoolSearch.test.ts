// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { flushSync, mount, tick, unmount } from 'svelte';
import SchoolSearch from './SchoolSearch.svelte';
import { globalShortcuts } from '$lib/state/shortcuts.svelte';
import type { School } from '$lib/types/dashboard';

const mocks = vi.hoisted(() => ({
	goto: vi.fn(),
	fetchSchools: vi.fn(),
	page: {
		url: { search: '', pathname: '/overview' },
		state: {} as Record<string, unknown>,
		params: {} as Record<string, string>,
		route: { id: '/(app)/overview' }
	}
}));

vi.mock('$app/navigation', () => ({ goto: mocks.goto }));
vi.mock('$app/paths', () => ({ resolve: (path: string) => path }));
vi.mock('$app/state', () => ({ page: mocks.page }));
vi.mock('$lib/api/dashboard', () => ({ fetchSchools: mocks.fetchSchools }));

const schools: School[] = [
	{
		id: '925630449',
		name: 'MAPUTLE SECONDARY SCHOOL',
		province: 'LIMPOPO',
		circuit: 'SEKHUKHUNE EAST',
		totalWrote: 100,
		totalPassed: 98,
		passRate: 98.4,
		totalAchieved: null
	},
	{
		id: '443611134',
		name: 'FEZILE DABI SECONDARY',
		province: 'FREE STATE',
		circuit: 'FEZILE DABI',
		totalWrote: 80,
		totalPassed: 60,
		passRate: 75,
		totalAchieved: null
	}
];

let component: ReturnType<typeof mount> | undefined;

function mountSearch() {
	const target = document.createElement('div');
	document.body.append(target);
	component = mount(SchoolSearch, { target, props: {} });
	return target;
}

async function search(target: HTMLElement, text = 'school') {
	const input = target.querySelector('input');
	if (!input) throw new Error('Missing search input');
	input.value = text;
	input.dispatchEvent(new Event('input', { bubbles: true }));
	flushSync();
	await vi.advanceTimersByTimeAsync(300);
	await tick();
	return target.querySelector<HTMLButtonElement>('[role="option"]');
}

beforeEach(() => {
	vi.useFakeTimers();
	vi.clearAllMocks();
	mocks.page.url = { search: '', pathname: '/overview' };
	mocks.page.state = {};
	mocks.page.params = {};
	mocks.page.route = { id: '/(app)/overview' };
	mocks.fetchSchools.mockResolvedValue({ items: schools, totalCount: 42 });
});

afterEach(async () => {
	if (component) await unmount(component);
	component = undefined;
	document.body.replaceChildren();
	vi.useRealTimers();
});

describe('school search navigation', () => {
	it('selects the result in the overview from the overview page', async () => {
		const target = mountSearch();
		const option = await search(target);
		option?.click();
		await tick();

		expect(mocks.goto).toHaveBeenCalledTimes(1);
		const [url, options] = mocks.goto.mock.calls[0] as [string, { state: object }];
		expect(url).toContain('/overview?');
		expect(url).toContain('school=925630449');
		expect(options.state).toMatchObject({ overviewReveal: 1 });
	});

	it("opens another school's page while on a school page", async () => {
		mocks.page.url = { search: '', pathname: '/schools/443611134' };
		mocks.page.params = { id: '443611134' };
		mocks.page.route = { id: '/(app)/schools/[id]' };
		const target = mountSearch();
		const option = await search(target);
		option?.click();
		await tick();

		expect(mocks.goto).toHaveBeenCalledWith('/schools/925630449');
	});

	it('stays put when the current school is searched from its own page', async () => {
		mocks.page.url = { search: '', pathname: '/schools/925630449' };
		mocks.page.params = { id: '925630449' };
		mocks.page.route = { id: '/(app)/schools/[id]' };
		const target = mountSearch();
		const option = await search(target);
		option?.click();
		await tick();

		expect(mocks.goto).not.toHaveBeenCalled();
	});
});

function press(target: HTMLElement, key: string) {
	const input = target.querySelector('input');
	if (!input) throw new Error('Missing search input');
	input.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }));
	flushSync();
}

describe('school search experience', () => {
	it('opens the top result on Enter without arrowing first', async () => {
		const target = mountSearch();
		await search(target, 'maputle');
		press(target, 'Enter');

		const [url] = mocks.goto.mock.calls[0] as [string];
		expect(url).toContain('school=925630449');
	});

	it('wraps from the last row to the first with the arrow keys', async () => {
		const target = mountSearch();
		await search(target);
		press(target, 'ArrowUp');
		const selected = target.querySelector('[role="option"][aria-selected="true"]');
		expect(selected?.textContent).toContain('See all 42 results');
		press(target, 'ArrowDown');
		const first = target.querySelector('[role="option"][aria-selected="true"]');
		expect(first?.textContent).toContain('Maputle Secondary School');
	});

	it('links to the full directory with the current search', async () => {
		const target = mountSearch();
		await search(target, 'secondary');
		const options = target.querySelectorAll<HTMLElement>('[role="option"]');
		options[options.length - 1]?.click();

		expect(mocks.goto).toHaveBeenCalledWith('/schools?search=secondary');
	});

	it('highlights the searched term in school names', async () => {
		const target = mountSearch();
		await search(target, 'fezile');
		const marks = [...target.querySelectorAll('mark')].map((mark) => mark.textContent);
		expect(marks).toEqual(['Fezile']);
	});

	it('explains when no schools match', async () => {
		mocks.fetchSchools.mockResolvedValue({ items: [], totalCount: 0 });
		const target = mountSearch();
		await search(target, 'zzz');

		expect(target.textContent).toContain('No schools match');
		expect(target.querySelector('[role="option"]')).toBeNull();
	});

	it('shows an error message when the search request fails', async () => {
		mocks.fetchSchools.mockRejectedValue(new Error('offline'));
		const target = mountSearch();
		await search(target, 'maputle');

		expect(target.textContent).toContain('Search is unavailable');
	});

	it('ignores a slow response that arrives after a newer search', async () => {
		let resolveFirst: (value: unknown) => void = () => {};
		mocks.fetchSchools
			.mockImplementationOnce(() => new Promise((resolve) => (resolveFirst = resolve)))
			.mockResolvedValueOnce({ items: [schools[1]], totalCount: 1 });
		const target = mountSearch();
		await search(target, 'map');
		await search(target, 'fezile');
		resolveFirst({ items: [schools[0]], totalCount: 1 });
		await tick();

		const names = [...target.querySelectorAll('[role="option"]')].map((o) => o.textContent);
		expect(names[0]).toContain('Fezile Dabi Secondary');
		expect(names).toHaveLength(2);
	});

	it('clears the query with the clear button', async () => {
		const target = mountSearch();
		await search(target);
		target.querySelector<HTMLButtonElement>('button[aria-label="Clear search"]')?.click();
		flushSync();

		expect(target.querySelector('input')?.value).toBe('');
		expect(target.querySelector('[role="option"]')).toBeNull();
	});

	it('focuses the search input with the / shortcut', () => {
		const target = mountSearch();
		flushSync();
		const shortcuts = globalShortcuts(window);
		window.dispatchEvent(
			new KeyboardEvent('keydown', { key: '/', code: 'Slash', bubbles: true, cancelable: true })
		);

		expect(document.activeElement).toBe(target.querySelector('input'));
		shortcuts.destroy();
	});
});
