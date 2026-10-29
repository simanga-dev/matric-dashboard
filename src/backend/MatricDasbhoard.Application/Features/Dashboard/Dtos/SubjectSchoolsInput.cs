namespace MatricDasbhoard.Application.Features.Dashboard.Dtos;

/// <summary>
/// Filters, sorting, and paging for the schools that offered a subject.
/// </summary>
/// <param name="Province">Optional province filter (exact name).</param>
/// <param name="District">Optional district filter (exact name), typically with a province.</param>
/// <param name="Year">Optional academic year; defaults to the latest year with data in the scope.</param>
/// <param name="Search">Optional case-insensitive match on school name or EMIS number.</param>
/// <param name="PageNumber">One-based page number; values below 1 are treated as 1.</param>
/// <param name="PageSize">Items per page; clamped to 1-100.</param>
/// <param name="Sort">Column to sort by: name, province, totalWrote, achieved, or percentAchieved.</param>
/// <param name="Order">Sort direction: asc or desc. Defaults to desc for metrics, asc for name and province.</param>
public record SubjectSchoolsInput(
    string? Province = null,
    string? District = null,
    int? Year = null,
    string? Search = null,
    int PageNumber = 1,
    int PageSize = 25,
    string? Sort = null,
    string? Order = null
);
