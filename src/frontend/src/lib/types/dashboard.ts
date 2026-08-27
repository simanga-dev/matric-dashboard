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

export interface SchoolList {
	items: School[];
	totalCount: number;
	pageNumber: number;
	pageSize: number;
	totalPages: number;
	hasPreviousPage: boolean;
	hasNextPage: boolean;
}
