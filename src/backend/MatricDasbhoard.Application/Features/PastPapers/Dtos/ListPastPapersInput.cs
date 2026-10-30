using MatricDasbhoard.Application.Features.PastPapers.Enums;

namespace MatricDasbhoard.Application.Features.PastPapers.Dtos;

/// <summary>
/// Filters, sort order, and paging for the past paper listing.
/// </summary>
/// <param name="Search">Optional case-insensitive free-text search; every whitespace-separated term must match the title or subject.</param>
/// <param name="Subject">Optional exact subject name (case-insensitive).</param>
/// <param name="Year">Optional year.</param>
/// <param name="Session">Optional examination sitting.</param>
/// <param name="Grade">Optional grade.</param>
/// <param name="Language">Optional language. English and Afrikaans also match bilingual papers.</param>
/// <param name="PaperNumber">Optional paper number.</param>
/// <param name="Kind">Optional document kind.</param>
/// <param name="Sort">Sort order.</param>
/// <param name="PageNumber">One-based page number.</param>
/// <param name="PageSize">Number of items per page.</param>
public record ListPastPapersInput(
    string? Search,
    string? Subject,
    int? Year,
    PastPaperSession? Session,
    int? Grade,
    PastPaperLanguage? Language,
    int? PaperNumber,
    PastPaperKind? Kind,
    PastPaperSort Sort,
    int PageNumber,
    int PageSize
);
