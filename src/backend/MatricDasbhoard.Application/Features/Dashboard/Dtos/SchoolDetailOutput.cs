namespace MatricDasbhoard.Application.Features.Dashboard.Dtos;

/// <summary>
/// Full school detail for the school page, including multi-year performance
/// and how the school compares to its province and the country.
/// </summary>
/// <param name="Id">Stable school identifier (EMIS number).</param>
/// <param name="Name">Official school or examination centre name.</param>
/// <param name="Province">Province where the school is located.</param>
/// <param name="Circuit">Education district or circuit name.</param>
/// <param name="CentreNumber">Examination centre number assigned by the DBE.</param>
/// <param name="Quintile">Socio-economic quintile (1 is poorest, 5 is wealthiest).</param>
/// <param name="TotalWrote">Learners who wrote in the latest available year.</param>
/// <param name="TotalPassed">Learners who passed in the latest available year.</param>
/// <param name="PassRate">Pass rate percentage for the latest available year.</param>
/// <param name="Years">Yearly NSC performance records, oldest first.</param>
/// <param name="Comparison">Local context relative to the province and the country.</param>
/// <param name="TotalAchieved">Learners who achieved bachelor passes, null when unavailable.</param>
/// <param name="SubjectsOfferedYear">Latest year with subject data for the school, null when none.</param>
/// <param name="SubjectsOffered">Subjects written in <paramref name="SubjectsOfferedYear"/>, ordered by name.</param>
public record SchoolDetailOutput(
    string Id,
    string Name,
    string Province,
    string Circuit,
    string CentreNumber,
    int Quintile,
    int TotalWrote,
    int TotalPassed,
    double PassRate,
    IReadOnlyList<SchoolYearOutput> Years,
    SchoolComparisonOutput Comparison,
    int? TotalAchieved = null,
    int? SubjectsOfferedYear = null,
    IReadOnlyList<OfferedSubjectOutput>? SubjectsOffered = null
);

/// <summary>
/// NSC performance for a single academic year.
/// </summary>
/// <param name="Year">The academic year.</param>
/// <param name="Progressed">Progressed learners who wrote the exams.</param>
/// <param name="TotalWrote">Total learners who wrote the exams.</param>
/// <param name="TotalPassed">Total learners who passed the exams.</param>
/// <param name="PassRate">Pass rate percentage.</param>
public record SchoolYearOutput(
    int Year,
    int Progressed,
    int TotalWrote,
    int TotalPassed,
    double PassRate
);

/// <summary>
/// How a school compares to its province and the country for the selected year.
/// </summary>
/// <param name="ProvinceRank">Position within the province (1 is highest), or 0 when unknown.</param>
/// <param name="ProvinceSchoolCount">Number of province schools with data for the selected year.</param>
/// <param name="ProvinceAveragePassRate">Learner-weighted province pass rate, or null when unavailable.</param>
/// <param name="NationalAveragePassRate">Learner-weighted national pass rate, or null when unavailable.</param>
/// <param name="Year">Academic year used for the comparison cohort.</param>
public record SchoolComparisonOutput(
    int ProvinceRank,
    int ProvinceSchoolCount,
    double? ProvinceAveragePassRate,
    double? NationalAveragePassRate,
    int Year = 2024
);
