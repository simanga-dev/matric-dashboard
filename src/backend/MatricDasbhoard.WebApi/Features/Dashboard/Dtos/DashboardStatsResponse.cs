namespace MatricDasbhoard.WebApi.Features.Dashboard.Dtos;

/// <summary>
/// Dashboard statistics response for the overview page.
/// </summary>
public class DashboardStatsResponse
{
    /// <summary>Top-performing schools count with trend.</summary>
    public StatItemResponse TopSchools { get; init; } = null!;

    /// <summary>Exam centres count with trend.</summary>
    public StatItemResponse ExamCenters { get; init; } = null!;

    /// <summary>Total matric learners with trend.</summary>
    public StatItemResponse TotalLearners { get; init; } = null!;

    /// <summary>Overall pass rate with trend.</summary>
    public StatItemResponse PassRate { get; init; } = null!;
}

/// <summary>
/// A single stat item with a total and optional trend.
/// </summary>
public class StatItemResponse
{
    /// <summary>Current value.</summary>
    public int Total { get; init; }

    /// <summary>Percentage change trend, or null if not applicable.</summary>
    public double? Trend { get; init; }
}
