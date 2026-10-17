using MatricDasbhoard.Application.Features.Dashboard.Dtos;
using MatricDasbhoard.Shared;

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

    /// <summary>
    /// Returns aggregated NSC performance for the country, or narrowed to a
    /// province and/or district.
    /// </summary>
    /// <param name="province">Optional province filter (exact name).</param>
    /// <param name="district">Optional district filter (exact name), typically with a province.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<RegionStatsOutput> GetRegionStatsAsync(
        string? province,
        string? district,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns per-subject NSC performance aggregated across the country, or
    /// narrowed to a province and/or education district.
    /// </summary>
    /// <param name="province">Optional province filter (exact name).</param>
    /// <param name="district">Optional district filter (exact name), typically with a province.</param>
    /// <param name="year">Optional academic year; defaults to the latest year with subject data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<RegionSubjectsOutput> GetRegionSubjectsAsync(
        string? province,
        string? district,
        int? year = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single school by its stable document identifier (EMIS number),
    /// including multi-year performance and province/national comparison.
    /// </summary>
    /// <param name="id">The school's stable identifier from the search index.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<SchoolDetailOutput>> GetSchoolByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns per-subject NSC performance for a school in a selected academic year.
    /// </summary>
    /// <param name="id">The school's stable identifier from the search index.</param>
    /// <param name="year">Optional academic year; defaults to the latest year with subject data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A successful result, or a not-found failure when the school does not exist.</returns>
    Task<Result<SchoolSubjectsOutput>> GetSchoolSubjectsAsync(
        string id,
        int? year = null,
        CancellationToken cancellationToken = default);
}