using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MatricDasbhoard.Api.Tests.Contracts;
using MatricDasbhoard.Api.Tests.Fixtures;
using MatricDasbhoard.Application.Features.PastPapers.Dtos;
using MatricDasbhoard.Application.Features.PastPapers.Enums;
using MatricDasbhoard.Shared;

namespace MatricDasbhoard.Api.Tests.Controllers;

/// <summary>Public past paper endpoints through the full HTTP pipeline (anonymous callers).</summary>
public class PastPapersControllerTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private const string BaseUrl = "/api/v1/past-papers";
    private const string PaperId = "0123456789abcdef0123456789abcdef";
    private const string MemoId = "fedcba9876543210fedcba9876543210";
    private static readonly DateTimeOffset LastModified = new(2026, 10, 1, 8, 30, 0, TimeSpan.Zero);

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PastPapersControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        factory.ResetMocks();
        _client = factory.CreateClient();
    }

    public void Dispose() => _client.Dispose();

    private static PastPaperOutput Paper() => new(
        PaperId, "Mathematics P1 Nov 2024 Eng", "Mathematics", 2024, PastPaperSession.November, 12, 1,
        PastPaperKind.QuestionPaper, PastPaperLanguage.English, null, "pdf", 524_288, MemoId);

    private void ReturnsFile(string fileName, string contentType, bool inline, byte[] content)
    {
        _factory.PastPaperService.OpenFileAsync(PaperId, Arg.Any<CancellationToken>())
            .Returns(_ => Result<PastPaperFileOutput>.Success(
                new PastPaperFileOutput(new MemoryStream(content), contentType, content.Length, fileName, inline,
                    LastModified)));
    }

    [Fact]
    public async Task List_Anonymous_ReturnsFrozenPaginatedShape()
    {
        _factory.PastPaperService.ListAsync(Arg.Any<ListPastPapersInput>(), Arg.Any<CancellationToken>())
            .Returns(Result<PastPaperListOutput>.Success(new PastPaperListOutput([Paper()], 97, 1, 48)));

        using var response = await _client.GetAsync(BaseUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contract = await response.Content.ReadFromJsonAsync<PastPaperListContract>();
        Assert.NotNull(contract);
        Assert.Equal(97, contract.TotalCount);
        Assert.Equal(1, contract.PageNumber);
        Assert.Equal(48, contract.PageSize);
        Assert.Equal(3, contract.TotalPages);
        Assert.False(contract.HasPreviousPage);
        Assert.True(contract.HasNextPage);
        var item = Assert.Single(contract.Items);
        Assert.Equal(new PastPaperContract(PaperId, "Mathematics P1 Nov 2024 Eng", "Mathematics", 2024, "November", 12,
            1, "QuestionPaper", "English", null, "pdf", 524_288, MemoId), item);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var element = json.RootElement.GetProperty("items")[0];
        Assert.False(element.TryGetProperty("key", out _));
        Assert.Equal(JsonValueKind.Null, element.GetProperty("variant").ValueKind);
    }

    [Fact]
    public async Task List_BindsFiltersWithDefaults()
    {
        _factory.PastPaperService.ListAsync(Arg.Any<ListPastPapersInput>(), Arg.Any<CancellationToken>())
            .Returns(Result<PastPaperListOutput>.Success(new PastPaperListOutput([], 0, 2, 48)));

        using var response = await _client.GetAsync(
            $"{BaseUrl}?search=algebra&subject=Mathematics&year=2024&session=MayJune&grade=12&language=Afrikaans" +
            "&paperNumber=2&kind=Memo&sort=oldest&pageNumber=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await _factory.PastPaperService.Received(1).ListAsync(
            new ListPastPapersInput("algebra", "Mathematics", 2024, PastPaperSession.MayJune, 12,
                PastPaperLanguage.Afrikaans, 2, PastPaperKind.Memo, PastPaperSort.Oldest, 2, 48),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("pageSize=101")]
    [InlineData("pageSize=0")]
    [InlineData("pageNumber=0")]
    [InlineData("grade=9")]
    [InlineData("paperNumber=5")]
    [InlineData("session=Winter")]
    [InlineData("sort=random")]
    public async Task List_InvalidQuery_Returns400ProblemDetails(string query)
    {
        using var response = await _client.GetAsync($"{BaseUrl}?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        await _factory.PastPaperService.DidNotReceiveWithAnyArgs().ListAsync(default!, default);
    }

    [Fact]
    public async Task List_CatalogUnavailable_Returns503()
    {
        _factory.PastPaperService.ListAsync(Arg.Any<ListPastPapersInput>(), Arg.Any<CancellationToken>())
            .Returns(Result<PastPaperListOutput>.Failure(
                ErrorMessages.PastPapers.CatalogUnavailable, ErrorType.ServiceUnavailable));

        using var response = await _client.GetAsync(BaseUrl);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Facets_Anonymous_ReturnsFrozenShape()
    {
        _factory.PastPaperService.GetFacetsAsync(Arg.Any<CancellationToken>())
            .Returns(Result<PastPaperFacetsOutput>.Success(new PastPaperFacetsOutput(
                ["Accounting", "Mathematics"], [2026, 2025], [PastPaperSession.November, PastPaperSession.MayJune],
                [PastPaperLanguage.English, PastPaperLanguage.EnglishAndAfrikaans], [10, 11, 12],
                [PastPaperKind.QuestionPaper, PastPaperKind.Memo], [1, 2, 3])));

        using var response = await _client.GetAsync($"{BaseUrl}/facets");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contract = await response.Content.ReadFromJsonAsync<PastPaperFacetsContract>();
        Assert.NotNull(contract);
        Assert.Equal(["Accounting", "Mathematics"], contract.Subjects);
        Assert.Equal([2026, 2025], contract.Years);
        Assert.Equal(["November", "MayJune"], contract.Sessions);
        Assert.Equal(["English", "EnglishAndAfrikaans"], contract.Languages);
        Assert.Equal([10, 11, 12], contract.Grades);
        Assert.Equal(["QuestionPaper", "Memo"], contract.Kinds);
        Assert.Equal([1, 2, 3], contract.PaperNumbers);
    }

    [Fact]
    public async Task File_MalformedId_Returns400()
    {
        _factory.PastPaperService.OpenFileAsync("NOT-AN-ID", Arg.Any<CancellationToken>())
            .Returns(Result<PastPaperFileOutput>.Failure(ErrorMessages.PastPapers.InvalidId, ErrorType.Validation));

        using var response = await _client.GetAsync($"{BaseUrl}/NOT-AN-ID/file");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task File_UnknownId_Returns404ProblemDetails()
    {
        _factory.PastPaperService.OpenFileAsync(PaperId, Arg.Any<CancellationToken>())
            .Returns(Result<PastPaperFileOutput>.Failure(ErrorMessages.PastPapers.NotFound, ErrorType.NotFound));

        using var response = await _client.GetAsync($"{BaseUrl}/{PaperId}/file");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(ErrorMessages.PastPapers.NotFound, json.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task File_Pdf_StreamsInlineWithCachingHeaders()
    {
        byte[] content = [0x25, 0x50, 0x44, 0x46];
        ReturnsFile("Mathematics P1 Nov 2024 Eng.pdf", "application/pdf", inline: true, content);

        using var response = await _client.GetAsync($"{BaseUrl}/{PaperId}/file");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(content.Length, response.Content.Headers.ContentLength);
        var disposition = response.Content.Headers.ContentDisposition;
        Assert.NotNull(disposition);
        Assert.Equal("inline", disposition.DispositionType);
        var rawDisposition = string.Join(", ", response.Content.Headers.GetValues("Content-Disposition"));
        Assert.StartsWith("inline; filename=\"Mathematics P1 Nov 2024 Eng.pdf\"", rawDisposition);
        Assert.Contains("filename*=UTF-8''Mathematics%20P1%20Nov%202024%20Eng.pdf", rawDisposition);
        Assert.Equal("public, max-age=86400", response.Headers.CacheControl?.ToString());
        Assert.Equal(LastModified, response.Content.Headers.LastModified);
        Assert.False(response.Headers.Contains("Accept-Ranges"));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(content, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task File_DownloadTrue_UsesAttachment()
    {
        ReturnsFile("Mathematics P1 Nov 2024 Eng.pdf", "application/pdf", inline: true, [1, 2, 3]);

        using var response = await _client.GetAsync($"{BaseUrl}/{PaperId}/file?download=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
    }

    [Fact]
    public async Task File_NonPdf_AlwaysAttachment()
    {
        const string docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        ReturnsFile("English FAL P1 May-June 2026 MG.docx", docx, inline: false, [1]);

        using var response = await _client.GetAsync($"{BaseUrl}/{PaperId}/file");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(docx, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
    }

    [Fact]
    public async Task File_NonAsciiName_IncludesRfc6266FilenameStar()
    {
        // U+00E2 (a with circumflex), as found in some published filenames.
        const string fileName = "Mathematics Paper 1 \u00e2 English.pdf";
        ReturnsFile(fileName, "application/pdf", inline: true, [1]);

        using var response = await _client.GetAsync($"{BaseUrl}/{PaperId}/file");

        var disposition = response.Content.Headers.ContentDisposition;
        Assert.NotNull(disposition);
        Assert.Equal(fileName, disposition.FileNameStar);
        Assert.DoesNotContain("\u00e2", disposition.FileName);
    }

    [Fact]
    public async Task File_ConcurrentStreamsPerClient_AreLimited()
    {
        // appsettings.Testing.json sets PastPaperFiles.ConcurrentStreamLimit to 2.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var opened = 0;
        var bothOpen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _factory.PastPaperService.OpenFileAsync(PaperId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref opened) == 2)
                {
                    bothOpen.TrySetResult();
                }

                return Result<PastPaperFileOutput>.Success(new PastPaperFileOutput(
                    new GatedStream(gate.Task), "application/pdf", null, "a.pdf", true, null));
            });

        var first = _client.GetAsync($"{BaseUrl}/{PaperId}/file");
        var second = _client.GetAsync($"{BaseUrl}/{PaperId}/file");
        await bothOpen.Task.WaitAsync(TimeSpan.FromSeconds(10));

        using var rejected = await _client.GetAsync($"{BaseUrl}/{PaperId}/file");
        gate.SetResult();
        using var firstResponse = await first;
        using var secondResponse = await second;
        using var afterRelease = await _client.GetAsync($"{BaseUrl}/{PaperId}/file");

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, afterRelease.StatusCode);
    }

    [Fact]
    public async Task OpenApi_DescribesFileResponseAsBinary()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        var ok = document.RootElement.GetProperty("paths").GetProperty("/api/v1/past-papers/{id}/file")
            .GetProperty("get").GetProperty("responses").GetProperty("200").GetProperty("content");
        foreach (var mediaType in new[] { "application/pdf", "application/octet-stream" })
        {
            var schema = ok.GetProperty(mediaType).GetProperty("schema");
            if (schema.TryGetProperty("$ref", out var reference))
            {
                schema = schemas.GetProperty((reference.GetString() ?? string.Empty).Split('/')[^1]);
            }

            Assert.Equal("string", schema.GetProperty("type").GetString());
            Assert.Equal("binary", schema.GetProperty("format").GetString());
        }
    }

    /// <summary>A non-seekable stream that yields no data until the gate opens.</summary>
    private sealed class GatedStream(Task gate) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await gate.WaitAsync(cancellationToken);
            return 0;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }
}
