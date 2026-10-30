namespace MatricDasbhoard.Application.Features.PastPapers.Dtos;

/// <summary>
/// An open read stream over a past paper file, ready to be sent to a client.
/// The caller owns <see cref="Content"/> and must dispose it (directly or via this record).
/// </summary>
/// <param name="Content">The file content stream. Not seekable.</param>
/// <param name="ContentType">The MIME content type derived from the file extension.</param>
/// <param name="ContentLength">The content length in bytes, if known.</param>
/// <param name="FileName">A human readable filename including the extension (may contain non-ASCII characters).</param>
/// <param name="CanDisplayInline">Whether browsers can safely render the file inline (PDF only).</param>
/// <param name="LastModified">When the stored file was last modified, if known.</param>
public sealed record PastPaperFileOutput(
    Stream Content,
    string ContentType,
    long? ContentLength,
    string FileName,
    bool CanDisplayInline,
    DateTimeOffset? LastModified
) : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Disposes the underlying content stream.
    /// </summary>
    public ValueTask DisposeAsync() => Content.DisposeAsync();

    /// <summary>
    /// Disposes the underlying content stream.
    /// </summary>
    public void Dispose() => Content.Dispose();
}
