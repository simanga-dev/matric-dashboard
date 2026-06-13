namespace MatricDasbhoard.WebApi.Features.Dashboard.Dtos;

/// <summary>
/// Paginated list of schools.
/// </summary>
public class SchoolListResponse
{
    /// <summary>The schools for the current page.</summary>
    public IReadOnlyList<SchoolResponse> Items { get; init; } = [];

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
