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

/** School directory ordering. */
export type SchoolSort = 'name' | 'updated';

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
	quintile: number;
	totalWrote: number;
	totalPassed: number;
	passRate: number;
	totalAchieved?: number | null;
	years: SchoolYear[];
	comparison: SchoolComparison;
	subjectsOfferedYear: number | null;
	subjectsOffered: OfferedSubject[];
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
	scope: 'country' | 'province' | 'district';
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
	scope: 'country' | 'province' | 'district';
	name: string;
	province: string | null;
	year: number | null;
	availableYears: number[];
	schoolCount: number;
	subjects: RegionSubject[];
}
