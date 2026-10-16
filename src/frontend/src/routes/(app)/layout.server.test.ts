import { describe, expect, it, vi } from 'vitest';
import { load } from './+layout.server';

type LoadEvent = Parameters<typeof load>[0];
function event(id: string, hadSession = false) {
	return {
		parent: async () => ({ user: null, backendError: null, hadSession }),
		route: { id },
		cookies: { get: vi.fn() }
	} as unknown as LoadEvent;
}

describe('public results access', () => {
	it.each(['/(app)/overview', '/(app)/subjects', '/(app)/schools/[id]'])(
		'allows anonymous visitors to %s',
		async (id) => {
			await expect(load(event(id))).resolves.toMatchObject({ user: null });
		}
	);

	it('keeps administrative pages protected', async () => {
		await expect(load(event('/(app)/admin/users'))).rejects.toMatchObject({
			status: 303,
			location: '/login'
		});
	});

	it('allows subject links even with an expired session', async () => {
		await expect(load(event('/(app)/subjects', true))).resolves.toMatchObject({ user: null });
	});
});
