using System.ComponentModel;
using JetBrains.Annotations;
using MatricDasbhoard.Application.Features.PastPapers.Enums;
using MatricDasbhoard.WebApi.Shared;

namespace MatricDasbhoard.WebApi.Features.PastPapers.Dtos.ListPastPapers;

/// <summary>
/// Query parameters for listing past papers. Page size defaults to 48 (maximum 100).
/// </summary>
public class ListPastPapersRequest : PaginatedRequest
{
    /// <summary>
    /// Default number of papers per page.
    /// </summary>
    public const int DefaultPageSize = 48;

    /// <summary>
    /// Initializes the request with the past paper default page size.
    /// </summary>
    public ListPastPapersRequest()
    {
        PageSize = DefaultPageSize;
    }

    /// <summary>
    /// Optional case-insensitive free-text search. Every whitespace-separated term must match the title or subject.
    /// </summary>
    [Description("Optional case-insensitive free-text search. Every whitespace-separated term must match the title or subject.")]
    public string? Search { get; [UsedImplicitly] set; }

    /// <summary>
    /// Optional exact subject name (case-insensitive), as returned by the facets endpoint.
    /// </summary>
    [Description("Optional exact subject name (case-insensitive), as returned by the facets endpoint.")]
    public string? Subject { get; [UsedImplicitly] set; }

    /// <summary>
    /// Optional year the paper was written.
    /// </summary>
    [Description("Optional year the paper was written.")]
    public int? Year { get; [UsedImplicitly] set; }

    /// <summary>
    /// Optional examination sitting.
    /// </summary>
    [Description("Optional examination sitting.")]
    public PastPaperSession? Session { get; [UsedImplicitly] set; }

    /// <summary>
    /// Optional grade (10, 11, or 12).
    /// </summary>
    [Description("Optional grade (10, 11, or 12).")]
    public int? Grade { get; [UsedImplicitly] set; }

    /// <summary>
    /// Optional language. English and Afrikaans also include bilingual papers.
    /// </summary>
    [Description("Optional language. English and Afrikaans also include bilingual papers.")]
    public PastPaperLanguage? Language { get; [UsedImplicitly] set; }

    /// <summary>
    /// Optional paper number (1-4).
    /// </summary>
    [Description("Optional paper number (1-4).")]
    public int? PaperNumber { get; [UsedImplicitly] set; }

    /// <summary>
    /// Optional document kind.
    /// </summary>
    [Description("Optional document kind.")]
    public PastPaperKind? Kind { get; [UsedImplicitly] set; }

    /// <summary>
    /// Sort order: Newest (default), Oldest, or Subject.
    /// </summary>
    [Description("Sort order: Newest (default), Oldest, or Subject.")]
    public PastPaperSort Sort { get; [UsedImplicitly] set; } = PastPaperSort.Newest;
}
