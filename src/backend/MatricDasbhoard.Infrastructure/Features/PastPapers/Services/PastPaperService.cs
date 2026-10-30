using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using MatricDasbhoard.Application.Caching.Constants;
using MatricDasbhoard.Application.Features.FileStorage;
using MatricDasbhoard.Application.Features.PastPapers;
using MatricDasbhoard.Application.Features.PastPapers.Dtos;
using MatricDasbhoard.Application.Features.PastPapers.Enums;
using MatricDasbhoard.Application.Features.PastPapers.Parsing;
using MatricDasbhoard.Infrastructure.Features.PastPapers.Models;
using MatricDasbhoard.Shared;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MatricDasbhoard.Infrastructure.Features.PastPapers.Services;

/// <summary>
/// Serves the past paper catalog by listing <see cref="PastPaperKeyParser.KeyPrefix"/> in object storage,
/// parsing each key, linking question papers to memos, and caching the result in <see cref="HybridCache"/>.
/// </summary>
internal sealed class PastPaperService(
    IFileStorageService fileStorageService,
    HybridCache hybridCache,
    ILogger<PastPaperService> logger) : IPastPaperService
{
    private static readonly HybridCacheEntryOptions CatalogCacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(1),
        LocalCacheExpiration = TimeSpan.FromHours(1)
    };

    /// <summary>
    /// A storage failure is cached briefly so an outage does not re-list storage on every request.
    /// </summary>
    internal static readonly TimeSpan FailureCacheDuration = TimeSpan.FromSeconds(30);

    /// <summary>
    /// An empty listing is suspicious (wrong bucket, upload not run yet), so it is only cached briefly.
    /// </summary>
    internal static readonly TimeSpan EmptyCacheDuration = TimeSpan.FromMinutes(1);

    private static readonly HybridCacheEntryOptions FailureCacheOptions = new()
    {
        Expiration = FailureCacheDuration,
        LocalCacheExpiration = FailureCacheDuration
    };

    private static readonly HybridCacheEntryOptions EmptyCacheOptions = new()
    {
        Expiration = EmptyCacheDuration,
        LocalCacheExpiration = EmptyCacheDuration
    };

    /// <summary>
    /// MIME types for every extension the parser accepts. Storage metadata is not trusted because
    /// the upload tooling labels every object as PDF.
    /// </summary>
    private static readonly FrozenDictionary<string, string> ContentTypes = new Dictionary<string, string>
    {
        ["pdf"] = "application/pdf",
        ["doc"] = "application/msword",
        ["docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ["ppt"] = "application/vnd.ms-powerpoint",
        ["pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ["xls"] = "application/vnd.ms-excel",
        ["xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ["zip"] = "application/zip"
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <inheritdoc />
    public async Task<Result<PastPaperListOutput>> ListAsync(
        ListPastPapersInput input, CancellationToken cancellationToken)
    {
        var catalog = await GetCatalogAsync(cancellationToken);
        if (!catalog.IsAvailable)
        {
            return Result<PastPaperListOutput>.Failure(
                ErrorMessages.PastPapers.CatalogUnavailable, ErrorType.ServiceUnavailable);
        }

        var filtered = Filter(catalog.Sorted(input.Sort), input).ToList();

        // Computed in long: (int.MaxValue - 1) * 100 overflows int and would wrap to an earlier page.
        var offset = ((long)input.PageNumber - 1) * input.PageSize;
        IReadOnlyList<PastPaperOutput> page = offset >= filtered.Count
            ? []
            : filtered.Skip((int)offset).Take(input.PageSize).ToList();

        return Result<PastPaperListOutput>.Success(
            new PastPaperListOutput(page, filtered.Count, input.PageNumber, input.PageSize));
    }

    /// <inheritdoc />
    public async Task<Result<PastPaperFacetsOutput>> GetFacetsAsync(CancellationToken cancellationToken)
    {
        var catalog = await GetCatalogAsync(cancellationToken);
        return catalog.IsAvailable
            ? Result<PastPaperFacetsOutput>.Success(catalog.Facets)
            : Result<PastPaperFacetsOutput>.Failure(
                ErrorMessages.PastPapers.CatalogUnavailable, ErrorType.ServiceUnavailable);
    }

    /// <inheritdoc />
    public async Task<Result<PastPaperFileOutput>> OpenFileAsync(string id, CancellationToken cancellationToken)
    {
        if (!PastPaperId.IsValid(id))
        {
            return Result<PastPaperFileOutput>.Failure(ErrorMessages.PastPapers.InvalidId, ErrorType.Validation);
        }

        var catalog = await GetCatalogAsync(cancellationToken);
        if (!catalog.IsAvailable)
        {
            return Result<PastPaperFileOutput>.Failure(
                ErrorMessages.PastPapers.CatalogUnavailable, ErrorType.ServiceUnavailable);
        }

        var entry = catalog.Find(id);
        if (entry is null)
        {
            return Result<PastPaperFileOutput>.Failure(ErrorMessages.PastPapers.NotFound, ErrorType.NotFound);
        }

        var opened = await fileStorageService.OpenReadAsync(entry.Key, cancellationToken);
        if (!opened.IsSuccess)
        {
            if (opened.ErrorType == ErrorType.NotFound)
            {
                logger.LogWarning("Past paper '{PaperId}' is in the catalog but missing from storage", id);
                return Result<PastPaperFileOutput>.Failure(ErrorMessages.PastPapers.NotFound, ErrorType.NotFound);
            }

            return Result<PastPaperFileOutput>.Failure(
                opened.Error ?? ErrorMessages.FileStorage.ReadFailed, opened.ErrorType ?? ErrorType.ServiceUnavailable);
        }

        var file = opened.Value;
        var paper = entry.Paper;
        var output = new PastPaperFileOutput(
            Content: file.Content,
            ContentType: ContentTypes.GetValueOrDefault(paper.FileType, "application/octet-stream"),
            ContentLength: file.ContentLength ?? paper.SizeBytes,
            FileName: $"{SafeFileNameStem(paper.Title)}.{paper.FileType}",
            CanDisplayInline: paper.FileType == "pdf",
            LastModified: entry.LastModified);

        return Result<PastPaperFileOutput>.Success(output);
    }

    /// <summary>
    /// Returns the cached catalog, building it on a miss. Storage failures and empty listings are
    /// re-cached with a short expiry so they recover quickly without hammering storage.
    /// </summary>
    private async Task<PastPaperCatalog> GetCatalogAsync(CancellationToken cancellationToken)
    {
        var builtHere = false;
        var catalog = await hybridCache.GetOrCreateAsync(
            CacheKeys.PastPaperCatalog,
            async token =>
            {
                builtHere = true;
                return await BuildCatalogAsync(token);
            },
            CatalogCacheOptions,
            cancellationToken: cancellationToken);

        if (builtHere && (!catalog.IsAvailable || catalog.Entries.Count == 0))
        {
            await hybridCache.SetAsync(
                CacheKeys.PastPaperCatalog,
                catalog,
                catalog.IsAvailable ? EmptyCacheOptions : FailureCacheOptions,
                cancellationToken: cancellationToken);
        }

        return catalog;
    }

    private async ValueTask<PastPaperCatalog> BuildCatalogAsync(CancellationToken cancellationToken)
    {
        var listed = await fileStorageService.ListAsync(PastPaperKeyParser.KeyPrefix, cancellationToken);
        if (!listed.IsSuccess)
        {
            logger.LogError("Failed to list past papers from storage, caching the failure for {Seconds}s: {Error}",
                FailureCacheDuration.TotalSeconds, listed.Error);
            return PastPaperCatalog.Unavailable();
        }

        var parsed = new List<(ParsedPastPaper Paper, DateTimeOffset? LastModified)>(listed.Value.Count);
        foreach (var stored in listed.Value)
        {
            if (PastPaperKeyParser.Parse(stored.Key, stored.Size) is { } paper)
            {
                parsed.Add((paper, stored.LastModified));
            }
        }

        var memoLinks = PastPaperMemoLinker.Link(parsed.Select(p => p.Paper).ToList());

        var entries = parsed
            .Select(item =>
            {
                var p = item.Paper;
                return new PastPaperCatalogEntry(p.Key, new PastPaperOutput(
                    p.Id, p.Title, p.Subject, p.Year, p.Session, p.Grade, p.PaperNumber, p.Kind, p.Language,
                    p.Variant, p.FileType, p.SizeBytes, memoLinks.GetValueOrDefault(p.Id)), item.LastModified);
            })
            .ToList();

        if (entries.Count == 0)
        {
            logger.LogWarning(
                "No past papers found under '{Prefix}' ({Listed} objects listed), caching the empty catalog for {Seconds}s",
                PastPaperKeyParser.KeyPrefix, listed.Value.Count, EmptyCacheDuration.TotalSeconds);
        }
        else
        {
            logger.LogInformation(
                "Built past paper catalog: {Count} papers, {MemoLinks} memo links, {Skipped} objects skipped",
                entries.Count, memoLinks.Count, listed.Value.Count - entries.Count);
        }

        return new PastPaperCatalog(entries);
    }

    private static IEnumerable<PastPaperOutput> Filter(IEnumerable<PastPaperOutput> papers, ListPastPapersInput input)
    {
        if (!string.IsNullOrWhiteSpace(input.Subject))
        {
            var subject = input.Subject.Trim();
            papers = papers.Where(p => string.Equals(p.Subject, subject, StringComparison.OrdinalIgnoreCase));
        }

        if (input.Year is { } year)
        {
            papers = papers.Where(p => p.Year == year);
        }

        if (input.Session is { } session)
        {
            papers = papers.Where(p => p.Session == session);
        }

        if (input.Grade is { } grade)
        {
            papers = papers.Where(p => p.Grade == grade);
        }

        if (input.Language is { } language)
        {
            papers = language == PastPaperLanguage.EnglishAndAfrikaans
                ? papers.Where(p => p.Language == PastPaperLanguage.EnglishAndAfrikaans)
                : papers.Where(p => p.Language == language || p.Language == PastPaperLanguage.EnglishAndAfrikaans);
        }

        if (input.PaperNumber is { } paperNumber)
        {
            papers = papers.Where(p => p.PaperNumber == paperNumber);
        }

        if (input.Kind is { } kind)
        {
            papers = papers.Where(p => p.Kind == kind);
        }

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var terms = input.Search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            papers = papers.Where(p => terms.All(term =>
                p.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                || p.Subject.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        return papers;
    }

    /// <summary>
    /// Removes characters that are unsafe in a download filename (control characters, invisible
    /// format characters such as U+202E RIGHT-TO-LEFT OVERRIDE, quotes, path separators, and reserved
    /// characters), keeping the title readable.
    /// </summary>
    private static string SafeFileNameStem(string title)
    {
        var builder = new StringBuilder(title.Length);
        foreach (var c in title)
        {
            if (char.IsControl(c)
                || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Format
                || c is '"' or '\\' or '/' or ':' or '*' or '?' or '<' or '>' or '|' or ';')
            {
                continue;
            }

            builder.Append(c);
        }

        var stem = builder.ToString().Trim().TrimEnd('.');
        return stem.Length == 0 ? "past-paper" : stem;
    }
}
