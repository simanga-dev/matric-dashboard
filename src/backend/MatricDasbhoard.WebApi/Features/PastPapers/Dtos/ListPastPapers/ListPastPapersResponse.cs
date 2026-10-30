using MatricDasbhoard.WebApi.Shared;

namespace MatricDasbhoard.WebApi.Features.PastPapers.Dtos.ListPastPapers;

/// <summary>
/// Paginated list of past papers.
/// </summary>
public class ListPastPapersResponse : PaginatedResponse
{
    /// <summary>The papers for the current page.</summary>
    public IReadOnlyList<PastPaperResponse> Items { get; init; } = [];
}
