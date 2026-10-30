namespace MatricDasbhoard.Infrastructure.Features.FileStorage.Streams;

/// <summary>
/// Read-only, forward-only stream wrapper that also disposes the object owning the inner stream
/// (for example an S3 <c>GetObjectResponse</c> holding the HTTP connection).
/// </summary>
/// <param name="inner">The stream to read from.</param>
/// <param name="owner">The object disposed together with <paramref name="inner"/>.</param>
internal sealed class OwnedReadStream(Stream inner, IDisposable owner) : Stream
{
    private bool _disposed;

    /// <inheritdoc />
    public override bool CanRead => !_disposed && inner.CanRead;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

    /// <inheritdoc />
    public override int Read(Span<byte> buffer) => inner.Read(buffer);

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        inner.ReadAsync(buffer, offset, count, cancellationToken);

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        inner.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) =>
        inner.CopyToAsync(destination, bufferSize, cancellationToken);

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    /// <remarks>The owner is disposed even if disposing the inner stream throws.</remarks>
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            try
            {
                inner.Dispose();
            }
            finally
            {
                owner.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    /// <remarks>The owner is disposed even if disposing the inner stream throws.</remarks>
    public override async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            try
            {
                await inner.DisposeAsync();
            }
            finally
            {
                owner.Dispose();
            }
        }

        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
