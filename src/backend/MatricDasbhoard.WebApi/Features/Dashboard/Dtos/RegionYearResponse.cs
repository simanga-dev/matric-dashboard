namespace MatricDasbhoard.WebApi.Features.Dashboard.Dtos;

/// <summary>
/// Aggregated NSC performance for a geographic scope in a single academic year.
/// </summary>
public class RegionYearResponse
{
    /// <summary>The academic year.</summary>
    public int Year { get; init; }

    /// <summary>Learners who wrote in the scope.</summary>
    public int TotalWrote { get; init; }

    /// <summary>Learners who passed in the scope.</summary>
    public int TotalPassed { get; init; }

    /// <summary>Learner-weighted pass rate percentage for the scope.</summary>
    public double PassRate { get; init; }
}
