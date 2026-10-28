// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { mount, tick, unmount } from 'svelte';
import SubjectsPage from './+page.svelte';
import { fetchRegionSubjects, fetchSchoolById, fetchSchoolSubjects } from '$lib/api/dashboard';
import type { RegionSubject, RegionSubjects, SchoolSubjects } from '$lib/types/dashboard';

const router = vi.hoisted(() => ({
	page: { url: new URL('http://localhost/subjects'), state: {} },
	afterNavigate: null as (() => void) | null
}));

vi.mock('$app/state', () => ({ page: router.page }));
vi.mock('$app/navigation', () => ({
	afterNavigate: (callback: () => void) => {
		router.afterNavigate = callback;
	},
	replaceState: (url: string) => {
		router.page.url = new URL(url, router.page.url);
	}
}));
vi.mock('$app/paths', () => ({ resolve: (path: string) => path }));
// The tooltip provider lives in the root layout, which is not mounted here.
vi.mock('$lib/components/common/InfoTooltip.svelte', () => ({ default: () => {} }));
vi.mock('$lib/api/dashboard', () => ({
	fetchRegionSubjects: vi.fn(),
	fetchSchoolSubjects: vi.fn(),
	fetchSchoolById: vi.fn(),
	fetchSchools: vi.fn()
}));

function schoolResults(id: string, year = 2025): SchoolSubjects {
	return { schoolId: id, year, availableYears: [2024, 2025], subjects: [] };
}

function regionResults(
	province?: string | null,
	district?: string | null,
	year = 2025
): RegionSubjects {
	return {
		scope: district ? 'district' : province ? 'province' : 'country',
		name: district ?? province ?? 'South Africa',
		province: province ?? null,
		year,
		availableYears: [2024, 2025],
		schoolCount: 0,
		subjects: []
	};
}

let component: ReturnType<typeof mount> | undefined;

async function navigate(path: string) {
	router.page.url = new URL(path, 'http://localhost');
	router.afterNavigate?.();
	await tick();
	await tick();
}

async function render(path: string) {
	router.page.url = new URL(path, 'http://localhost');
	const target = document.createElement('div');
	document.body.append(target);
	component = mount(SubjectsPage, {
		target
	});
	await tick();
	await navigate(path);
}

beforeEach(() => {
	vi.clearAllMocks();
	router.afterNavigate = null;
	vi.mocked(fetchRegionSubjects).mockImplementation(async (province, district, year) =>
		regionResults(province, district, year ?? 2025)
	);
	vi.mocked(fetchSchoolSubjects).mockImplementation(async (id, year) => schoolResults(id, year));
	vi.mocked(fetchSchoolById).mockImplementation(async (id) => ({
		id,
		name: `School ${id}`,
		province: 'GAUTENG',
		circuit: 'Central',
		centreNumber: id,
		quintile: 3,
		totalWrote: 0,
		totalPassed: 0,
		passRate: 0,
		years: [],
		comparison: {
			provinceRank: 0,
			provinceSchoolCount: 0,
			provinceAveragePassRate: null,
			nationalAveragePassRate: null
		},
		subjectsOfferedYear: null,
		subjectsOffered: []
	}));
});

afterEach(async () => {
	if (component) await unmount(component);
	component = undefined;
	document.body.replaceChildren();
});

describe('subject scope navigation', () => {
	it('loads an initial school link with its year and ignores conflicting region filters', async () => {
		await render('/subjects?school=100&province=GAUTENG&district=Central&year=2024');

		expect(fetchSchoolSubjects).toHaveBeenCalledExactlyOnceWith('100', 2024);
		expect(fetchRegionSubjects).not.toHaveBeenCalled();
		expect(document.body.textContent).toContain('School 100');
		expect(router.page.url.search).toBe('?school=100&year=2024');
	});

	it('returns to national results when the sidebar navigates to /subjects', async () => {
		await render('/subjects?school=100&year=2024');
		await navigate('/subjects');

		expect(fetchRegionSubjects).toHaveBeenCalledExactlyOnceWith(null, null, null);
		expect(fetchSchoolSubjects).toHaveBeenCalledTimes(1);
		expect(document.body.textContent).toContain('South Africa');
		expect(document.body.textContent).not.toContain('School 100');
		expect(router.page.url.searchParams.has('school')).toBe(false);
	});

	it('adopts province, district and year changes without remounting', async () => {
		await render('/subjects');
		await navigate('/subjects?province=GAUTENG&district=Central&year=2024');

		expect(fetchRegionSubjects).toHaveBeenLastCalledWith('GAUTENG', 'Central', 2024);
		expect(document.body.textContent).toContain('Central');
		await navigate('/subjects?province=LIMPOPO&year=2025');

		expect(fetchRegionSubjects).toHaveBeenLastCalledWith('LIMPOPO', null, 2025);
		expect(document.body.textContent).toContain('Limpopo');
		expect(router.page.url.search).toBe('?province=LIMPOPO&year=2025');
		expect(fetchRegionSubjects).toHaveBeenCalledTimes(3);
	});

	it('ignores a stale school response after navigation to a different school', async () => {
		let finish!: (result: SchoolSubjects) => void;
		vi.mocked(fetchSchoolSubjects).mockImplementationOnce(
			() => new Promise((resolve) => (finish = resolve))
		);
		await render('/subjects?school=100');
		await navigate('/subjects?school=200&year=2024');
		finish(schoolResults('100', 2025));
		await tick();
		await tick();

		expect(document.body.textContent).toContain('School 200');
		expect(router.page.url.search).toBe('?school=200&year=2024');
		expect(fetchSchoolById).toHaveBeenCalledExactlyOnceWith('200');
	});

	it('keeps year controls shareable without reloading for its own URL writes', async () => {
		await render('/subjects?school=100');
		const yearButton = [...document.querySelectorAll('button')].find(
			(button) => button.textContent?.trim() === '2024'
		);
		expect(yearButton).toBeDefined();
		yearButton?.click();
		await tick();
		await tick();

		expect(fetchSchoolSubjects).toHaveBeenLastCalledWith('100', 2024);
		expect(fetchSchoolSubjects).toHaveBeenCalledTimes(2);
		expect(router.page.url.search).toBe('?school=100&year=2024');
	});
});

