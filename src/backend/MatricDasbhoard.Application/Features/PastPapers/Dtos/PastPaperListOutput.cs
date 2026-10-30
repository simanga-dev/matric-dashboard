namespace MatricDasbhoard.Application.Features.PastPapers.Dtos;

/// <summary>
/// A page of past papers matching the requested filters.
/// </summary>
/// <param name="Items">Papers on the current page.</param>
/// <param name="TotalCount">Total papers matching the filters.</param>
/// <param name="PageNumber">Current one-based page number.</param>
/// <param name="PageSize">Number of items per page.</param>
public record PastPaperListOutput(
    IReadOnlyList<PastPaperOutput> Items,
    int TotalCount,
    int PageNumber,
    int PageSize
);
