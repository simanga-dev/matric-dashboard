namespace MatricDasbhoard.WebApi.Features.Dashboard.Dtos;

/// <summary>
/// Per-subject NSC performance for a school in a selected academic year.
/// </summary>
public class SchoolSubjectsResponse
{
    /// <summary>Stable school identifier (EMIS number).</summary>
    public string SchoolId { get; init; } = string.Empty;

    /// <summary>Academic year the subject rows cover, null when the school has no subject data.</summary>
    public int? Year { get; init; }

    /// <summary>Years with subject data for the school, oldest first.</summary>
    public IReadOnlyList<int> AvailableYears { get; init; } = [];

    /// <summary>Subject rows for the selected year, ordered by subject name.</summary>
    public IReadOnlyList<SchoolSubjectResponse> Subjects { get; init; } = [];
}
