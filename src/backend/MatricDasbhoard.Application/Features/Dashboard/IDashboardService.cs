using MatricDasbhoard.Application.Features.Dashboard.Dtos;

namespace MatricDasbhoard.Application.Features.Dashboard;

/// <summary>
/// Provides dashboard statistics and data for the frontend overview page.
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Returns aggregated stats (top schools, exam centers, learners, pass rate).
    /// </summary>
    Task<DashboardStatsOutput> GetStatsAsync();

    /// <summary>
    /// Returns NSC pass rate trends over multiple years.
    /// </summary>
    /// <param name="years">Filter: "5" for last 5 years, "10" for last 10, null for all.</param>
    Task<IReadOnlyList<PassRateTrendOutput>> GetPassRateTrendsAsync(string? years = null);

    /// <summary>
    /// Returns a paginated list of schools with optional search.
    /// </summary>
    Task<SchoolListOutput> GetSchoolsAsync(int pageNumber, int pageSize, string? search = null);
}
