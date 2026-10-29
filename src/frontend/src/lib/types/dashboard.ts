export interface DashboardStats {
	year: number;
	topSchools: StatItem;
	examCenters: StatItem;
	totalLearners: StatItem;
	passRate: StatItem;
}

export interface StatItem {
	total: number;
	trend: number | null;
}

export interface PassRateTrend {
	year: number;
	passRate: number;
	totalLearners: number;
}

export interface School {
	id: string;
	name: string;
	province: string;
	circuit: string;
	totalWrote: number;
	totalPassed: number;
	passRate: number;
	totalAchieved: number | null;
	/** When the school's map coordinates were last corrected, or null if never edited. */
	mapUpdatedAt?: string | null;
}

/** Sortable school directory columns. */
export type SchoolSort =
	| 'name'
	| 'province'
	| 'totalWrote'
	| 'totalPassed'
	| 'passRate'
	| 'mapUpdatedAt';

/** Sort direction for the school directory. */
export type SchoolSortOrder = 'asc' | 'desc';

export interface SchoolYear {
	year: number;
	progressed: number;
	totalWrote: number;
	totalPassed: number;
	passRate: number;
}

export interface SchoolComparison {
	year?: number;
	provinceRank: number;
	provinceSchoolCount: number;
	provinceAveragePassRate: number | null;
	nationalAveragePassRate: number | null;
}

export interface OfferedSubject {
	code: string;
	name: string;
	year: number;
	totalWrote: number;
	percentAchieved: number | null;
}

export interface SchoolDetail {
	id: string;
	name: string;
	province: string;
	circuit: string;
	centreNumber: string;
	/** Socio-economic quintile (1–5), null when no source publishes one. */
	quintile: number | null;
	/** Raw DBE masterlist quintile value (e.g. "Q3", "N/A"), null when absent. */
	quintileCode?: string | null;
	totalWrote: number;
	totalPassed: number;
	passRate: number;
	totalAchieved?: number | null;
	years: SchoolYear[];
	comparison: SchoolComparison;
	subjectsOfferedYear: number | null;
	subjectsOffered: OfferedSubject[];
	/** School email address, null when the masterlist has none. */
	email?: string | null;
	/** Telephone in E.164 form, null when the masterlist has none. */
	telephone?: string | null;
	/** "Public" or "Independent", null when unknown. */
	sector?: string | null;
	/** Phase of schooling, null when unknown. */
	phase?: string | null;
	/** Normalized land ownership, null when unknown. */
	landOwnership?: string | null;
	/** Normalized building ownership, null when unknown. */
	buildingOwnership?: string | null;
	isSection21?: boolean | null;
	isNoFeeSchool?: boolean | null;
	/** "Urban" or "Rural", null when unknown. */
	urbanRural?: string | null;
	/** Latest learner and educator counts, null when no masterlist data exists. */
	enrolment?: SchoolEnrolment | null;
}

/** Learner and educator counts for a single year. */
export interface SchoolEnrolment {
	year: number;
	learners: number | null;
	educators: number | null;
}

export interface SchoolList {
	items: School[];
	totalCount: number;
	pageNumber: number;
	pageSize: number;
	totalPages: number;
	hasPreviousPage: boolean;
	hasNextPage: boolean;
}

export interface SchoolSubject {
	subjectId: string;
	code: string;
	name: string;
	totalWrote: number;
	achieved: number | null;
	percentAchieved: number | null;
}

export interface SchoolSubjects {
	schoolId: string;
	year: number | null;
	availableYears: number[];
	subjects: SchoolSubject[];
}

export interface RegionYear {
	year: number;
	totalWrote: number;
	totalPassed: number;
	passRate: number;
}

export interface RegionStats {
	scope: RegionScope;
	name: string;
	province: string | null;
	year: number;
	schoolCount: number;
	totalWrote: number;
	totalPassed: number;
	passRate: number;
	years: RegionYear[];
}

export interface RegionSubject {
	code: string;
	name: string;
	totalWrote: number;
	achieved: number | null;
	percentAchieved: number | null;
	schoolCount: number;
}

export interface RegionSubjects {
	scope: RegionScope;
	name: string;
	province: string | null;
	year: number | null;
	availableYears: number[];
	schoolCount: number;
	subjects: RegionSubject[];
}

/** Geographic scope of an aggregate: the whole country, one province, or one district. */
export type RegionScope = 'country' | 'province' | 'district';

/** One year of a subject's performance within a scope. */
export interface SubjectTrendPoint {
	year: number;
	schoolCount: number;
	totalWrote: number;
	achieved: number | null;
	percentAchieved: number | null;
}

/** A subject's performance in one province or district for the selected year. */
export interface SubjectRegionBreakdown {
	name: string;
	schoolCount: number;
	totalWrote: number;
	achieved: number | null;
	percentAchieved: number | null;
}

/** A single subject's performance for a scope and year, with its trend and regional split. */
export interface SubjectDetail {
	code: string;
	name: string;
	isGateway: boolean;
	scope: RegionScope;
	scopeName: string;
	province: string | null;
	district: string | null;
	year: number | null;
	availableYears: number[];
	schoolCount: number;
	totalWrote: number;
	achieved: number | null;
	percentAchieved: number | null;
	/** Yearly results for the scope, oldest first. */
	trend: SubjectTrendPoint[];
	/** Every province for the selected year. */
	provinces: SubjectRegionBreakdown[];
	/** Districts of the selected province, empty when no province is selected. */
	districts: SubjectRegionBreakdown[];
}

/** A school's result in one subject for the selected year. */
export interface SubjectSchool {
	/** EMIS number. */
	id: string;
	name: string;
	province: string;
	district: string;
	/** School quintile (1–5), null when not published. */
	quintile: number | null;
	totalWrote: number;
	achieved: number | null;
	percentAchieved: number | null;
}

/** Sortable columns of the schools-per-subject list. */
export type SubjectSchoolSort = 'name' | 'province' | 'totalWrote' | 'achieved' | 'percentAchieved';

/** Paginated schools that wrote a subject. */
export interface SubjectSchoolList {
	year: number | null;
	items: SubjectSchool[];
	totalCount: number;
	pageNumber: number;
	pageSize: number;
	totalPages: number;
	hasPreviousPage: boolean;
	hasNextPage: boolean;
}
