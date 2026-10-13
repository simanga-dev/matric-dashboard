export interface DashboardStats {
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
}

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
