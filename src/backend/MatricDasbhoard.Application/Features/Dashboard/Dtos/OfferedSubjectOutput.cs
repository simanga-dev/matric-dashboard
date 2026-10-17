namespace MatricDasbhoard.Application.Features.Dashboard.Dtos;

/// <summary>
/// A subject offered by a school, with the most recent year it was offered.
/// </summary>
/// <param name="Code">Stable subject code (e.g. "accounting").</param>
/// <param name="Name">Display name of the subject.</param>
/// <param name="Year">Most recent academic year the subject was offered.</param>
/// <param name="TotalWrote">Learners who wrote the subject in that year.</param>
/// <param name="PercentAchieved">Pass percentage for the subject in that year, null when unavailable.</param>
public record OfferedSubjectOutput(
    string Code,
    string Name,
    int Year,
    int TotalWrote,
    double? PercentAchieved
);
