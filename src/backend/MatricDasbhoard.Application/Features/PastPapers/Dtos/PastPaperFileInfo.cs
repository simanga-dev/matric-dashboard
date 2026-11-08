namespace MatricDasbhoard.Application.Features.PastPapers.Dtos;

/// <summary>
/// Non-streaming metadata about a past paper file, used to answer conditional requests without
/// opening the storage stream.
/// </summary>
/// <param name="Id">The paper identifier.</param>
/// <param name="ContentType">The MIME content type derived from the file extension.</param>
/// <param name="ContentLength">The content length in bytes, if known.</param>
/// <param name="FileName">A human readable filename including the extension.</param>
/// <param name="CanDisplayInline">Whether browsers can safely render the file inline (PDF only).</param>
/// <param name="LastModified">When the stored file was last modified, if known.</param>
public sealed record PastPaperFileInfo(
    string Id,
    string ContentType,
    long? ContentLength,
    string FileName,
    bool CanDisplayInline,
    DateTimeOffset? LastModified);