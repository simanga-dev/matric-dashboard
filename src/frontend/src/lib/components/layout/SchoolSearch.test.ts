// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { flushSync, mount, tick, unmount } from 'svelte';
import SchoolSearch from './SchoolSearch.svelte';
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
	mocks.fetchSchools.mockResolvedValue({ items: schools });
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
