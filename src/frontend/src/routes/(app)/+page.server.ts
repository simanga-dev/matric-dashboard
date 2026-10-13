import { redirect } from '@sveltejs/kit';
import { routes } from '$lib/config';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async () => {
	// 302 (not 301): the landing destination can change, and a permanent
	// redirect would be cached by browsers and pin them to the old target.
	throw redirect(302, routes.overview);
};
