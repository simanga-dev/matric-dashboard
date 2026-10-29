import type { PageServerLoad } from './$types';
import { getSchoolById, getSchoolSpatial } from '$lib/server/dashboard';

export const load: PageServerLoad = async ({ fetch, url, params }) => {
	const [school, spatial] = await Promise.all([
		getSchoolById(fetch, url.origin, params.id),
		getSchoolSpatial(fetch, url.origin, params.id)
	]);
	return { school, spatial };
};
