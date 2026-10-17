namespace MatricDasbhoard.Application.Features.Dashboard.Dtos;

/// <summary>
/// Aggregated NSC performance for a geographic scope: the whole country, a
/// province, or an education district.
/// </summary>
/// <param name="Scope">One of "country", "province", or "district".</param>
/// <param name="Name">Display name of the scope ("South Africa", province, or district).</param>
/// <param name="Province">Province the scope belongs to, null for the country.</param>
/// <param name="Year">Academic year the aggregates cover.</param>
/// <param name="SchoolCount">Schools with performance data for the year.</param>
/// <param name="TotalWrote">Learners who wrote in the scope.</param>
/// <param name="TotalPassed">Learners who passed in the scope.</param>
/// <param name="PassRate">Learner-weighted pass rate percentage for the scope.</param>
/// <param name="Years">Learner-weighted pass rate trend by year, oldest first.</param>
public record RegionStatsOutput(
    string Scope,
    string Name,
    string? Province,
    int Year,
    int SchoolCount,
    int TotalWrote,
    int TotalPassed,
    double PassRate,
    IReadOnlyList<RegionYearOutput> Years
);
