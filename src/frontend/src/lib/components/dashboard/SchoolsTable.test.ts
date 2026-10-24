// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { mount, tick, unmount } from 'svelte';
import SchoolsTable from './SchoolsTable.svelte';
import { fetchSchools } from '$lib/api/dashboard';
import type { SchoolList } from '$lib/types/dashboard';

vi.mock('$app/navigation', () => ({ goto: vi.fn() }));
vi.mock('$app/paths', () => ({ resolve: (path: string) => path }));
vi.mock('$lib/api/dashboard', () => ({ fetchSchools: vi.fn() }));

const fetchSchoolsMock = vi.mocked(fetchSchools);
let component: ReturnType<typeof mount> | undefined;

function schoolList(): SchoolList {
	return {
		items: [],
		totalCount: 0,
		pageNumber: 1,
		pageSize: 10,
		totalPages: 1,
		hasPreviousPage: false,
		hasNextPage: false
	};
}

async function render(props: Record<string, unknown> = {}) {
	const target = document.createElement('div');
	document.body.append(target);
	component = mount(SchoolsTable, { target, props });
	await tick();
	return target;
}

beforeEach(() => {
	fetchSchoolsMock.mockResolvedValue(schoolList());
});

afterEach(async () => {
	if (component) await unmount(component);
	component = undefined;
	document.body.replaceChildren();
	vi.useRealTimers();
});

describe('school directory requests', () => {
	it('loads the deep-linked search on mount', async () => {
		await render({ externalSearch: 'pretoria' });

		expect(fetchSchoolsMock).toHaveBeenCalledTimes(1);
		expect(fetchSchoolsMock.mock.calls[0]?.[0]).toMatchObject({
			pageNumber: 1,
			pageSize: 10,
			search: 'pretoria'
		});
	});

	it('debounces typing into a single request and resets to the first page', async () => {
		vi.useFakeTimers();
		const target = await render();
		expect(fetchSchoolsMock).toHaveBeenCalledTimes(1);

		const input = target.querySelector('input')!;
		for (const value of ['p', 'pr', 'pre']) {
			input.value = value;
			input.dispatchEvent(new Event('input', { bubbles: true }));
			await tick();
		}

		expect(fetchSchoolsMock).toHaveBeenCalledTimes(1);
		await vi.advanceTimersByTimeAsync(300);

		expect(fetchSchoolsMock).toHaveBeenCalledTimes(2);
		expect(fetchSchoolsMock.mock.calls[1]?.[0]).toMatchObject({
			pageNumber: 1,
			search: 'pre'
		});
	});

	it('does not apply a response the user already replaced', async () => {
		vi.useFakeTimers();
		const target = await render();
		const stale = deferred<SchoolList>();
		fetchSchoolsMock.mockReturnValueOnce(stale.promise);

		const input = target.querySelector('input')!;
		input.value = 'stale';
		input.dispatchEvent(new Event('input', { bubbles: true }));
		await vi.advanceTimersByTimeAsync(300);

		input.value = 'fresh';
		input.dispatchEvent(new Event('input', { bubbles: true }));
		await vi.advanceTimersByTimeAsync(300);

		stale.resolve({ ...schoolList(), totalCount: 999 });
		await tick();

		expect(fetchSchoolsMock.mock.calls.at(-1)?.[0]).toMatchObject({ search: 'fresh' });
		expect(target.textContent).not.toContain('999 schools');
	});
});

describe('school directory filters and shareable state', () => {
	it('starts from the route province, sort and page', async () => {
		await render({
			externalProvince: 'GAUTENG',
			externalSort: 'passRate',
			externalOrder: 'desc',
			externalPage: 3
		});
		await tick();

		expect(fetchSchoolsMock).toHaveBeenCalledTimes(1);
		expect(fetchSchoolsMock.mock.calls[0]?.[0]).toMatchObject({
			pageNumber: 3,
			province: 'GAUTENG',
			sort: 'passRate',
			order: 'desc'
		});
		const header = [...document.querySelectorAll('th')].find((th) =>
			th.textContent?.includes('Pass Rate')
		);
		expect(header?.getAttribute('aria-sort')).toBe('descending');
	});

	it('only shows the province filter when options are provided', async () => {
		const plain = await render();
		expect(plain.querySelector('[aria-label="Province"]')).toBeNull();
		await unmount(component!);
		component = undefined;
		document.body.replaceChildren();

		const filtered = await render({ provinces: ['GAUTENG', 'LIMPOPO'] });
		expect(filtered.querySelector('[aria-label="Province"]')?.textContent).toContain(
			'All provinces'
		);
	});

	it('labels a deep-linked province before the option list has loaded', async () => {
		const target = await render({ provinces: [], externalProvince: 'NORTH WEST' });
		expect(target.querySelector('[aria-label="Province"]')?.textContent).toContain('North West');
	});

	it('reports the applied view so the page can mirror it in the URL', async () => {
		fetchSchoolsMock.mockResolvedValue({ ...schoolList(), totalPages: 2, hasNextPage: true });
		const onQueryChange = vi.fn();
		const target = await render({
			externalSearch: 'pretoria',
			externalProvince: 'GAUTENG',
			onQueryChange
		});
		await tick();
		expect(onQueryChange).toHaveBeenLastCalledWith({
			search: 'pretoria',
			province: 'GAUTENG',
			sort: null,
			order: null,
			page: 1
		});

		const next = [...target.querySelectorAll('button')].find((button) =>
			button.textContent?.includes('Next')
		);
		next?.click();
		await tick();
		expect(onQueryChange).toHaveBeenLastCalledWith(expect.objectContaining({ page: 2 }));
		expect(fetchSchoolsMock.mock.calls.at(-1)?.[0]).toMatchObject({
			pageNumber: 2,
			province: 'GAUTENG'
		});
	});
});

function deferred<T>(): { promise: Promise<T>; resolve: (value: T) => void } {
	let resolve!: (value: T) => void;
	const promise = new Promise<T>((next) => {
		resolve = next;
	});
	return { promise, resolve };
}
