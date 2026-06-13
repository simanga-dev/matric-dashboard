import { error, redirect } from '@sveltejs/kit';
import { routes } from '$lib/config';
import * as m from '$lib/paraglide/messages';
import type { LayoutServerLoad } from './$types';

export const load: LayoutServerLoad = async ({ parent, route, cookies }) => {
	const { user, backendError, hadSession } = await parent();

	if (backendError === 'backend_unavailable') {
		throw error(503, m.serverError_backendUnavailable());
	}

	const isPublicDashboard = route.id === '/(app)/dashboard';

	if (!user && !isPublicDashboard) {
		const target = hadSession ? `${routes.login}?reason=session_expired` : routes.login;
		throw redirect(303, target);
	}

	const sidebarOpen = cookies.get('sidebar:state') !== 'false';

	return { user, sidebarOpen };
};
