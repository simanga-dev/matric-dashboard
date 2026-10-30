using MatricDasbhoard.Application.Features.PastPapers.Enums;

namespace MatricDasbhoard.Application.Features.PastPapers.Parsing;

/// <summary>
/// Metadata parsed from a past paper storage key. Contains the raw key and grouping fields, so it
/// must never be returned to clients directly.
/// </summary>
/// <param name="Key">The raw storage key (server-side only).</param>
/// <param name="Id">The opaque identifier derived from <paramref name="Key"/>.</param>
/// <param name="Title">Human readable title derived from the filename.</param>
/// <param name="Subject">Canonical subject name.</param>
/// <param name="Year">The year the paper was written.</param>
/// <param name="Session">The examination sitting.</param>
/// <param name="Grade">The grade the paper is for.</param>
/// <param name="PaperNumber">The paper number, if any.</param>
/// <param name="Kind">The type of document.</param>
/// <param name="Language">The publication language, if marked.</param>
/// <param name="Variant">The specialisation or provincial variant, if any.</param>
/// <param name="FileType">The lowercase file extension.</param>
/// <param name="SizeBytes">The file size in bytes.</param>
/// <param name="SessionFolder">The raw session folder name (memo linking scope).</param>
/// <param name="SubjectFolder">The raw subject folder name (memo linking scope).</param>
/// <param name="MatchStem">Filename tokens with kind, language, and noise markers removed, used to pair papers with memos.</param>
/// <param name="Level">The language level token (hl, fal, sal), if present.</param>
public record ParsedPastPaper(
    string Key,
    string Id,
    string Title,
    string Subject,
    int Year,
    PastPaperSession Session,
    int Grade,
    int? PaperNumber,
    PastPaperKind Kind,
    PastPaperLanguage? Language,
    string? Variant,
    string FileType,
    long SizeBytes,
    string SessionFolder,
    string SubjectFolder,
    string MatchStem,
    string? Level
);
