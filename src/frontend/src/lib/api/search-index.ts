import { browserClient } from './client';

/**
 * Publishes the current school directory to the Meilisearch `schools` index.
 *
 * The API upserts every non-deleted school and removes documents for schools
 * that no longer exist. Requires an authenticated session.
 */
export async function seedSchoolSearchIndex(client = browserClient) {
	return client.POST('/api/v1/search-index/schools');
}
