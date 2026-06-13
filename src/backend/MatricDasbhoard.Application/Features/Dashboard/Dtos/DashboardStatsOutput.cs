namespace MatricDasbhoard.Application.Features.Dashboard.Dtos;

/// <summary>
/// Aggregated dashboard statistics for the overview page.
/// </summary>
public record DashboardStatsOutput(
    StatItemOutput TopSchools,
    StatItemOutput ExamCenters,
    StatItemOutput TotalLearners,
    StatItemOutput PassRate
);

/// <summary>
/// A single stat item with a total value and an optional trend percentage.
/// </summary>
public record StatItemOutput(
    int Total,
    double? Trend
);

/// <summary>
/// NSC pass rate data point for a single year.
/// </summary>
public record PassRateTrendOutput(
    int Year,
    double PassRate,
    int TotalLearners
);

/// <summary>
/// A school entry with performance data.
/// </summary>
public record SchoolOutput(
    int Id,
    string Name,
    string Province,
    string Circuit,
    int TotalWrote,
    int TotalPassed,
    double PassRate,
    int? TotalAchieved
);

/// <summary>
/// Paginated list of schools.
/// </summary>
public record SchoolListOutput(
    IReadOnlyList<SchoolOutput> Items,
    int TotalCount,
    int PageNumber,
    int PageSize
);
