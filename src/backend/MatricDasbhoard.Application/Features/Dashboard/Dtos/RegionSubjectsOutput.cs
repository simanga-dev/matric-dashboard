namespace MatricDasbhoard.Application.Features.Dashboard.Dtos;

/// <summary>
/// Aggregated NSC per-subject performance for a geographic scope: the whole
/// country, a province, or an education district.
/// </summary>
/// <param name="Scope">One of "country", "province", or "district".</param>
/// <param name="Name">Display name of the scope ("South Africa", province, or district).</param>
/// <param name="Province">Province the scope belongs to, null for the country.</param>
/// <param name="Year">Academic year the aggregates cover, null when the scope has no subject data.</param>
/// <param name="AvailableYears">Years with subject data for the scope, oldest first.</param>
/// <param name="SchoolCount">Schools in the scope with subject data for the selected year.</param>
/// <param name="Subjects">Aggregated subject rows for the selected year, ordered by subject name.</param>
public record RegionSubjectsOutput(
    string Scope,
    string Name,
    string? Province,
    int? Year,
    IReadOnlyList<int> AvailableYears,
    int SchoolCount,
    IReadOnlyList<RegionSubjectOutput> Subjects
);
