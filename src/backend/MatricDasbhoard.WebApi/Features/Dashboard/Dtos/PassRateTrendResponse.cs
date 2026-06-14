namespace MatricDasbhoard.WebApi.Features.Dashboard.Dtos;

/// <summary>
/// NSC pass rate data point for a single year.
/// </summary>
public class PassRateTrendResponse
{
    /// <summary>The academic year.</summary>
    public int Year { get; init; }

    /// <summary>Pass rate percentage.</summary>
    public double PassRate { get; init; }

    /// <summary>Total number of learners who wrote.</summary>
    public int TotalLearners { get; init; }
}
