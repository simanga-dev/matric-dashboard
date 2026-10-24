// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { flushSync, mount, tick, unmount } from 'svelte';
import SubjectSchoolsTable from './SubjectSchoolsTable.svelte';
import { fetchSubjectSchools } from '$lib/api/dashboard';
import type { SubjectSchoolList } from '$lib/types/dashboard';

vi.mock('$app/paths', () => ({ resolve: (path: string) => path }));
vi.mock('$lib/api/dashboard', () => ({ fetchSubjectSchools: vi.fn() }));

const fetchMock = vi.mocked(fetchSubjectSchools);
let component: ReturnType<typeof mount> | undefined;

function list(overrides: Partial<SubjectSchoolList> = {}): SubjectSchoolList {
	return {
		year: 2024,
		items: [
			{
				id: '700400139',
				name: 'MAMELODI HIGH SCHOOL',
				province: 'GAUTENG',
				district: 'TSHWANE SOUTH',
				quintile: 3,
				totalWrote: 80,
				achieved: 60,
				percentAchieved: 75
			}
		],
		totalCount: 41,
		pageNumber: 1,
		pageSize: 20,
		totalPages: 3,
		hasPreviousPage: false,
		hasNextPage: true,
		...overrides
	};
}

async function render(props: Record<string, unknown> = {}) {
	const target = document.createElement('div');
	document.body.append(target);
	component = mount(SubjectSchoolsTable, {
		target,
		props: { code: 'mathematics', subjectName: 'Mathematics', ...props }
	});
	await tick();
	await tick();
	return target;
}

function button(target: HTMLElement, label: string): HTMLButtonElement {
	const match = [...target.querySelectorAll('button')].find((b) =>
		b.textContent?.trim().startsWith(label)
	);
	if (!match) throw new Error(`No button "${label}"`);
	return match;
}

beforeEach(() => {
	fetchMock.mockReset();
	fetchMock.mockImplementation(async () => list());
});

afterEach(async () => {
	if (component) await unmount(component);
	component = undefined;
	document.body.replaceChildren();
	vi.useRealTimers();
});

describe('schools that wrote a subject', () => {
	it('loads the scope ordered by pass rate and links each school to its profile', async () => {
		const target = await render({ province: 'GAUTENG', district: null, year: 2024 });

		expect(fetchMock).toHaveBeenCalledExactlyOnceWith(
			'mathematics',
			expect.objectContaining({
				province: 'GAUTENG',
				district: null,
				year: 2024,
				pageNumber: 1,
				pageSize: 20,
				sort: 'percentAchieved',
				order: 'desc'
			}),
			undefined,
			expect.any(AbortSignal)
		);
		const link = target.querySelector('a');
		expect(link?.getAttribute('href')).toBe('/schools/700400139');
		expect(link?.textContent?.trim()).toBe('Mamelodi High School');
		expect(target.textContent).toContain('Tshwane South');
		expect(target.textContent).toContain('75.0%');
		expect(target.textContent).toContain('Schools that wrote Mathematics');
	});

	it('toggles server-side sorting and resets to the first page', async () => {
		const target = await render();
		button(target, 'Next').click();
		await tick();
		expect(fetchMock.mock.calls.at(-1)?.[1]).toMatchObject({ pageNumber: 2 });

		button(target, 'School').click();
		await tick();
		expect(fetchMock.mock.calls.at(-1)?.[1]).toMatchObject({
			pageNumber: 1,
			sort: 'name',
			order: 'asc'
		});

		button(target, 'School').click();
		await tick();
		expect(fetchMock.mock.calls.at(-1)?.[1]).toMatchObject({ sort: 'name', order: 'desc' });
		const header = button(target, 'School').closest('th');
		expect(header?.getAttribute('aria-sort')).toBe('descending');
	});

	it('debounces search into one request', async () => {
		vi.useFakeTimers();
		const target = await render();
		const input = target.querySelector('input')!;
		for (const value of ['m', 'ma', 'mam']) {
			input.value = value;
			input.dispatchEvent(new Event('input', { bubbles: true }));
			await tick();
		}
		expect(fetchMock).toHaveBeenCalledTimes(1);
		await vi.advanceTimersByTimeAsync(300);
		expect(fetchMock).toHaveBeenCalledTimes(2);
		expect(fetchMock.mock.calls[1]?.[1]).toMatchObject({ search: 'mam', pageNumber: 1 });
	});

	it('returns to the first page when the scope changes', async () => {
		const props = $state({
			code: 'mathematics',
			subjectName: 'Mathematics',
			province: null as string | null
		});
		const target = document.createElement('div');
		document.body.append(target);
		component = mount(SubjectSchoolsTable, { target, props });
		await tick();
		await tick();
		button(target, 'Next').click();
		await tick();
		expect(fetchMock.mock.calls.at(-1)?.[1]).toMatchObject({ pageNumber: 2 });

		props.province = 'LIMPOPO';
		flushSync();
		await tick();
		expect(fetchMock.mock.calls.at(-1)?.[1]).toMatchObject({
			province: 'LIMPOPO',
			pageNumber: 1
		});
	});

	it('shows an error with a working retry', async () => {
		fetchMock.mockRejectedValueOnce(new Error('Failed to fetch subject schools'));
		const target = await render();
		expect(target.querySelector('[role="alert"]')?.textContent).toContain(
			'Failed to fetch subject schools'
		);
		button(target, 'Try again').click();
		await tick();
		await tick();
		expect(fetchMock).toHaveBeenCalledTimes(2);
		expect(target.textContent).toContain('Mamelodi High School');
	});

	it('shows an empty state when no school wrote the subject', async () => {
		fetchMock.mockResolvedValue(list({ items: [], totalCount: 0, totalPages: 0 }));
		const target = await render();
		expect(target.textContent).toContain('No schools wrote Mathematics in this scope.');
	});
});
