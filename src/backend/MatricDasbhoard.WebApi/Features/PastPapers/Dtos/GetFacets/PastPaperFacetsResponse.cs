using MatricDasbhoard.Application.Features.PastPapers.Enums;

namespace MatricDasbhoard.WebApi.Features.PastPapers.Dtos.GetFacets;

/// <summary>
/// Distinct filter values present in the past paper catalog.
/// </summary>
public class PastPaperFacetsResponse
{
    /// <summary>Subject names, ascending.</summary>
    public IReadOnlyList<string> Subjects { get; init; } = [];

    /// <summary>Years, most recent first.</summary>
    public IReadOnlyList<int> Years { get; init; } = [];

    /// <summary>Examination sittings present, in canonical order.</summary>
    public IReadOnlyList<PastPaperSession> Sessions { get; init; } = [];

    /// <summary>Languages present, in canonical order.</summary>
    public IReadOnlyList<PastPaperLanguage> Languages { get; init; } = [];

    /// <summary>Grades present, ascending.</summary>
    public IReadOnlyList<int> Grades { get; init; } = [];

    /// <summary>Document kinds present, in canonical order.</summary>
    public IReadOnlyList<PastPaperKind> Kinds { get; init; } = [];

    /// <summary>Paper numbers present, ascending.</summary>
    public IReadOnlyList<int> PaperNumbers { get; init; } = [];
}