const subjects: RegionSubject[] = [
	{
		code: 'mathematics',
		name: 'Mathematics',
		totalWrote: 300,
		achieved: 150,
		percentAchieved: 50,
		schoolCount: 30
	},
	{
		code: 'accounting',
		name: 'Accounting',
		totalWrote: 100,
		achieved: 90,
		percentAchieved: 90,
		schoolCount: 20
	},
	{
		code: 'history',
		name: 'History',
		totalWrote: 100,
		achieved: null,
		percentAchieved: null,
		schoolCount: 10
	}
];

function tableNames(): string[] {
	return [...document.querySelectorAll('tbody tr td:first-child')].map(
		(cell) => cell.textContent?.trim() ?? ''
	);
}

function headerButton(label: string): HTMLButtonElement {
	const match = [...document.querySelectorAll('th button')].find(
		(button) => button.textContent?.trim() === label
	);
	if (!(match instanceof HTMLButtonElement)) throw new Error(`No header "${label}"`);
	return match;
}

describe('subject list', () => {
	beforeEach(() => {
		vi.mocked(fetchRegionSubjects).mockImplementation(async (province, district, year) => ({
			...regionResults(province, district, year ?? 2025),
			schoolCount: 42,
			subjects
		}));
	});

	it('links every subject to its detail page with the current scope', async () => {
		await render('/subjects?province=GAUTENG&district=Central&year=2024');

		const tableLink = [...document.querySelectorAll('tbody a')].find(
			(link) => link.textContent?.trim() === 'Accounting'
		);
		expect(tableLink?.getAttribute('href')).toBe(
			'/subjects/accounting?province=GAUTENG&district=Central&year=2024'
		);
		const barLinks = [...document.querySelectorAll('[data-subject-bar] a')].map((link) =>
			link.getAttribute('href')
		);
		expect(barLinks).toContain('/subjects/mathematics?province=GAUTENG&district=Central&year=2024');
	});

	it('links school-scoped subjects to the unscoped subject page', async () => {
		vi.mocked(fetchSchoolSubjects).mockResolvedValue({
			schoolId: '100',
			year: 2024,
			availableYears: [2024],
			subjects: [
				{
					subjectId: 'a1',
					code: 'accounting',
					name: 'Accounting',
					totalWrote: 10,
					achieved: 9,
					percentAchieved: 90
				}
			]
		});
		await render('/subjects?school=100&year=2024');

		const link = document.querySelector('tbody a');
		expect(link?.getAttribute('href')).toBe('/subjects/accounting');
	});

	it('summarises the scope with a learner-weighted pass rate', async () => {
		await render('/subjects');

		const summary = document.querySelector('[aria-label="Scope summary"]');
		expect(summary?.textContent).toContain('3');
		expect(summary?.textContent).toContain('500');
		expect(summary?.textContent).toContain('42');
		// (150 + 90) / (300 + 100): History has no achievement count and is excluded.
		expect(summary?.textContent).toContain('60.0%');
	});

	it('sorts the table by column with unavailable values last', async () => {
		await render('/subjects');
		expect(tableNames()).toEqual(['Accounting', 'History', 'Mathematics']);

		headerButton('Pass rate').click();
		await tick();
		expect(tableNames()).toEqual(['Accounting', 'Mathematics', 'History']);
		expect(headerButton('Pass rate').closest('th')?.getAttribute('aria-sort')).toBe('descending');

		headerButton('Pass rate').click();
		await tick();
		expect(tableNames()).toEqual(['Mathematics', 'Accounting', 'History']);

		headerButton('Wrote').click();
		await tick();
		expect(tableNames()).toEqual(['Mathematics', 'Accounting', 'History']);
	});
});
