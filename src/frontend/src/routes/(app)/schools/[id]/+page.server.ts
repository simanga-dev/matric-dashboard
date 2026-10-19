import type { PageServerLoad } from './$types';
import { getSchoolById } from '$lib/server/dashboard';

export const load: PageServerLoad = async ({ fetch, url, params }) => {
	return { school: await getSchoolById(fetch, url.origin, params.id) };
};
