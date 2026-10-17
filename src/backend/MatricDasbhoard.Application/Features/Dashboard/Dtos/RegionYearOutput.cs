namespace MatricDasbhoard.Application.Features.Dashboard.Dtos;

/// <summary>
/// Aggregated NSC performance for a geographic scope in a single academic year.
/// </summary>
/// <param name="Year">The academic year.</param>
/// <param name="TotalWrote">Learners who wrote in the scope.</param>
/// <param name="TotalPassed">Learners who passed in the scope.</param>
/// <param name="PassRate">Learner-weighted pass rate percentage for the scope.</param>
public record RegionYearOutput(
    int Year,
    int TotalWrote,
    int TotalPassed,
    double PassRate
);
