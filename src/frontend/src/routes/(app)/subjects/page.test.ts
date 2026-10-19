// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { mount, tick, unmount } from 'svelte';
import SubjectsPage from './+page.svelte';
import { fetchRegionSubjects, fetchSchoolById, fetchSchoolSubjects } from '$lib/api/dashboard';
import type { RegionSubjects, SchoolSubjects } from '$lib/types/dashboard';

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
		target,
		props: {
			data: {
				schools: [],
				user: null,
				backendError: null,
				hadSession: false,
				locale: 'en',
				apiUrl: undefined,
				sidebarOpen: true
			}
		}
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
