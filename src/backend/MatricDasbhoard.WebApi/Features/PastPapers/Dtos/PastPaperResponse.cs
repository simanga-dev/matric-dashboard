using JetBrains.Annotations;
using MatricDasbhoard.Application.Features.PastPapers.Enums;

namespace MatricDasbhoard.WebApi.Features.PastPapers.Dtos;

/// <summary>
/// A single past exam paper file.
/// </summary>
[UsedImplicitly]
public class PastPaperResponse
{
    /// <summary>Opaque, stable identifier (32 lowercase hex characters). Use it with the file endpoint.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Human readable title derived from the filename (without extension).</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Canonical subject name.</summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary>The year the paper was written.</summary>
    public int Year { get; init; }

    /// <summary>The examination sitting.</summary>
    public PastPaperSession Session { get; init; }

    /// <summary>The grade the paper is for (12, or 10/11 for common papers).</summary>
    public int Grade { get; init; }

    /// <summary>The paper number (1-4), null when the subject has a single paper.</summary>
    public int? PaperNumber { get; init; }

    /// <summary>The type of document.</summary>
    public PastPaperKind Kind { get; init; }

    /// <summary>The publication language, null when the filename has no language marker.</summary>
    public PastPaperLanguage? Language { get; init; }

    /// <summary>The specialisation, provincial, or version variant (e.g. "Woodworking", "Gauteng"), if any.</summary>
    public string? Variant { get; init; }

    /// <summary>The lowercase file extension (e.g. "pdf", "docx").</summary>
    public string FileType { get; init; } = string.Empty;

    /// <summary>The file size in bytes.</summary>
    public long SizeBytes { get; init; }

    /// <summary>For question papers, the identifier of the matching memo, if one exists; otherwise null.</summary>
    public string? MemoId { get; init; }
}
