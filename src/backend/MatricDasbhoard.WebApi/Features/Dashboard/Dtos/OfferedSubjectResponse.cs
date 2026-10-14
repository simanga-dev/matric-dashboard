namespace MatricDasbhoard.WebApi.Features.Dashboard.Dtos;

/// <summary>
/// A subject offered by a school, with the most recent year it was offered.
/// </summary>
public class OfferedSubjectResponse
{
    /// <summary>Stable subject code (e.g. "accounting").</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Display name of the subject.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Most recent academic year the subject was offered.</summary>
    public int Year { get; init; }

    /// <summary>Learners who wrote the subject in that year.</summary>
    public int TotalWrote { get; init; }

    /// <summary>Pass percentage for the subject in that year, null when unavailable.</summary>
    public double? PercentAchieved { get; init; }
}
