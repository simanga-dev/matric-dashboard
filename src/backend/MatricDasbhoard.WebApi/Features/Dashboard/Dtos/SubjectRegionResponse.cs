namespace MatricDasbhoard.WebApi.Features.Dashboard.Dtos;

/// <summary>
/// A subject's aggregated performance in a single province or district.
/// </summary>
public class SubjectRegionResponse
{
    /// <summary>Province or district name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Schools that offered the subject.</summary>
    public int SchoolCount { get; init; }

    /// <summary>Learners who wrote the subject.</summary>
    public int TotalWrote { get; init; }

    /// <summary>Learners who achieved the subject, null when unavailable.</summary>
    public int? Achieved { get; init; }

    /// <summary>Cohort-weighted pass percentage (one decimal), null when unavailable.</summary>
    public double? PercentAchieved { get; init; }
}
