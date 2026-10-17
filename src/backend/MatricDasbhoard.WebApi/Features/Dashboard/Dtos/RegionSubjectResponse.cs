namespace MatricDasbhoard.WebApi.Features.Dashboard.Dtos;

/// <summary>
/// A single subject's performance aggregated across every school in a
/// geographic scope for one academic year.
/// </summary>
public class RegionSubjectResponse
{
    /// <summary>Stable subject code (e.g. "accounting").</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Display name of the subject.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Learners who wrote the subject across the scope.</summary>
    public int TotalWrote { get; init; }

    /// <summary>Learners who achieved (passed) the subject, null when unavailable.</summary>
    public int? Achieved { get; init; }

    /// <summary>Learner-weighted pass percentage for the subject, null when unavailable.</summary>
    public double? PercentAchieved { get; init; }

    /// <summary>Number of schools in the scope offering the subject.</summary>
    public int SchoolCount { get; init; }
}
