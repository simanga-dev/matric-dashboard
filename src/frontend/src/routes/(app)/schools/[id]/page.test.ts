// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { mount, tick, unmount } from 'svelte';
import SchoolDetailPage from './+page.svelte';
import type { SchoolDetail } from '$lib/types/dashboard';
import type { PageData } from './$types';

vi.mock('$lib/components/dashboard/SchoolLocationMap.svelte', () => ({ default: () => {} }));
vi.mock('$lib/components/dashboard/SmoothTrendChart.svelte', () => ({ default: () => {} }));
vi.mock('$lib/components/common/InfoTooltip.svelte', () => ({ default: () => {} }));
vi.mock('$lib/components/ui/sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
vi.mock('$app/paths', () => ({ resolve: (path: string) => path }));
vi.mock('$app/navigation', () => ({ goto: vi.fn() }));
vi.mock('$app/state', () => ({ page: { data: {} } }));

function school(overrides: Partial<SchoolDetail> = {}): SchoolDetail {
	return {
		id: '500101343',
		name: 'AMADADA HIGH SCHOOL',
		province: 'GAUTENG',
		circuit: 'Johannesburg East',
		centreNumber: 'C1343',
		quintile: 3,
		quintileCode: 'Q3',
		totalWrote: 210,
		totalPassed: 172,
		passRate: 81.9,
		years: [{ year: 2024, progressed: 3, totalWrote: 210, totalPassed: 172, passRate: 81.9 }],
		comparison: {
			year: 2024,
			provinceRank: 12,
			provinceSchoolCount: 400,
			provinceAveragePassRate: 82.4,
			nationalAveragePassRate: 87.3
		},
		subjectsOfferedYear: 2024,
		subjectsOffered: [],
		email: 'admin@profile.co.za',
		telephone: '+27111234567',
		sector: 'Public',
		phase: 'Secondary',
		landOwnership: 'Government',
		buildingOwnership: 'Government',
		isSection21: false,
		isNoFeeSchool: true,
		urbanRural: 'Urban',
		enrolment: { year: 2025, learners: 1200, educators: 40 },
		...overrides
	};
}

let component: ReturnType<typeof mount> | undefined;

async function render(detail: SchoolDetail) {
	const target = document.createElement('div');
	document.body.append(target);
	const data = { school: detail, spatial: null } as unknown as PageData;
	component = mount(SchoolDetailPage, { target, props: { data } });
	await tick();
	await tick();
	return target;
}

beforeEach(() => {
	vi.stubGlobal(
		'ResizeObserver',
		class {
			observe() {}
			unobserve() {}
			disconnect() {}
		}
	);
});

afterEach(async () => {
	if (component) await unmount(component);
	component = undefined;
	document.body.replaceChildren();
});

describe('school detail page profile', () => {
	it('shows contact details, enrolment and classification', async () => {
		const target = await render(school());
		const text = target.textContent ?? '';

		expect(text).toContain('admin@profile.co.za');
		expect(text).toContain('+27 11 123 4567');
		expect(text).toContain('1,200');
		expect(text).toContain('40');
		expect(text).toContain('30.0');
		expect(text).toContain('Secondary school');
		expect(text).toContain('Government');
		expect(text).toContain('No-fee school');
	});

	it('links email and telephone using actionable schemes', async () => {
		const target = await render(school());

		expect(target.querySelector('a[href="mailto:admin@profile.co.za"]')).not.toBeNull();
		expect(target.querySelector('a[href="tel:+27111234567"]')).not.toBeNull();
	});

	it('falls back to n/a when a school has no masterlist profile', async () => {
		const target = await render(
			school({
				quintile: null,
				quintileCode: 'N/A',
				email: null,
				telephone: null,
				sector: null,
				phase: null,
				landOwnership: null,
				buildingOwnership: null,
				isSection21: null,
				isNoFeeSchool: null,
				urbanRural: null,
				enrolment: null
			})
		);
		const text = target.textContent ?? '';

		expect(text).toContain('No numeric quintile is published for this school');
		expect(text).toContain('Quintile n/a');
		expect(text).not.toContain('Learners per educator');
		expect(target.querySelector('a[href^="mailto:"]')).toBeNull();
		expect(target.querySelector('a[href^="tel:"]')).toBeNull();
	});
});
