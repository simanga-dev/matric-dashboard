namespace MatricDasbhoard.Application.Features.FileStorage.Dtos;

/// <summary>
/// An open, unbuffered read stream over an object in the object store.
/// The caller owns <see cref="Content"/> and must dispose it (directly or via this record).
/// </summary>
/// <param name="Content">The object content stream. Not seekable; read it sequentially.</param>
/// <param name="ContentType">The MIME content type reported by the store.</param>
/// <param name="ContentLength">The content length in bytes, if reported by the store.</param>
public sealed record FileStreamOutput(Stream Content, string ContentType, long? ContentLength)
    : IAsyncDisposable, IDisposable
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
