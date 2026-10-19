namespace MatricDasbhoard.Application.Features.Dashboard.Dtos;

/// <summary>
/// A single subject's performance aggregated across every school in a
/// geographic scope for one academic year.
/// </summary>
/// <param name="Code">Stable subject code (e.g. "accounting").</param>
/// <param name="Name">Display name of the subject.</param>
/// <param name="TotalWrote">Learners who wrote the subject across the scope.</param>
/// <param name="Achieved">Learners who achieved (passed) the subject, null when unavailable.</param>
/// <param name="PercentAchieved">Learner-weighted pass percentage for the subject, null when unavailable.</param>
/// <param name="SchoolCount">Number of schools in the scope offering the subject.</param>
public record RegionSubjectOutput(
    string Code,
    string Name,
    int TotalWrote,
    int? Achieved,
    double? PercentAchieved,
    int SchoolCount
);
