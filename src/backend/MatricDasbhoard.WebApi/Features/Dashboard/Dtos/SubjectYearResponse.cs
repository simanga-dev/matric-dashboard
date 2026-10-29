namespace MatricDasbhoard.WebApi.Features.Dashboard.Dtos;

/// <summary>
/// A subject's aggregated performance in a scope for one academic year.
/// </summary>
public class SubjectYearResponse
{
    /// <summary>Academic year.</summary>
    public int Year { get; init; }

    /// <summary>Schools that offered the subject.</summary>
    public int SchoolCount { get; init; }

    /// <summary>Learners who wrote the subject.</summary>
    public int TotalWrote { get; init; }

    /// <summary>Learners who achieved the subject, null when unavailable.</summary>
    public int? Achieved { get; init; }

    /// <summary>Cohort-weighted pass percentage (one decimal), null when unavailable.</summary>
    public double? PercentAchieved { get; init; }
}
