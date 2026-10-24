// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { mount, tick, unmount } from 'svelte';
import SubjectPage from './+page.svelte';
import { load } from './+page';
import { fetchSubjectDetail, fetchSubjectSchools } from '$lib/api/dashboard';
import { goto, invalidate } from '$app/navigation';
import type { SubjectDetail } from '$lib/types/dashboard';

vi.mock('$app/state', () => ({
	page: { url: new URL('http://localhost/subjects/mathematics') },
	navigating: { to: null }
}));
vi.mock('$app/navigation', () => ({ goto: vi.fn(), invalidate: vi.fn() }));
vi.mock('$app/paths', () => ({ resolve: (path: string) => path }));
// The tooltip provider lives in the root layout, which is not mounted here.
vi.mock('$lib/components/common/InfoTooltip.svelte', () => ({ default: () => {} }));
vi.mock('$lib/api/client', () => ({ createApiClient: vi.fn(() => ({})) }));
vi.mock('$lib/api/dashboard', () => ({
	fetchSubjectDetail: vi.fn(),
	fetchSubjectSchools: vi.fn()
}));

function detail(overrides: Partial<SubjectDetail> = {}): SubjectDetail {
	return {
		code: 'mathematics',
		name: 'Mathematics',
		isGateway: true,
		scope: 'country',
		scopeName: 'South Africa',
		province: null,
		district: null,
		year: 2024,
		availableYears: [2023, 2024],
		schoolCount: 5000,
		totalWrote: 200000,
		achieved: 130000,
		percentAchieved: 65,
		trend: [
			{
				year: 2023,
				schoolCount: 4800,
				totalWrote: 160000,
				achieved: 96000,
				percentAchieved: 60
			},
			{
				year: 2024,
				schoolCount: 5000,
				totalWrote: 200000,
				achieved: 130000,
				percentAchieved: 65
			}
		],
		provinces: [
			{ name: 'GAUTENG', schoolCount: 900, totalWrote: 40000, achieved: 30000, percentAchieved: 75 }
		],
		districts: [],
		...overrides
	};
}

type Data = {
	code: string;
	scope: { province: string | null; district: string | null; year: number | null };
	detail: SubjectDetail | null;
	loadError: string | null;
};

let component: ReturnType<typeof mount> | undefined;

async function render(data: Partial<Data> = {}) {
	const target = document.createElement('div');
	document.body.append(target);
	component = mount(SubjectPage, {
		target,
		props: {
			data: {
				code: 'mathematics',
				scope: { province: null, district: null, year: null },
				detail: detail(),
				loadError: null,
				user: null,
				backendError: null,
				hadSession: false,
				locale: 'en',
				apiUrl: undefined,
				sidebarOpen: true,
				...data
			}
		}
	});
	await tick();
	await tick();
	return target;
}

beforeEach(() => {
	vi.clearAllMocks();
	vi.stubGlobal(
		'ResizeObserver',
		class {
			observe() {}
			unobserve() {}
			disconnect() {}
		}
	);
	vi.mocked(fetchSubjectSchools).mockResolvedValue({
		year: 2024,
		items: [],
		totalCount: 0,
		pageNumber: 1,
		pageSize: 20,
		totalPages: 0,
		hasPreviousPage: false,
		hasNextPage: false
	});
});

afterEach(async () => {
	if (component) await unmount(component);
	component = undefined;
	document.body.replaceChildren();
	vi.unstubAllGlobals();
});

