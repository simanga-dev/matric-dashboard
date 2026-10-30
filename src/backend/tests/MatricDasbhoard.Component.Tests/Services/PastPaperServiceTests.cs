using MatricDasbhoard.Application.Caching.Constants;
using MatricDasbhoard.Application.Features.FileStorage;
using MatricDasbhoard.Application.Features.FileStorage.Dtos;
using MatricDasbhoard.Application.Features.PastPapers.Dtos;
using MatricDasbhoard.Application.Features.PastPapers.Enums;
using MatricDasbhoard.Application.Features.PastPapers.Parsing;
using MatricDasbhoard.Infrastructure.Features.PastPapers.Services;
using MatricDasbhoard.Shared;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace MatricDasbhoard.Component.Tests.Services;

/// <summary>Catalog filtering, paging, sorting, memo linking, facets, and file access.</summary>
public class PastPaperServiceTests : IDisposable
{
    private const string Nov2024 = "exam_papers/2024 November NSC Examination Papers/";
    private const string MayJune2024 = "exam_papers/2024 May_June NSC_SC Exam Papers/";
    private const string Nov2023 = "exam_papers/2023 NSC November Exam papers/";
    private const string Grade11 = "exam_papers/Grade 11 Common Paper (2015-2018)/";

    private static readonly string[] Keys =
    [
        Nov2024 + "Mathematics/Mathematics P1 Nov 2024 Eng.pdf",
        Nov2024 + "Mathematics/Mathematics P1 Nov 2024 Afr.pdf",
        Nov2024 + "Mathematics/Mathematics P1 Nov 2024 MG Afr & Eng.pdf",
        Nov2024 + "Mathematics/Mathematics P2 Nov 2024 Eng.pdf",
        Nov2024 + "Accounting/Accounting P1 Nov 2024 Eng.pdf",
        Nov2024 + "Accounting/Accounting P1 Nov 2024 MG Eng.pdf",
        Nov2024 + "Information Technology/DataNov2024.exe",
        MayJune2024 + "Mathematics/Mathematics P1 May-June 2024 Eng.pdf",
        Nov2023 + "History/History P1 Nov 2023 Addendum Afr.pdf",
        Grade11 + "Mathematics_ 2015/Mathematics P1 Grade 11 Nov 2015 Eng.pdf",
        "avatars/someone.webp"
    ];

    private readonly IFileStorageService _storage = Substitute.For<IFileStorageService>();
    private readonly ServiceProvider _provider;
    private readonly RecordingHybridCache _cache;
    private readonly PastPaperService _sut;

    public PastPaperServiceTests()
    {
        _provider = new ServiceCollection().AddHybridCache().Services.BuildServiceProvider();
        _cache = new RecordingHybridCache(_provider.GetRequiredService<HybridCache>());
        _sut = new PastPaperService(_storage, _cache, NullLogger<PastPaperService>.Instance);
        _storage.ListAsync(PastPaperKeyParser.KeyPrefix, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StoredObjectInfo>>.Success(
                Keys.Select((k, i) => new StoredObjectInfo(k, 1000 + i, null)).ToList()));
    }

    public void Dispose() => _provider.Dispose();

    private static ListPastPapersInput Input(
        string? search = null, string? subject = null, int? year = null, PastPaperSession? session = null,
        int? grade = null, PastPaperLanguage? language = null, int? paperNumber = null, PastPaperKind? kind = null,
        PastPaperSort sort = PastPaperSort.Newest, int pageNumber = 1, int pageSize = 48) =>
        new(search, subject, year, session, grade, language, paperNumber, kind, sort, pageNumber, pageSize);

    private async Task<IReadOnlyList<PastPaperOutput>> ListAll(ListPastPapersInput input)
    {
        var result = await _sut.ListAsync(input, CancellationToken.None);
        Assert.True(result.IsSuccess);
        return result.Value.Items;
    }

