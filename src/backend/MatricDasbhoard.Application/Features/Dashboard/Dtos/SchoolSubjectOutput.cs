namespace MatricDasbhoard.Application.Features.Dashboard.Dtos;

/// <summary>
/// A school's NSC performance in a single subject for one academic year.
/// </summary>
/// <param name="SubjectId">Stable subject identifier.</param>
/// <param name="Code">Stable subject code (e.g. "accounting").</param>
/// <param name="Name">Display name of the subject.</param>
/// <param name="TotalWrote">Learners who wrote the subject; zero means the school did not offer it.</param>
/// <param name="Achieved">Learners who achieved (passed) the subject, null when unavailable.</param>
/// <param name="PercentAchieved">Pass percentage for the subject, null when unavailable.</param>
public record SchoolSubjectOutput(
    string SubjectId,
    string Code,
    string Name,
    int TotalWrote,
    int? Achieved,
    double? PercentAchieved
);
