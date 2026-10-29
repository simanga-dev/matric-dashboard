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
	fetchMock.mockImplementation(async (_code, query) => {
		const pageNumber = query?.pageNumber ?? 1;
		const base = list();
		return list({
			pageNumber,
			items:
				pageNumber === 1
					? base.items
					: base.items.map((school) => ({ ...school, id: `${school.id}-${pageNumber}` }))
		});
	});
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
				pageSize: 10,
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
		button(target, 'Show more').click();
		flushSync();
		await tick();
		expect(fetchMock.mock.calls.at(-1)?.[1]).toMatchObject({ pageNumber: 2 });

		button(target, 'School').click();
		flushSync();
		await tick();
		expect(fetchMock.mock.calls.at(-1)?.[1]).toMatchObject({
			pageNumber: 1,
			sort: 'name',
			order: 'asc'
		});

		button(target, 'School').click();
		flushSync();
		await tick();
		expect(fetchMock.mock.calls.at(-1)?.[1]).toMatchObject({ sort: 'name', order: 'desc' });
		const header = button(target, 'School').closest('th');
		expect(header?.getAttribute('aria-sort')).toBe('descending');
	});

	it('appends the next page when Show more is pressed', async () => {
		fetchMock.mockImplementation(async (_code, query) => {
			const pageNumber = query?.pageNumber ?? 1;
			return list({
				pageNumber,
				items: list().items.map((school) => ({
					...school,
					id: `${school.id}-${pageNumber}`,
					name: `PAGE ${pageNumber} SCHOOL`
				})),
				hasNextPage: pageNumber < 2
			});
		});
		const target = await render();

		expect(target.querySelectorAll('tbody tr')).toHaveLength(1);
		button(target, 'Show more').click();
		flushSync();
		await tick();

		expect(target.querySelectorAll('tbody tr')).toHaveLength(2);
		expect(target.textContent).toContain('Page 1 School');
		expect(target.textContent).toContain('Page 2 School');
		expect(target.textContent).not.toContain('Show more');
		expect(target.textContent).toContain('Showing 2 of 41 schools');
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
		button(target, 'Show more').click();
		flushSync();
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
