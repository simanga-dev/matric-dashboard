namespace MatricDasbhoard.Application.Features.Dashboard.Dtos;

/// <summary>
/// A single subject's NSC performance for a geographic scope (the whole
/// country, a province, or an education district), with a yearly trend and
/// province and district breakdowns for the selected year.
/// </summary>
/// <param name="Code">Stable subject code (e.g. "mathematics").</param>
/// <param name="Name">Display name of the subject.</param>
/// <param name="IsGateway">Whether the subject is treated as a gateway subject.</param>
/// <param name="Scope">One of "country", "province", or "district".</param>
/// <param name="ScopeName">Display name of the scope ("South Africa", province, or district).</param>
/// <param name="Province">Province filter applied to the scope, null for the country.</param>
/// <param name="District">District filter applied to the scope, null when not narrowed to a district.</param>
/// <param name="Year">Selected academic year, null when the scope has no data for the subject.</param>
/// <param name="AvailableYears">Years with data for the subject in the scope, oldest first.</param>
/// <param name="SchoolCount">Schools in the scope that offered the subject in the selected year.</param>
/// <param name="TotalWrote">Learners who wrote the subject in the scope in the selected year.</param>
/// <param name="Achieved">Learners who achieved the subject, null when any school lacks an exact count.</param>
/// <param name="PercentAchieved">Cohort-weighted pass percentage, null when any school lacks results.</param>
/// <param name="Trend">Aggregates for every available year in the scope, oldest first.</param>
/// <param name="Provinces">National per-province aggregates for the selected year, ordered by name.</param>
/// <param name="Districts">Per-district aggregates within the selected province, ordered by name; empty without a province.</param>
public record SubjectDetailOutput(
    string Code,
    string Name,
    bool IsGateway,
    string Scope,
    string ScopeName,
    string? Province,
    string? District,
    int? Year,
    IReadOnlyList<int> AvailableYears,
    int SchoolCount,
    int TotalWrote,
    int? Achieved,
    double? PercentAchieved,
    IReadOnlyList<SubjectYearOutput> Trend,
    IReadOnlyList<SubjectRegionOutput> Provinces,
    IReadOnlyList<SubjectRegionOutput> Districts
);

/// <summary>
/// A subject's aggregated performance in a scope for one academic year.
/// </summary>
/// <param name="Year">Academic year.</param>
/// <param name="SchoolCount">Schools that offered the subject.</param>
/// <param name="TotalWrote">Learners who wrote the subject.</param>
/// <param name="Achieved">Learners who achieved the subject, null when unavailable.</param>
/// <param name="PercentAchieved">Cohort-weighted pass percentage, null when unavailable.</param>
public record SubjectYearOutput(
    int Year,
    int SchoolCount,
    int TotalWrote,
    int? Achieved,
    double? PercentAchieved
);

/// <summary>
/// A subject's aggregated performance in a single province or district.
/// </summary>
/// <param name="Name">Province or district name.</param>
/// <param name="SchoolCount">Schools that offered the subject.</param>
/// <param name="TotalWrote">Learners who wrote the subject.</param>
/// <param name="Achieved">Learners who achieved the subject, null when unavailable.</param>
/// <param name="PercentAchieved">Cohort-weighted pass percentage, null when unavailable.</param>
public record SubjectRegionOutput(
    string Name,
    int SchoolCount,
    int TotalWrote,
    int? Achieved,
    double? PercentAchieved
);
