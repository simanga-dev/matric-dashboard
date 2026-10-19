namespace MatricDasbhoard.WebApi.Features.Dashboard.Dtos;

/// <summary>
/// A school's NSC performance in a single subject for one academic year.
/// </summary>
public class SchoolSubjectResponse
{
    /// <summary>Stable subject identifier.</summary>
    public string SubjectId { get; init; } = string.Empty;

    /// <summary>Stable subject code (e.g. "accounting").</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Display name of the subject.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Learners who wrote the subject; zero means the school did not offer it.</summary>
    public int TotalWrote { get; init; }

    /// <summary>Learners who achieved (passed) the subject, null when unavailable.</summary>
    public int? Achieved { get; init; }

    /// <summary>Pass percentage for the subject, null when unavailable.</summary>
    public double? PercentAchieved { get; init; }
}
