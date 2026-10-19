namespace MatricDasbhoard.WebApi.Features.Dashboard.Dtos;

/// <summary>
/// Aggregated NSC performance for a geographic scope (country, province, district).
/// </summary>
public class RegionStatsResponse
{
    /// <summary>One of "country", "province", or "district".</summary>
    public string Scope { get; init; } = string.Empty;

    /// <summary>Display name of the scope.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Province the scope belongs to, null for the country.</summary>
    public string? Province { get; init; }

    /// <summary>Academic year the aggregates cover.</summary>
    public int Year { get; init; }

    /// <summary>Schools with performance data for the year.</summary>
    public int SchoolCount { get; init; }

    /// <summary>Learners who wrote in the scope.</summary>
    public int TotalWrote { get; init; }

    /// <summary>Learners who passed in the scope.</summary>
    public int TotalPassed { get; init; }

    /// <summary>Learner-weighted pass rate percentage for the scope.</summary>
    public double PassRate { get; init; }

    /// <summary>Learner-weighted pass rate trend by year, oldest first.</summary>
    public IReadOnlyList<RegionYearResponse> Years { get; init; } = [];
}