describe('subject detail page', () => {
	it('shows the subject header, KPIs and a breadcrumb back to the scoped list', async () => {
		const target = await render({ scope: { province: null, district: null, year: 2024 } });

		expect(target.querySelector('h1')?.textContent).toBe('Mathematics');
		expect(target.textContent).toContain('Gateway subject');
		expect(target.textContent).toContain('200,000');
		expect(target.textContent).toContain('130,000');
		expect(target.textContent).toContain('65.0%');
		expect(target.textContent).toContain('+5 pts');
		expect(target.textContent).toContain('+25%');
		const crumb = [...target.querySelectorAll('a')].find((a) => a.textContent === 'Subjects');
		expect(crumb?.getAttribute('href')).toBe('/subjects?year=2024');
		expect(fetchSubjectSchools).toHaveBeenCalledWith(
			'mathematics',
			expect.objectContaining({ province: null, year: 2024 }),
			undefined,
			expect.any(AbortSignal)
		);
	});

	it('drills from a province row into that province, keeping the year', async () => {
		const target = await render({ scope: { province: null, district: null, year: 2024 } });
		target.querySelector<HTMLElement>('[data-region-row="GAUTENG"]')?.click();

		expect(goto).toHaveBeenCalledWith('/subjects/mathematics?province=GAUTENG&year=2024', {
			noScroll: true,
			keepFocus: true
		});
	});

	it('switches the year through the URL', async () => {
		const target = await render();
		[...target.querySelectorAll('button')].find((b) => b.textContent?.trim() === '2023')?.click();

		expect(goto).toHaveBeenCalledWith('/subjects/mathematics?year=2023', expect.any(Object));
	});

	it('shows district rows once a province is selected', async () => {
		const target = await render({
			scope: { province: 'GAUTENG', district: null, year: null },
			detail: detail({
				scope: 'province',
				scopeName: 'GAUTENG',
				province: 'GAUTENG',
				districts: [
					{
						name: 'TSHWANE SOUTH',
						schoolCount: 90,
						totalWrote: 4000,
						achieved: 3000,
						percentAchieved: 75
					}
				]
			})
		});
		expect(target.textContent).toContain('By district');
		target.querySelector<HTMLElement>('[data-region-row="TSHWANE SOUTH"]')?.click();

		expect(goto).toHaveBeenCalledWith(
			'/subjects/mathematics?province=GAUTENG&district=TSHWANE+SOUTH',
			expect.any(Object)
		);
	});

	it('shows an empty state when the scope has no results for the subject', async () => {
		const target = await render({
			detail: detail({ year: null, totalWrote: 0, availableYears: [], trend: [], provinces: [] })
		});
		expect(target.textContent).toContain('No Mathematics results for South Africa.');
		expect(fetchSubjectSchools).not.toHaveBeenCalled();
	});

	it('offers a retry when the load failed', async () => {
		const target = await render({ detail: null, loadError: 'Failed to load subject results' });
		expect(target.querySelector('[role="alert"]')?.textContent).toContain(
			'Failed to load subject results'
		);
		[...target.querySelectorAll('button')]
			.find((b) => b.textContent?.includes('Try again'))
			?.click();
		expect(invalidate).toHaveBeenCalledWith('app:subject-detail');
	});
});

describe('subject detail load', () => {
	type LoadEvent = Parameters<typeof load>[0];
	function event(path: string) {
		const url = new URL(path, 'http://localhost');
		return {
			fetch: vi.fn(),
			url,
			params: { code: url.pathname.split('/').at(-1) ?? '' },
			depends: vi.fn()
		} as unknown as LoadEvent;
	}

	it('loads the subject for the scope in the query string', async () => {
		vi.mocked(fetchSubjectDetail).mockResolvedValue(detail());
		const result = await load(event('/subjects/mathematics?province=GAUTENG&year=2024'));

		expect(fetchSubjectDetail).toHaveBeenCalledWith(
			'mathematics',
			{ province: 'GAUTENG', district: null, year: 2024 },
			expect.anything()
		);
		expect(result).toMatchObject({ code: 'mathematics', loadError: null });
	});

	it('responds 404 for an unknown subject', async () => {
		vi.mocked(fetchSubjectDetail).mockResolvedValue(null);
		await expect(load(event('/subjects/unknown'))).rejects.toMatchObject({ status: 404 });
	});

	it('returns an in-page error instead of failing on upstream errors', async () => {
		vi.mocked(fetchSubjectDetail).mockRejectedValue(new Error('boom'));
		await expect(load(event('/subjects/mathematics'))).resolves.toMatchObject({
			detail: null,
			loadError: 'Failed to load subject results'
		});
	});
});
