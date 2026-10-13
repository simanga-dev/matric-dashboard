import createClient from 'openapi-fetch';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { DashboardPaths } from './dashboard-contract';
import {
	fetchDashboardStats,
	fetchPassRateTrends,
	fetchSchoolById,
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

	it('returns null for a missing school, but rejects upstream failures', async () => {
		transport.mockResolvedValueOnce(Response.json({ status: 404 }, { status: 404 }));
		expect(await fetchSchoolById('1001')).toBeNull();
		expect(transport.mock.calls[0]?.[0]).toMatchObject({
			url: 'http://frontend.test/api/v1/dashboard/schools/1001'
		});
		transport.mockResolvedValueOnce(Response.json({ status: 503 }, { status: 503 }));
		await expect(fetchSchoolById('1001')).rejects.toThrow('Failed to fetch school');
	});

	it('rejects missing success bodies rather than casting undefined to a dashboard model', async () => {
		transport.mockResolvedValue(new Response(null, { status: 204 }));
		await expect(fetchSchools()).rejects.toThrow('Failed to fetch schools');
	});
});
