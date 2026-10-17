import createClient from 'openapi-fetch';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { DashboardPaths } from './dashboard-contract';
import {
	fetchDashboardStats,
	fetchPassRateTrends,
	fetchRegionStats,
	fetchSchoolById,
	fetchSchoolSubjects,
	fetchSchools
} from './dashboard';

const { transport } = vi.hoisted(() => ({ transport: vi.fn<typeof fetch>() }));
vi.mock('./client', () => ({
	browserClient: createClient<DashboardPaths>({ baseUrl: 'http://frontend.test', fetch: transport })
}));

beforeEach(() => transport.mockReset());

describe('dashboard API requests', () => {
	it('uses the stats endpoint and returns the typed response', async () => {
		const body = {
			topSchools: { total: 4, trend: null },
			examCenters: { total: 5, trend: 1 },
			totalLearners: { total: 100, trend: 2 },
			passRate: { total: 90, trend: -1 }
		};
		transport.mockResolvedValue(Response.json(body));
		expect(await fetchDashboardStats()).toEqual(body);
		expect(transport.mock.calls[0]?.[0]).toMatchObject({
			url: 'http://frontend.test/api/v1/dashboard/stats'
		});
	});

	it('encodes trend years and omits an absent optional query', async () => {
		transport.mockImplementation(async () => Response.json([]));
		await fetchPassRateTrends('2023,2024');
		await fetchPassRateTrends();
		expect(transport.mock.calls[0]?.[0]).toMatchObject({
			url: 'http://frontend.test/api/v1/dashboard/pass-rate-trends?years=2023%2C2024'
		});
		expect(transport.mock.calls[1]?.[0]).toMatchObject({
			url: 'http://frontend.test/api/v1/dashboard/pass-rate-trends'
		});
	});

	it('preserves pagination and encodes school search text', async () => {
		const body = {
			items: [],
			totalCount: 0,
			pageNumber: 2,
			pageSize: 8,
			totalPages: 0,
			hasPreviousPage: true,
			hasNextPage: false
		};
		transport.mockResolvedValue(Response.json(body));
		expect(await fetchSchools(2, 8, 'School & District')).toEqual(body);
		expect(transport.mock.calls[0]?.[0]).toMatchObject({
			url: 'http://frontend.test/api/v1/dashboard/schools?pageNumber=2&pageSize=8&search=School%20%26%20District'
		});
	});

	it('forwards cancellation to the school search request', async () => {
		const controller = new AbortController();
		transport.mockResolvedValue(Response.json({ items: [] }));
		await fetchSchools(1, 100, 'School', undefined, controller.signal);
		const request = transport.mock.calls[0]?.[0] as Request;
		expect(request.signal.aborted).toBe(false);
		controller.abort();
		expect(request.signal.aborted).toBe(true);
	});

	it('encodes region scope filters and returns the yearly trend', async () => {
		const body = {
			scope: 'district',
			name: 'Fezile Dabi',
			province: 'FREE STATE',
			year: 2025,
			schoolCount: 62,
			totalWrote: 1000,
			totalPassed: 867,
			passRate: 86.7,
			years: [{ year: 2024, totalWrote: 900, totalPassed: 780, passRate: 86.7 }]
		};
		transport.mockResolvedValue(Response.json(body));
		expect(await fetchRegionStats('FREE STATE', 'Fezile Dabi')).toEqual(body);
		expect(transport.mock.calls[0]?.[0]).toMatchObject({
			url: 'http://frontend.test/api/v1/dashboard/regions/stats?province=FREE%20STATE&district=Fezile%20Dabi'
		});
	});

	it('returns school detail with the union of offered subjects and their years', async () => {
		const body = {
			id: '200500039',
			name: 'Example High School',
			province: 'GAUTENG',
			circuit: 'Ekurhuleni North',
			centreNumber: '200500039',
			quintile: 3,
			totalWrote: 120,
			totalPassed: 100,
			passRate: 83.3,
			totalAchieved: 40,
			years: [{ year: 2024, progressed: 2, totalWrote: 120, totalPassed: 100, passRate: 83.3 }],
			comparison: {
				year: 2024,
				provinceRank: 10,
				provinceSchoolCount: 200,
				provinceAveragePassRate: 80.1,
				nationalAveragePassRate: 78.5
			},
			subjectsOfferedYear: 2024,
			subjectsOffered: [
				{
					code: 'accounting',
					name: 'Accounting',
					year: 2024,
					totalWrote: 56,
					percentAchieved: 96.4
				},
				{ code: 'history', name: 'History', year: 2023, totalWrote: 20, percentAchieved: 85 }
			]
		};
		transport.mockResolvedValue(Response.json(body));
		const detail = await fetchSchoolById('200500039');
		expect(detail).toEqual(body);
		expect(detail?.subjectsOffered[1]?.year).toBe(2023);
		expect(transport.mock.calls[0]?.[0]).toMatchObject({
			url: 'http://frontend.test/api/v1/dashboard/schools/200500039'
		});
	});

	it('returns null for a missing school, but rejects upstream failures', async () => {
		transport.mockResolvedValueOnce(Response.json({ status: 404 }, { status: 404 }));
		expect(await fetchSchoolById('1001')).toBeNull();
		expect(transport.mock.calls[0]?.[0]).toMatchObject({
			url: 'http://frontend.test/api/v1/dashboard/schools/1001'
		});
		transport.mockResolvedValueOnce(Response.json({ status: 503 }, { status: 503 }));
		await expect(fetchSchoolById('1001')).rejects.toThrow('Failed to fetch school');
	});

	it('requests school subjects and encodes the optional year', async () => {
		const body = {
			schoolId: '200500039',
			year: 2024,
			availableYears: [2012, 2024],
			subjects: [
				{
					subjectId: '6f6a1a2e-0f1a-4c1a-9d2e-2f9d4f0a1b3c',
					code: 'accounting',
					name: 'Accounting',
					totalWrote: 56,
					achieved: 54,
					percentAchieved: 96.4
				}
			]
		};
		transport.mockResolvedValue(Response.json(body));
		expect(await fetchSchoolSubjects('200500039', 2024)).toEqual(body);
		expect(transport.mock.calls[0]?.[0]).toMatchObject({
			url: 'http://frontend.test/api/v1/dashboard/schools/200500039/subjects?year=2024'
		});
		transport.mockResolvedValueOnce(
			Response.json({ schoolId: '1001', year: null, availableYears: [], subjects: [] })
		);
		await fetchSchoolSubjects('1001');
		expect(transport.mock.calls[1]?.[0]).toMatchObject({
			url: 'http://frontend.test/api/v1/dashboard/schools/1001/subjects'
		});
	});

	it('rejects upstream failures for school subjects', async () => {
		transport.mockResolvedValue(Response.json({ status: 503 }, { status: 503 }));
		await expect(fetchSchoolSubjects('1001')).rejects.toThrow('Failed to fetch school subjects');
	});

	it('rejects missing success bodies rather than casting undefined to a dashboard model', async () => {
		transport.mockResolvedValue(new Response(null, { status: 204 }));
		await expect(fetchSchools()).rejects.toThrow('Failed to fetch schools');
	});
});
