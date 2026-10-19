import type { LayoutServerLoad } from './$types';
import { dev } from '$app/environment';
import { env } from '$env/dynamic/public';
import { SERVER_CONFIG } from '$lib/config/server';
import { getUser, REFRESH_TOKEN_COOKIE } from '$lib/auth';

export const load: LayoutServerLoad = async ({ locals, fetch, url, cookies }) => {
	// Read BEFORE getUser - the failed-refresh response includes a Set-Cookie
	// that clears the token, and SvelteKit's internal fetch mutates the shared
	// cookie store. After getUser returns, cookies.get() would return undefined.
	const hadSession = Boolean(cookies.get(REFRESH_TOKEN_COOKIE));

	const { user, error: backendError } = await getUser(fetch, url.origin);
	// Keep these optional for consumers that supply partial layout fixtures.
	const imagery: { mapImageryUrl?: string; mapImageryAttribution?: string } = {
		mapImageryUrl: env.PUBLIC_MAP_IMAGERY_URL ?? '',
		mapImageryAttribution: env.PUBLIC_MAP_IMAGERY_ATTRIBUTION ?? ''
	};
	return {
		user,
		backendError,
		hadSession,
		locale: locals.locale,
		...imagery,
		apiUrl: dev ? SERVER_CONFIG.API_URL : undefined
	};
};
