namespace MatricDasbhoard.WebApi.Features.Dashboard.Dtos;

/// <summary>
/// Paginated schools that offered a subject in the selected academic year.
/// </summary>
public class SubjectSchoolListResponse
{
    /// <summary>Selected academic year, null when the scope has no data for the subject.</summary>
    public int? Year { get; init; }

    /// <summary>The schools for the current page.</summary>
    public IReadOnlyList<SubjectSchoolResponse> Items { get; init; } = [];

    /// <summary>Total number of schools matching the query.</summary>
    public int TotalCount { get; init; }

    /// <summary>Current page number.</summary>
    public int PageNumber { get; init; }

    /// <summary>Number of items per page.</summary>
    public int PageSize { get; init; }

    /// <summary>Total number of pages.</summary>
    public int TotalPages => (TotalCount + PageSize - 1) / PageSize;

    /// <summary>Whether there is a previous page.</summary>
    public bool HasPreviousPage => PageNumber > 1;

    /// <summary>Whether there is a next page.</summary>
    public bool HasNextPage => PageNumber < TotalPages;
}
