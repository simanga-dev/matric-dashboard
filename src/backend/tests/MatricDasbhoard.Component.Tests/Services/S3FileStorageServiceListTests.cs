using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using MatricDasbhoard.Infrastructure.Features.FileStorage.Options;
using MatricDasbhoard.Infrastructure.Features.FileStorage.Services;
using MatricDasbhoard.Infrastructure.Features.FileStorage.Streams;
using MatricDasbhoard.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MatricDasbhoard.Component.Tests.Services;

/// <summary>Listing and streaming against a substituted S3 client.</summary>
public class S3FileStorageServiceListTests
{
    private readonly IAmazonS3 _s3 = Substitute.For<IAmazonS3>();
    private readonly S3FileStorageService _sut;

    public S3FileStorageServiceListTests()
    {
        var options = Options.Create(new FileStorageOptions
        {
            Endpoint = "http://storage.test", AccessKey = "a", SecretKey = "s", BucketName = "bucket"
        });
        _sut = new S3FileStorageService(_s3, options, NullLogger<S3FileStorageService>.Instance);
    }

    [Fact]
    public async Task ListAsync_FollowsContinuationTokensAcrossPages()
    {
        var modified = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
        _s3.ListObjectsV2Async(Arg.Is<ListObjectsV2Request>(r => r.ContinuationToken == null), Arg.Any<CancellationToken>())
            .Returns(new ListObjectsV2Response
            {
                S3Objects = [new S3Object { Key = "exam_papers/a.pdf", Size = 10, LastModified = modified },
                             new S3Object { Key = "exam_papers/folder/", Size = 0 }],
                IsTruncated = true,
                NextContinuationToken = "page-2"
            });
        _s3.ListObjectsV2Async(Arg.Is<ListObjectsV2Request>(r => r.ContinuationToken == "page-2"), Arg.Any<CancellationToken>())
            .Returns(new ListObjectsV2Response
            {
                S3Objects = [new S3Object { Key = "exam_papers/b.pdf", Size = 20 }],
                IsTruncated = false
            });

        var result = await _sut.ListAsync("exam_papers/", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["exam_papers/a.pdf", "exam_papers/b.pdf"], result.Value.Select(o => o.Key));
        Assert.Equal([10L, 20L], result.Value.Select(o => o.Size));
        Assert.Equal(new DateTimeOffset(modified), result.Value[0].LastModified);
        Assert.Null(result.Value[1].LastModified);
        await _s3.Received(2).ListObjectsV2Async(
            Arg.Is<ListObjectsV2Request>(r => r.BucketName == "bucket" && r.Prefix == "exam_papers/"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListAsync_S3Error_ReturnsServiceUnavailable()
    {
        _s3.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>())
            .Returns<ListObjectsV2Response>(_ => throw new AmazonS3Exception("boom"));

        var result = await _sut.ListAsync("exam_papers/", CancellationToken.None);

        Assert.Equal(ErrorType.ServiceUnavailable, result.ErrorType);
        Assert.Equal(ErrorMessages.FileStorage.ListFailed, result.Error);
    }

    [Fact]
    public async Task OpenReadAsync_MissingObject_ReturnsNotFound()
    {
        _s3.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns<GetObjectResponse>(_ => throw new AmazonS3Exception("missing") { StatusCode = HttpStatusCode.NotFound });

        var result = await _sut.OpenReadAsync("exam_papers/missing.pdf", CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task OpenReadAsync_ReturnsUnbufferedStreamWithMetadata()
    {
        var body = new MemoryStream([1, 2, 3, 4]);
        var response = new GetObjectResponse { ResponseStream = body, ContentLength = 4 };
        response.Headers.ContentType = "application/pdf";
        _s3.GetObjectAsync(Arg.Is<GetObjectRequest>(r => r.Key == "exam_papers/a.pdf"), Arg.Any<CancellationToken>())
            .Returns(response);

        var result = await _sut.OpenReadAsync("exam_papers/a.pdf", CancellationToken.None);

        Assert.True(result.IsSuccess);
        await using (var file = result.Value)
        {
            Assert.Equal("application/pdf", file.ContentType);
            Assert.Equal(4, file.ContentLength);
            Assert.False(file.Content.CanSeek);
            using var copy = new MemoryStream();
            await file.Content.CopyToAsync(copy);
            Assert.Equal([1, 2, 3, 4], copy.ToArray());
        }

        Assert.Throws<ObjectDisposedException>(() => body.ReadByte());
    }

    [Fact]
    public void OwnedReadStream_DisposesOwnerEvenWhenInnerThrows()
    {
        var owner = Substitute.For<IDisposable>();
        var stream = new OwnedReadStream(new ThrowingDisposeStream(), owner);

        Assert.Throws<IOException>(stream.Dispose);
        owner.Received(1).Dispose();
    }

    [Fact]
    public async Task OwnedReadStream_DisposeAsync_DisposesOwnerEvenWhenInnerThrows()
    {
        var owner = Substitute.For<IDisposable>();
        var stream = new OwnedReadStream(new ThrowingDisposeStream(), owner);

        await Assert.ThrowsAsync<IOException>(async () => await stream.DisposeAsync());
        owner.Received(1).Dispose();
    }

    /// <summary>A stream whose disposal fails, to prove the owner is still released.</summary>
    private sealed class ThrowingDisposeStream : MemoryStream
    {
        protected override void Dispose(bool disposing) => throw new IOException("dispose failed");

        public override ValueTask DisposeAsync() => throw new IOException("dispose failed");
    }
}
