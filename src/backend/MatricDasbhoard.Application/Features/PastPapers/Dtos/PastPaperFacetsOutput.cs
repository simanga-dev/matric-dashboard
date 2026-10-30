using MatricDasbhoard.Application.Features.PastPapers.Enums;

namespace MatricDasbhoard.Application.Features.PastPapers.Dtos;

/// <summary>
/// Distinct filter values present in the past paper catalog.
/// </summary>
/// <param name="Subjects">Subject names, ascending.</param>
/// <param name="Years">Years, most recent first.</param>
/// <param name="Sessions">Sessions in canonical order.</param>
/// <param name="Languages">Languages in canonical order (papers without a language marker are not represented).</param>
/// <param name="Grades">Grades, ascending.</param>
/// <param name="Kinds">Document kinds in canonical order.</param>
/// <param name="PaperNumbers">Paper numbers, ascending.</param>
public record PastPaperFacetsOutput(
    IReadOnlyList<string> Subjects,
    IReadOnlyList<int> Years,
    IReadOnlyList<PastPaperSession> Sessions,
    IReadOnlyList<PastPaperLanguage> Languages,
    IReadOnlyList<int> Grades,
    IReadOnlyList<PastPaperKind> Kinds,
    IReadOnlyList<int> PaperNumbers
);
