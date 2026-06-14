/**
 * Tests for the (app) layout server guard - the auth boundary for protected routes.
 *
 * These cover the server-side load function only. The client-side behavior
 * (toast + replaceState in login/+page.svelte) is inside an `onMount` callback
 * whose logic is a trivial conditional - verifying it would test Svelte's
 * lifecycle rather than application logic. If component testing infrastructure
 * (e.g. @testing-library/svelte) is added later, a smoke test for the toast
 * would be a reasonable addition.
 *
 * Note: hadSession is determined by the root layout (reads the cookie before
 * getUser can mutate it) and passed down via parent(). These tests verify
 * that the (app) layout correctly uses it for redirect decisions.
 */
import { describe, expect, it, vi } from 'vitest';
import { isHttpError, isRedirect } from '@sveltejs/kit';
import { load } from './+layout.server';
import { MOCK_USER, createMockLoadEvent, createMockCookies } from '../../test-utils';

type LoadEvent = Parameters<typeof load>[0];

/** Builds a complete mock SvelteKit load event for the (app) layout. */
function mockLoadEvent(
	overrides: {
		user?: typeof MOCK_USER | null;
		backendError?: 'backend_unavailable' | null;
		hadSession?: boolean;
		routeId?: string;
	} = {}
) {
	const {
		user = null,
		backendError = null,
		hadSession = false,
		routeId = '/(app)/settings'
	} = overrides;

	return createMockLoadEvent({
		route: { id: routeId },
		parent: vi.fn().mockResolvedValue({ user, backendError, hadSession })
	}) as LoadEvent;
}

/** Asserts that a load function throws a SvelteKit redirect. */
async function expectRedirect(fn: () => ReturnType<typeof load>, status: number, location: string) {
	try {
		await fn();
		expect.fail('Expected redirect to be thrown');
	} catch (e) {
		expect(isRedirect(e)).toBe(true);
		if (isRedirect(e)) {
			expect(e.status).toBe(status);
			expect(e.location).toBe(location);
		}
	}
}

describe('(app) layout server load', () => {
	// ── Authenticated ───────────────────────────────────────────────

	it('authenticated user - returns user data with sidebar state', async () => {
		const result = await load(mockLoadEvent({ user: MOCK_USER }));
		expect(result).toEqual({ user: MOCK_USER, sidebarOpen: true });
	});

	// ── Backend unavailable ─────────────────────────────────────────

	it('backend unavailable - throws 503 error', async () => {
		try {
			await load(mockLoadEvent({ backendError: 'backend_unavailable' }));
			expect.fail('Expected error to be thrown');
		} catch (e) {
			expect(isHttpError(e, 503)).toBe(true);
		}
	});

	it('backend unavailable takes precedence over missing user', async () => {
		try {
			await load(mockLoadEvent({ backendError: 'backend_unavailable', hadSession: true }));
			expect.fail('Expected error to be thrown');
		} catch (e) {
			// Should throw 503, not redirect - backend error is checked first
			expect(isHttpError(e, 503)).toBe(true);
			expect(isRedirect(e)).toBe(false);
		}
	});

	// ── Session expired detection ───────────────────────────────────

	it('no user, no prior session on protected route - redirects to /login', async () => {
		await expectRedirect(() => load(mockLoadEvent()), 303, '/login');
	});

	it('no user, had session on protected route - redirects with session_expired reason', async () => {
		await expectRedirect(
			() => load(mockLoadEvent({ hadSession: true })),
			303,
			'/login?reason=session_expired'
		);
	});

	// ── Public routes ───────────────────────────────────────────────

	it('no user on root route - returns null user with sidebar state', async () => {
		const result = await load(mockLoadEvent({ routeId: '/(app)' }));
		expect(result).toEqual({ user: null, sidebarOpen: true });
	});

	it('no user on dashboard - returns null user with sidebar state', async () => {
		const result = await load(mockLoadEvent({ routeId: '/(app)/dashboard' }));
		expect(result).toEqual({ user: null, sidebarOpen: true });
	});

	it('no user on dashboard with collapsed sidebar - returns sidebarOpen false', async () => {
		const event = createMockLoadEvent({
			route: { id: '/(app)/dashboard' },
			parent: vi.fn().mockResolvedValue({ user: null, backendError: null, hadSession: false }),
			cookies: createMockCookies(() => 'false')
		}) as LoadEvent;
		const result = await load(event);
		expect(result).toEqual({ user: null, sidebarOpen: false });
	});
});
