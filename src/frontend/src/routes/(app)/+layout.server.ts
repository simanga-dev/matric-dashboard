import { error, redirect } from '@sveltejs/kit';
import { routes } from '$lib/config';
import * as m from '$lib/paraglide/messages';
import type { LayoutServerLoad } from './$types';

export const load: LayoutServerLoad = async ({ parent, cookies }) => {
	const { user, backendError, hadSession } = await parent();

	if (backendError === 'backend_unavailable') {
		throw error(503, m.serverError_backendUnavailable());
	}

	if (!user) {
		const target = hadSession ? `${routes.login}?reason=session_expired` : routes.login;
		throw redirect(303, target);
	}

	const sidebarOpen = cookies.get('sidebar:state') !== 'false';

	return { user, sidebarOpen };
};
