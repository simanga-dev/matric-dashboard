namespace MatricDasbhoard.WebApi.Features.Dashboard.Dtos;

/// <summary>
/// A school entry with performance data.
/// </summary>
public class SchoolResponse
{
    /// <summary>Unique school identifier.</summary>
    public int Id { get; init; }

    /// <summary>School name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Province where the school is located.</summary>
    public string Province { get; init; } = string.Empty;

    /// <summary>Education circuit or district.</summary>
    public string Circuit { get; init; } = string.Empty;

    /// <summary>Total learners who wrote matric exams.</summary>
    public int TotalWrote { get; init; }

    /// <summary>Total learners who passed matric exams.</summary>
    public int TotalPassed { get; init; }

    /// <summary>Pass rate percentage.</summary>
    public double PassRate { get; init; }

    /// <summary>Learners who achieved bachelor passes, if available.</summary>
    public int? TotalAchieved { get; init; }
}
