using MatricDasbhoard.Application.Features.PastPapers.Enums;

namespace MatricDasbhoard.Application.Features.PastPapers.Dtos;

/// <summary>
/// A single past exam paper file in the public catalog. The storage key is never exposed.
/// </summary>
/// <param name="Id">Opaque, stable, URL-safe identifier (32 lowercase hex characters).</param>
/// <param name="Title">Human readable title derived from the filename (without extension).</param>
/// <param name="Subject">Canonical subject name.</param>
/// <param name="Year">The year the paper was written.</param>
/// <param name="Session">The examination sitting.</param>
/// <param name="Grade">The grade the paper is for (12, or 10/11 for common papers).</param>
/// <param name="PaperNumber">The paper number (P1, P2, P3), null when the subject has a single paper.</param>
/// <param name="Kind">The type of document.</param>
/// <param name="Language">The publication language, null when the filename has no Eng/Afr marker.</param>
/// <param name="Variant">The specialisation or provincial variant (e.g. "Woodworking", "Gauteng"), if any.</param>
/// <param name="FileType">The lowercase file extension (e.g. "pdf", "docx").</param>
/// <param name="SizeBytes">The file size in bytes.</param>
/// <param name="MemoId">For question papers, the identifier of the matching memo, if one exists.</param>
public record PastPaperOutput(
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
    string? MemoId
);
