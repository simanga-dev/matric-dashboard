namespace MatricDasbhoard.WebApi.Features.Dashboard.Dtos;

/// <summary>
/// Aggregated per-subject NSC performance for a geographic scope (country,
/// province, or district).
/// </summary>
public class RegionSubjectsResponse
{
    /// <summary>One of "country", "province", or "district".</summary>
    public string Scope { get; init; } = string.Empty;

    /// <summary>Display name of the scope.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Province the scope belongs to, null for the country.</summary>
    public string? Province { get; init; }

    /// <summary>Academic year the aggregates cover, null when the scope has no subject data.</summary>
    public int? Year { get; init; }

    /// <summary>Years with subject data for the scope, oldest first.</summary>
    public IReadOnlyList<int> AvailableYears { get; init; } = [];

    /// <summary>Schools in the scope with subject data for the selected year.</summary>
    public int SchoolCount { get; init; }

    /// <summary>Aggregated subject rows for the selected year, ordered by subject name.</summary>
    public IReadOnlyList<RegionSubjectResponse> Subjects { get; init; } = [];
}
