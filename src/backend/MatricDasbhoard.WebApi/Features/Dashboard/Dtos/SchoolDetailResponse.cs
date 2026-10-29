namespace MatricDasbhoard.WebApi.Features.Dashboard.Dtos;

/// <summary>
/// Full school detail, including multi-year performance and local context.
/// </summary>
public class SchoolDetailResponse
{
    /// <summary>Unique school identifier (EMIS number).</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>School name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Province where the school is located.</summary>
    public string Province { get; init; } = string.Empty;

    /// <summary>Education circuit or district.</summary>
    public string Circuit { get; init; } = string.Empty;

    /// <summary>Examination centre number assigned by the DBE.</summary>
    public string CentreNumber { get; init; } = string.Empty;

    /// <summary>Socio-economic quintile (1 is poorest, 5 is wealthiest), null when not published.</summary>
    public int? Quintile { get; init; }

    /// <summary>Learners who wrote in the latest available year.</summary>
    public int TotalWrote { get; init; }

    /// <summary>Learners who passed in the latest available year.</summary>
    public int TotalPassed { get; init; }

    /// <summary>Pass rate percentage for the latest available year.</summary>
    public double PassRate { get; init; }

    /// <summary>Learners who achieved bachelor passes, if available.</summary>
    public int? TotalAchieved { get; init; }

    /// <summary>Yearly NSC performance records, oldest first.</summary>
    public IReadOnlyList<SchoolYearResponse> Years { get; init; } = [];

    /// <summary>How the school compares to its province and the country.</summary>
    public SchoolComparisonResponse Comparison { get; init; } = new();

    /// <summary>Latest year with subject data for the school, null when none.</summary>
    public int? SubjectsOfferedYear { get; init; }

    /// <summary>Subjects written in <see cref="SubjectsOfferedYear"/>, ordered by name.</summary>
    public IReadOnlyList<OfferedSubjectResponse> SubjectsOffered { get; init; } = [];

    /// <summary>Raw quintile value from the DBE masterlist (e.g. "Q3", "N/A").</summary>
    public string? QuintileCode { get; init; }

    /// <summary>School email address.</summary>
    public string? Email { get; init; }

    /// <summary>School telephone number in E.164 form.</summary>
    public string? Telephone { get; init; }

    /// <summary>Public or independent sector.</summary>
    public string? Sector { get; init; }

    /// <summary>Phase of schooling offered.</summary>
    public string? Phase { get; init; }

    /// <summary>Normalized land ownership.</summary>
    public string? LandOwnership { get; init; }

    /// <summary>Normalized building ownership.</summary>
    public string? BuildingOwnership { get; init; }

    /// <summary>Whether the school is a Section 21 school, null when unknown.</summary>
    public bool? IsSection21 { get; init; }

    /// <summary>Whether the school charges no fees, null when unknown.</summary>
    public bool? IsNoFeeSchool { get; init; }

    /// <summary>Urban or rural classification.</summary>
    public string? UrbanRural { get; init; }

    /// <summary>Latest learner and educator counts, null when no masterlist data exists.</summary>
    public SchoolEnrolmentResponse? Enrolment { get; init; }
}

/// <summary>
/// Learner and educator counts for a single year.
/// </summary>
public class SchoolEnrolmentResponse
{
    /// <summary>Data year the counts apply to.</summary>
    public int Year { get; init; }

    /// <summary>Learners enrolled, null when not published.</summary>
    public int? Learners { get; init; }

    /// <summary>Educators employed, null when not published.</summary>
    public int? Educators { get; init; }
}

/// <summary>
/// NSC performance for a single academic year.
/// </summary>
public class SchoolYearResponse
{
    /// <summary>The academic year.</summary>
    public int Year { get; init; }

    /// <summary>Progressed learners who wrote the exams.</summary>
    public int Progressed { get; init; }

    /// <summary>Total learners who wrote the exams.</summary>
    public int TotalWrote { get; init; }

    /// <summary>Total learners who passed the exams.</summary>
    public int TotalPassed { get; init; }

    /// <summary>Pass rate percentage.</summary>
    public double PassRate { get; init; }
}

/// <summary>
/// How a school compares to its province and the country for the selected year.
/// </summary>
public class SchoolComparisonResponse
{
    /// <summary>Academic year used for the comparison cohort.</summary>
    public int Year { get; init; }

    /// <summary>Position within the province (1 is highest), or 0 when unknown.</summary>
    public int ProvinceRank { get; init; }

    /// <summary>Number of province schools with data for the selected year.</summary>
    public int ProvinceSchoolCount { get; init; }

    /// <summary>Learner-weighted province pass rate, or null when unavailable.</summary>
    public double? ProvinceAveragePassRate { get; init; }

    /// <summary>Learner-weighted national pass rate, or null when unavailable.</summary>
    public double? NationalAveragePassRate { get; init; }
}
