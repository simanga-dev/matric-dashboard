/** Exam sitting a past paper belongs to. */
export type PastPaperSession = 'November' | 'MayJune' | 'FebMarch' | 'Exemplar' | 'CommonPaper';

/** What a stored file is: the question paper itself or a supporting document. */
export type PastPaperKind = 'QuestionPaper' | 'Memo' | 'AnswerBook' | 'Addendum' | 'Other';

/** Language a paper is written in. Bilingual papers carry both. */
export type PastPaperLanguage = 'English' | 'Afrikaans' | 'EnglishAndAfrikaans';

/** Server-side ordering for the past paper list. */
export type PastPaperSort = 'newest' | 'oldest' | 'subject';

/** One NSC past paper file held in object storage. */
export interface PastPaper {
	/** Opaque, stable id used to fetch the file. */
	id: string;
	title: string;
	subject: string;
	year: number;
	session: PastPaperSession;
	grade: number;
	paperNumber: number | null;
	kind: PastPaperKind;
	language: PastPaperLanguage | null;
	/** Sub-paper such as "Woodworking", or a province for provincial papers. */
	variant: string | null;
	/** File extension, e.g. "pdf". */
	fileType: string;
	sizeBytes: number;
	/** Id of the matching memo, set only on question papers that have one. */
	memoId: string | null;
}

/** A page of past papers. */
export interface PastPaperList {
	items: PastPaper[];
	totalCount: number;
	pageNumber: number;
	pageSize: number;
	totalPages: number;
	hasPreviousPage: boolean;
	hasNextPage: boolean;
}

/** Distinct values present in the catalog, used to build filter menus. */
export interface PastPaperFacets {
	subjects: string[];
	years: number[];
	sessions: PastPaperSession[];
	languages: PastPaperLanguage[];
	grades: number[];
	kinds: PastPaperKind[];
	paperNumbers: number[];
}

/** Filters, ordering and paging accepted by the past paper list endpoint. */
export interface PastPaperQuery {
	search?: string;
	subject?: string;
	year?: number;
	session?: PastPaperSession;
	grade?: number;
	language?: PastPaperLanguage;
	paperNumber?: number;
	kind?: PastPaperKind;
	sort?: PastPaperSort;
	pageNumber?: number;
	pageSize?: number;
}
