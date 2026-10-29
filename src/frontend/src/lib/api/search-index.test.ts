import { describe, expect, it, vi } from 'vitest';
import { createApiClient } from './client';
import { seedSchoolSearchIndex } from './search-index';

describe('search index API transport', () => {
	it('posts the rebuild request and returns the indexed counts', async () => {
		const fetchFn = vi
			.fn<typeof fetch>()
			.mockResolvedValue(Response.json({ indexed: 8237, removed: 12, durationMs: 1450 }));
		const client = createApiClient(fetchFn, 'https://frontend.test');

		const { data, response } = await seedSchoolSearchIndex(client);

		expect(response.status).toBe(200);
		expect(data).toEqual({ indexed: 8237, removed: 12, durationMs: 1450 });
		expect(fetchFn).toHaveBeenCalledOnce();

		const [request] = fetchFn.mock.calls[0]!;
		expect(request).toBeInstanceOf(Request);
		expect((request as Request).url).toBe('https://frontend.test/api/v1/search-index/schools');
		expect((request as Request).method).toBe('POST');
	});

	it('surfaces a problem response without inventing counts', async () => {
		const fetchFn = vi.fn<typeof fetch>().mockResolvedValue(
			new Response(JSON.stringify({ status: 503, detail: 'Search unavailable' }), {
				status: 503,
				headers: { 'Content-Type': 'application/problem+json' }
			})
		);
		const client = createApiClient(fetchFn, 'https://frontend.test');

		const { data, error, response } = await seedSchoolSearchIndex(client);

		expect(response.status).toBe(503);
		expect(data).toBeUndefined();
		expect(error).toMatchObject({ status: 503 });
	});
});
