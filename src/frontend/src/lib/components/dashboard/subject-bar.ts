/**
 * A single subject row for a subject chart. `year` is optional and only shown
 * when it differs from the headline year, so subjects last offered earlier
 * stay legible.
 */
export interface SubjectBar {
	name: string;
	totalWrote: number;
	percentAchieved: number | null;
	year?: number;
	/** Optional resolved link, e.g. to the subject's detail page. The bar renders as a link. */
	href?: string;
}
