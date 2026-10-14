namespace MatricDasbhoard.Application.Features.Dashboard.Dtos;

/// <summary>
/// Per-subject NSC performance for a school in a selected academic year.
/// </summary>
/// <param name="SchoolId">Stable school identifier (EMIS number).</param>
/// <param name="Year">Academic year the subject rows cover, null when the school has no subject data.</param>
/// <param name="AvailableYears">Years with subject data for the school, oldest first.</param>
/// <param name="Subjects">Subject rows for the selected year, ordered by subject name.</param>
public record SchoolSubjectsOutput(
    string SchoolId,
    int? Year,
    IReadOnlyList<int> AvailableYears,
    IReadOnlyList<SchoolSubjectOutput> Subjects
);
