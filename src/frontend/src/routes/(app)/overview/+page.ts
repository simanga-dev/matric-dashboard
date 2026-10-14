import type { PageLoad } from './$types';
import { loadSchoolDirectory } from '$lib/components/dashboard/school-directory';

export const load: PageLoad = async ({ fetch, url }) => {
	return { schools: await loadSchoolDirectory(fetch, url.origin) };
};