    [Fact]
    public async Task List_ExcludesUnsupportedObjectsAndNeverExposesKeys()
    {
        var result = await _sut.ListAsync(Input(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(9, result.Value.TotalCount);
        Assert.DoesNotContain(result.Value.Items, p => p.FileType == "exe");
        Assert.All(result.Value.Items, p => Assert.True(PastPaperId.IsValid(p.Id)));
        Assert.All(result.Value.Items, p => Assert.DoesNotContain("exam_papers", p.Title));
    }

    [Fact]
    public async Task List_DefaultSort_NewestYearSessionSubjectPaperThenKind()
    {
        var items = await ListAll(Input());

        Assert.Equal(
        [
            "Accounting P1 Nov 2024 Eng",
            "Accounting P1 Nov 2024 MG Eng",
            "Mathematics P1 Nov 2024 Eng",
            "Mathematics P1 Nov 2024 Afr",
            "Mathematics P1 Nov 2024 MG Afr & Eng",
            "Mathematics P2 Nov 2024 Eng",
            "Mathematics P1 May-June 2024 Eng",
            "History P1 Nov 2023 Addendum Afr",
            "Mathematics P1 Grade 11 Nov 2015 Eng"
        ], items.Select(p => p.Title));
    }

    [Fact]
    public async Task List_OldestAndSubjectSorts()
    {
        var oldest = await ListAll(Input(sort: PastPaperSort.Oldest));
        var bySubject = await ListAll(Input(sort: PastPaperSort.Subject));

        Assert.Equal(2015, oldest[0].Year);
        Assert.Equal("Mathematics P1 May-June 2024 Eng", oldest[2].Title);
        Assert.Equal(["Accounting", "Accounting", "History"], bySubject.Take(3).Select(p => p.Subject));
    }

    [Fact]
    public async Task List_LinksQuestionPapersToMemos()
    {
        var items = await ListAll(Input());
        var memo = items.Single(p => p.Title == "Mathematics P1 Nov 2024 MG Afr & Eng");

        Assert.Equal(memo.Id, items.Single(p => p.Title == "Mathematics P1 Nov 2024 Eng").MemoId);
        Assert.Equal(memo.Id, items.Single(p => p.Title == "Mathematics P1 Nov 2024 Afr").MemoId);
        Assert.Null(items.Single(p => p.Title == "Mathematics P2 Nov 2024 Eng").MemoId);
        Assert.Null(memo.MemoId);
        Assert.NotNull(items.Single(p => p.Title == "Accounting P1 Nov 2024 Eng").MemoId);
    }

    [Fact]
    public async Task List_AppliesEveryFilter()
    {
        Assert.Equal(6, (await ListAll(Input(subject: "mathematics"))).Count);
        Assert.Equal(7, (await ListAll(Input(year: 2024))).Count);
        Assert.Single(await ListAll(Input(session: PastPaperSession.MayJune)));
        Assert.Single(await ListAll(Input(grade: 11)));
        Assert.Single(await ListAll(Input(paperNumber: 2)));
        Assert.Single(await ListAll(Input(kind: PastPaperKind.Addendum)));
        Assert.Equal(2, (await ListAll(Input(kind: PastPaperKind.Memo))).Count);
        Assert.Single(await ListAll(Input(language: PastPaperLanguage.EnglishAndAfrikaans)));
        // Afrikaans includes bilingual papers.
        Assert.Equal(3, (await ListAll(Input(language: PastPaperLanguage.Afrikaans))).Count);
        // Every search term must match the title or subject.
        Assert.Equal(
            ["Mathematics P1 Nov 2024 Afr", "Mathematics P1 Nov 2024 MG Afr & Eng"],
            (await ListAll(Input(search: "mathematics afr 2024"))).Select(p => p.Title));
        Assert.Empty(await ListAll(Input(search: "mathematics history")));
    }

    [Fact]
    public async Task List_PagesResults()
    {
        var first = await _sut.ListAsync(Input(pageSize: 4), CancellationToken.None);
        var last = await _sut.ListAsync(Input(pageNumber: 3, pageSize: 4), CancellationToken.None);

        Assert.Equal(4, first.Value.Items.Count);
        Assert.Equal(9, first.Value.TotalCount);
        Assert.Single(last.Value.Items);
        Assert.Equal(3, last.Value.PageNumber);
        Assert.Equal(4, last.Value.PageSize);
    }

    [Fact]
    public async Task GetFacets_ReturnsDistinctOrderedValues()
    {
        var result = await _sut.GetFacetsAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        var facets = result.Value;
        Assert.Equal(["Accounting", "History", "Mathematics"], facets.Subjects);
        Assert.Equal([2024, 2023, 2015], facets.Years);
        Assert.Equal([PastPaperSession.November, PastPaperSession.MayJune, PastPaperSession.CommonPaper], facets.Sessions);
        Assert.Equal(
            [PastPaperLanguage.English, PastPaperLanguage.Afrikaans, PastPaperLanguage.EnglishAndAfrikaans],
            facets.Languages);
        Assert.Equal([11, 12], facets.Grades);
        Assert.Equal([PastPaperKind.QuestionPaper, PastPaperKind.Memo, PastPaperKind.Addendum], facets.Kinds);
        Assert.Equal([1, 2], facets.PaperNumbers);
    }

    [Fact]
    public async Task Catalog_IsCachedAcrossCalls()
    {
        await _sut.ListAsync(Input(), CancellationToken.None);
        await _sut.GetFacetsAsync(CancellationToken.None);
        await _sut.ListAsync(Input(subject: "History"), CancellationToken.None);

        await _storage.Received(1).ListAsync(PastPaperKeyParser.KeyPrefix, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Catalog_Success_IsCachedWithTheNormalExpiry()
    {
        await _sut.ListAsync(Input(), CancellationToken.None);

        Assert.Empty(_cache.Sets);
    }

    [Fact]
    public async Task Catalog_StorageFailure_IsCachedBrieflyAndReturns503()
    {
        _storage.ListAsync(PastPaperKeyParser.KeyPrefix, Arg.Any<CancellationToken>())
            .Returns(
                Result<IReadOnlyList<StoredObjectInfo>>.Failure(ErrorMessages.FileStorage.ListFailed, ErrorType.ServiceUnavailable),
                Result<IReadOnlyList<StoredObjectInfo>>.Success([new StoredObjectInfo(Keys[0], 1, null)]));

        var failed = await _sut.ListAsync(Input(), CancellationToken.None);
        var stillFailed = await _sut.GetFacetsAsync(CancellationToken.None);

        Assert.Equal(ErrorType.ServiceUnavailable, failed.ErrorType);
        Assert.Equal(ErrorMessages.PastPapers.CatalogUnavailable, failed.Error);
        Assert.Equal(ErrorType.ServiceUnavailable, stillFailed.ErrorType);
        await _storage.Received(1).ListAsync(PastPaperKeyParser.KeyPrefix, Arg.Any<CancellationToken>());
        var set = Assert.Single(_cache.Sets);
        Assert.Equal(PastPaperService.FailureCacheDuration, set.Expiration);
        Assert.Equal(PastPaperService.FailureCacheDuration, set.LocalCacheExpiration);

        // Once the short entry expires, the next request lists storage again and recovers.
        await _cache.Inner.RemoveAsync(CacheKeys.PastPaperCatalog);
        var recovered = await _sut.ListAsync(Input(), CancellationToken.None);

        Assert.True(recovered.IsSuccess);
        Assert.Equal(1, recovered.Value.TotalCount);
    }

    [Fact]
    public async Task Catalog_EmptyListing_IsCachedBriefly()
    {
        _storage.ListAsync(PastPaperKeyParser.KeyPrefix, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StoredObjectInfo>>.Success([]));

        var result = await _sut.ListAsync(Input(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.TotalCount);
        Assert.Empty(result.Value.Items);
        var set = Assert.Single(_cache.Sets);
        Assert.Equal(PastPaperService.EmptyCacheDuration, set.Expiration);
    }

    [Theory]
    [InlineData(int.MaxValue, 100)]
    [InlineData(int.MaxValue, 1)]
    [InlineData(3, 48)]
    public async Task List_PageBeyondEnd_ReturnsEmptyPageWithRequestedNumber(int pageNumber, int pageSize)
    {
        var result = await _sut.ListAsync(Input(pageNumber: pageNumber, pageSize: pageSize), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items);
        Assert.Equal(9, result.Value.TotalCount);
        Assert.Equal(pageNumber, result.Value.PageNumber);
        Assert.Equal(pageSize, result.Value.PageSize);
    }

    [Fact]
    public async Task OpenFile_StripsInvisibleFormatCharactersFromFileName()
    {
        // U+202E RIGHT-TO-LEFT OVERRIDE could disguise "fdp.exe" style names; U+200B is zero width.
        var key = Nov2024 + "Mathematics/Maths \u202Efdp.exe\u200B P3 Nov 2024 Eng.pdf";
        var modified = new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);
        _storage.ListAsync(PastPaperKeyParser.KeyPrefix, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StoredObjectInfo>>.Success([new StoredObjectInfo(key, 5, modified)]));
        _storage.OpenReadAsync(key, Arg.Any<CancellationToken>())
            .Returns(Result<FileStreamOutput>.Success(new FileStreamOutput(new MemoryStream([1]), "application/pdf", 1)));

        var result = await _sut.OpenFileAsync(PastPaperId.FromKey(key), CancellationToken.None);

        Assert.True(result.IsSuccess);
        await using var file = result.Value;
        Assert.Equal("Maths fdp.exe P3 Nov 2024 Eng.pdf", file.FileName);
        Assert.Equal(modified, file.LastModified);
    }

    [Fact]
    public async Task OpenFile_MalformedId_ReturnsValidationWithoutTouchingStorage()
    {
        var result = await _sut.OpenFileAsync("not-an-id", CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.ErrorType);
        await _storage.DidNotReceiveWithAnyArgs().ListAsync(default!, default);
        await _storage.DidNotReceiveWithAnyArgs().OpenReadAsync(default!, default);
    }

    [Fact]
    public async Task OpenFile_UnknownId_ReturnsNotFound()
    {
        var result = await _sut.OpenFileAsync(new string('a', 32), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Equal(ErrorMessages.PastPapers.NotFound, result.Error);
        await _storage.DidNotReceiveWithAnyArgs().OpenReadAsync(default!, default);
    }

    [Fact]
    public async Task OpenFile_KnownId_StreamsWithExtensionContentTypeAndReadableName()
    {
        var key = Keys[0];
        var content = new MemoryStream([1, 2, 3]);
        _storage.OpenReadAsync(key, Arg.Any<CancellationToken>())
            .Returns(Result<FileStreamOutput>.Success(new FileStreamOutput(content, "binary/octet-stream", 3)));

        var result = await _sut.OpenFileAsync(PastPaperId.FromKey(key), CancellationToken.None);

        Assert.True(result.IsSuccess);
        await using var file = result.Value;
        Assert.Same(content, file.Content);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal(3, file.ContentLength);
        Assert.Equal("Mathematics P1 Nov 2024 Eng.pdf", file.FileName);
        Assert.True(file.CanDisplayInline);
    }

    [Fact]
    public async Task OpenFile_ObjectMissingFromStorage_ReturnsNotFound()
    {
        var key = Keys[0];
        _storage.OpenReadAsync(key, Arg.Any<CancellationToken>())
            .Returns(Result<FileStreamOutput>.Failure(ErrorMessages.FileStorage.NotFound, ErrorType.NotFound));

        var result = await _sut.OpenFileAsync(PastPaperId.FromKey(key), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Equal(ErrorMessages.PastPapers.NotFound, result.Error);
    }

    /// <summary>Delegates to a real cache and records explicit <c>SetAsync</c> entry options.</summary>
    private sealed class RecordingHybridCache(HybridCache inner) : HybridCache
    {
        public HybridCache Inner { get; } = inner;

        public List<HybridCacheEntryOptions> Sets { get; } = [];

        public override ValueTask<T> GetOrCreateAsync<TState, T>(string key, TState state,
            Func<TState, CancellationToken, ValueTask<T>> factory, HybridCacheEntryOptions? options = null,
            IEnumerable<string>? tags = null, CancellationToken cancellationToken = default) =>
            Inner.GetOrCreateAsync(key, state, factory, options, tags, cancellationToken);

        public override ValueTask SetAsync<T>(string key, T value, HybridCacheEntryOptions? options = null,
            IEnumerable<string>? tags = null, CancellationToken cancellationToken = default)
        {
            if (options is not null)
            {
                Sets.Add(options);
            }

            return Inner.SetAsync(key, value, options, tags, cancellationToken);
        }

        public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Inner.RemoveAsync(key, cancellationToken);

        public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default) =>
            Inner.RemoveByTagAsync(tag, cancellationToken);
    }
}
