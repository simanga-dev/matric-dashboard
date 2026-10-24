namespace MatricDasbhoard.WebApi.Features.Dashboard.Dtos;

/// <summary>
/// A single subject's NSC performance for a geographic scope (country,
/// province, or district), with a yearly trend and province and district
/// breakdowns for the selected year.
/// </summary>
public class SubjectDetailResponse
{
    /// <summary>Stable subject code (e.g. "mathematics").</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Display name of the subject.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Whether the subject is treated as a gateway subject.</summary>
    public bool IsGateway { get; init; }

    /// <summary>One of "country", "province", or "district".</summary>
    public string Scope { get; init; } = string.Empty;

    /// <summary>Display name of the scope ("South Africa", province, or district).</summary>
    public string ScopeName { get; init; } = string.Empty;

    /// <summary>Province filter applied to the scope, null for the country.</summary>
    public string? Province { get; init; }

    /// <summary>District filter applied to the scope, null when not narrowed to a district.</summary>
    public string? District { get; init; }

    /// <summary>Selected academic year, null when the scope has no data for the subject.</summary>
    public int? Year { get; init; }

    /// <summary>Years with data for the subject in the scope, oldest first.</summary>
    public IReadOnlyList<int> AvailableYears { get; init; } = [];

    /// <summary>Schools in the scope that offered the subject in the selected year.</summary>
    public int SchoolCount { get; init; }

    /// <summary>Learners who wrote the subject in the scope in the selected year.</summary>
    public int TotalWrote { get; init; }

    /// <summary>Learners who achieved the subject, null when any school lacks an exact count.</summary>
    public int? Achieved { get; init; }

    /// <summary>Cohort-weighted pass percentage (one decimal), null when any school lacks results.</summary>
    public double? PercentAchieved { get; init; }

    /// <summary>Aggregates for every available year in the scope, oldest first.</summary>
    public IReadOnlyList<SubjectYearResponse> Trend { get; init; } = [];

    /// <summary>National per-province aggregates for the selected year, ordered by name.</summary>
    public IReadOnlyList<SubjectRegionResponse> Provinces { get; init; } = [];

    /// <summary>Per-district aggregates within the selected province, ordered by name; empty without a province.</summary>
    public IReadOnlyList<SubjectRegionResponse> Districts { get; init; } = [];
}
