// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { mount, tick, unmount } from 'svelte';
import SchoolsPage from './+page.svelte';
import { fetchRegionStats, fetchSchools } from '$lib/api/dashboard';
import { loadSchoolDirectory } from '$lib/components/dashboard/school-directory';
import type { RegionStats, School } from '$lib/types/dashboard';

const router = vi.hoisted(() => ({
	page: { url: new URL('http://localhost/schools'), state: {} },
	afterNavigate: null as (() => void) | null
}));

vi.mock('$app/state', () => ({ page: router.page }));
vi.mock('$app/navigation', () => ({
	goto: vi.fn(),
	afterNavigate: (callback: () => void) => {
		router.afterNavigate = callback;
	},
	replaceState: (url: string) => {
		router.page.url = new URL(url, router.page.url);
	}
}));
vi.mock('$app/paths', () => ({ resolve: (path: string) => path }));
vi.mock('$lib/api/dashboard', () => ({ fetchSchools: vi.fn(), fetchRegionStats: vi.fn() }));
vi.mock('$lib/components/dashboard/school-directory', () => ({ loadSchoolDirectory: vi.fn() }));

function stats(province: string | null): RegionStats {
	return {
		scope: province ? 'province' : 'country',
		name: province ?? 'South Africa',
		province,
		year: 2024,
		schoolCount: province ? 2100 : 6800,
		totalWrote: province ? 110000 : 700000,
		totalPassed: 0,
		passRate: province ? 88.4 : 87.3,
		years: []
	};
}

let component: ReturnType<typeof mount> | undefined;

async function render(path: string) {
	router.page.url = new URL(path, 'http://localhost');
	const target = document.createElement('div');
	document.body.append(target);
	component = mount(SchoolsPage, { target });
	await tick();
	router.afterNavigate?.();
	await tick();
	await tick();
	return target;
}

beforeEach(() => {
	vi.clearAllMocks();
	router.afterNavigate = null;
	vi.mocked(fetchSchools).mockResolvedValue({
		items: [],
		totalCount: 0,
		pageNumber: 1,
		pageSize: 25,
		totalPages: 2,
		hasPreviousPage: false,
		hasNextPage: true
	});
	vi.mocked(fetchRegionStats).mockImplementation(async (province) => stats(province ?? null));
	vi.mocked(loadSchoolDirectory).mockResolvedValue([
		{ province: 'LIMPOPO' } as School,
		{ province: 'GAUTENG' } as School,
		{ province: 'GAUTENG' } as School
	]);
});

afterEach(async () => {
	if (component) await unmount(component);
	component = undefined;
	document.body.replaceChildren();
});

describe('school directory page', () => {
	it('keeps the legacy search deep link working', async () => {
		await render('/schools?search=pretoria');

		expect(fetchSchools).toHaveBeenCalledTimes(1);
		expect(vi.mocked(fetchSchools).mock.calls[0]?.[0]).toMatchObject({ search: 'pretoria' });
		expect(router.page.url.search).toBe('?search=pretoria');
	});

	it('restores province, sort and page from the URL and summarises the province', async () => {
		const target = await render('/schools?province=GAUTENG&sort=passRate&order=desc&page=2');

		expect(vi.mocked(fetchSchools).mock.calls[0]?.[0]).toMatchObject({
			province: 'GAUTENG',
			sort: 'passRate',
			order: 'desc',
			pageNumber: 2
		});
		expect(fetchRegionStats).toHaveBeenLastCalledWith('GAUTENG');
		expect(target.textContent).toContain('2,100');
		expect(target.textContent).toContain('110,000');
		expect(target.textContent).toContain('88.4%');
		expect(target.querySelector('[aria-label="Province"]')?.textContent).toContain('Gauteng');
	});

	it('writes table changes back to the URL so the view is shareable', async () => {
		const target = await render('/schools?province=LIMPOPO');
		const next = [...target.querySelectorAll('button')].find((button) =>
			button.textContent?.includes('Next')
		);
		next?.click();
		await tick();

		expect(router.page.url.search).toBe('?province=LIMPOPO&page=2');
	});

	it('shows the national summary without a province', async () => {
		const target = await render('/schools');

		expect(fetchRegionStats).toHaveBeenCalledWith(null);
		expect(target.textContent).toContain('6,800');
		expect(target.textContent).toContain('South Africa');
	});
});
