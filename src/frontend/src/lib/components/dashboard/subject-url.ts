/**
 * URL helpers for subject pages. The geographic scope (province, district)
 * and year live in the query string so subject views can be shared and
 * bookmarked, and so links between the subject list and a subject's detail
 * page keep the visitor's current scope.
 */

import { subjectPath } from '$lib/config';

/** Load dependency key so the subject detail page can retry a failed load via `invalidate`. */
export const SUBJECT_DETAIL_DEPENDENCY = 'app:subject-detail';

/** Province, district and year carried between subject pages. Null means unfiltered. */
export interface SubjectScopeParams {
	province: string | null;
	district: string | null;
	year: number | null;
}

/**
 * Reads the subject scope from a query string. A district is only honoured
 * together with its province, and the year must be a positive whole number.
 */
export function parseSubjectScope(params: URLSearchParams): SubjectScopeParams {
	const province = params.get('province')?.trim() || null;
	const district = province ? params.get('district')?.trim() || null : null;
	const rawYear = Number(params.get('year'));
	const year = Number.isInteger(rawYear) && rawYear > 0 ? rawYear : null;
	return { province, district, year };
}

/** Serialises a scope to `?province=..&district=..&year=..`, or an empty string when unfiltered. */
export function subjectScopeSearch(scope: Partial<SubjectScopeParams> = {}): string {
	const params = new URLSearchParams();
	if (scope.province) {
		params.set('province', scope.province);
		if (scope.district) params.set('district', scope.district);
	}
	if (scope.year) params.set('year', String(scope.year));
	const query = params.toString();
	return query ? `?${query}` : '';
}

/** Link to a subject's detail page, carrying the given scope in the query string. */
export function subjectHref(
	code: string,
	scope: Partial<SubjectScopeParams> = {}
): `/subjects/${string}` {
	return `${subjectPath(code)}${subjectScopeSearch(scope)}`;
}

/** Link back to the subject list, carrying the given scope in the query string. */
export function subjectsListHref(
	scope: Partial<SubjectScopeParams> = {}
): '/subjects' | `/subjects?${string}` {
	const search = subjectScopeSearch(scope);
	return search ? `/subjects?${search.slice(1)}` : '/subjects';
}
