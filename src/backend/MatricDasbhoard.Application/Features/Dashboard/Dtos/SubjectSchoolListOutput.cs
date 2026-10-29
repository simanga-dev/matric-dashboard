namespace MatricDasbhoard.Application.Features.Dashboard.Dtos;

/// <summary>
/// Paginated schools that offered a subject in the selected academic year.
/// </summary>
/// <param name="Year">Selected academic year, null when the scope has no data for the subject.</param>
/// <param name="Items">Schools on the current page.</param>
/// <param name="TotalCount">Total schools matching the filters.</param>
/// <param name="PageNumber">Current one-based page number.</param>
/// <param name="PageSize">Number of items per page.</param>
public record SubjectSchoolListOutput(
    int? Year,
    IReadOnlyList<SubjectSchoolOutput> Items,
    int TotalCount,
    int PageNumber,
    int PageSize
);

/// <summary>
/// A school's result in a single subject for the selected academic year.
/// </summary>
/// <param name="Id">Stable school identifier (EMIS number).</param>
/// <param name="Name">School name.</param>
/// <param name="Province">Province where the school is located.</param>
/// <param name="District">Education district of the school.</param>
/// <param name="Quintile">School quintile (1-5), null when not published.</param>
/// <param name="TotalWrote">Learners who wrote the subject.</param>
/// <param name="Achieved">Learners who achieved the subject, null when unavailable.</param>
/// <param name="PercentAchieved">Pass percentage for the subject, null when unavailable.</param>
public record SubjectSchoolOutput(
    string Id,
    string Name,
    string Province,
    string District,
    int? Quintile,
    int TotalWrote,
    int? Achieved,
    double? PercentAchieved
);
