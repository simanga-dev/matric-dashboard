namespace MatricDasbhoard.WebApi.Features.Dashboard.Dtos;

/// <summary>
/// A school's result in a single subject for the selected academic year.
/// </summary>
public class SubjectSchoolResponse
{
    /// <summary>Stable school identifier (EMIS number).</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>School name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Province where the school is located.</summary>
    public string Province { get; init; } = string.Empty;

    /// <summary>Education district of the school.</summary>
    public string District { get; init; } = string.Empty;

    /// <summary>School quintile (1-5), null when not published.</summary>
    public int? Quintile { get; init; }

    /// <summary>Learners who wrote the subject.</summary>
    public int TotalWrote { get; init; }

    /// <summary>Learners who achieved the subject, null when unavailable.</summary>
    public int? Achieved { get; init; }

    /// <summary>Pass percentage for the subject (one decimal), null when unavailable.</summary>
    public double? PercentAchieved { get; init; }
}
