using System.ComponentModel;
using System.Text.Json.Serialization;
using MatricDasbhoard.Application.Features.PastPapers.Dtos;
using MatricDasbhoard.Application.Features.PastPapers.Enums;

namespace MatricDasbhoard.Infrastructure.Features.PastPapers.Models;

/// <summary>
/// A catalog entry pairing the public paper metadata with server-side storage details.
/// </summary>
/// <param name="Key">The storage key. Never exposed to clients.</param>
/// <param name="Paper">The public paper metadata.</param>
/// <param name="LastModified">When the stored object was last modified, if reported by storage.</param>
internal sealed record PastPaperCatalogEntry(string Key, PastPaperOutput Paper, DateTimeOffset? LastModified);

/// <summary>
/// The parsed past paper catalog, cached as a single immutable value. Marked immutable so
/// <c>HybridCache</c> can hand out the same instance instead of deserializing a copy per request.
/// </summary>
/// <remarks>
/// <see cref="Entries"/> has no meaningful order. The constructor derives everything a request needs
/// (one presorted view per <see cref="PastPaperSort"/> mode, the facets, and the id index) once, so
/// requests only filter and slice. A catalog with <see cref="IsAvailable"/> set to <c>false</c> marks a
/// storage failure that is cached briefly to avoid re-listing storage on every request during an outage.
/// </remarks>
[ImmutableObject(true)]
internal sealed class PastPaperCatalog
{
    private readonly Dictionary<string, PastPaperCatalogEntry> _byId;
    private readonly IReadOnlyList<PastPaperOutput> _newest;
    private readonly IReadOnlyList<PastPaperOutput> _oldest;
    private readonly IReadOnlyList<PastPaperOutput> _bySubject;

    /// <summary>
    /// Creates a catalog and precomputes its sorted views, facets, and id index.
    /// </summary>
    /// <param name="entries">The catalog entries, in any order.</param>
    /// <param name="isAvailable"><c>false</c> to mark a storage failure; <paramref name="entries"/> is then empty.</param>
    public PastPaperCatalog(IReadOnlyList<PastPaperCatalogEntry> entries, bool isAvailable = true)
    {
        Entries = entries;
        IsAvailable = isAvailable;
        _byId = entries.ToDictionary(e => e.Paper.Id, StringComparer.Ordinal);

        var papers = entries.Select(e => e.Paper).ToList();
        _newest = Sort(papers, PastPaperSort.Newest);
        _oldest = Sort(papers, PastPaperSort.Oldest);
        _bySubject = Sort(papers, PastPaperSort.Subject);
        Facets = BuildFacets(papers);
    }

    /// <summary>
    /// Gets a catalog marking a storage failure.
    /// </summary>
    public static PastPaperCatalog Unavailable() => new([], isAvailable: false);

    /// <summary>
    /// Gets all entries, in no particular order.
    /// </summary>
    public IReadOnlyList<PastPaperCatalogEntry> Entries { get; }

    /// <summary>
    /// Gets whether the catalog was loaded from storage (<c>false</c> marks a cached storage failure).
    /// </summary>
    public bool IsAvailable { get; }

    /// <summary>
    /// Gets the distinct filter values, computed once at build time.
    /// </summary>
    [JsonIgnore]
    public PastPaperFacetsOutput Facets { get; }

    /// <summary>
    /// Returns every paper presorted for the requested sort mode.
    /// </summary>
    /// <param name="sort">The sort mode.</param>
    /// <returns>All papers in that order.</returns>
    public IReadOnlyList<PastPaperOutput> Sorted(PastPaperSort sort) => sort switch
    {
        PastPaperSort.Oldest => _oldest,
        PastPaperSort.Subject => _bySubject,
        _ => _newest
    };

    /// <summary>
    /// Looks up an entry by paper identifier.
    /// </summary>
    /// <param name="id">The paper identifier.</param>
    /// <returns>The entry, or <c>null</c> when unknown.</returns>
    public PastPaperCatalogEntry? Find(string id) => _byId.GetValueOrDefault(id);

    private static PastPaperFacetsOutput BuildFacets(IReadOnlyList<PastPaperOutput> papers) => new(
        Subjects: papers.Select(p => p.Subject).Distinct().Order(StringComparer.OrdinalIgnoreCase).ToList(),
        Years: papers.Select(p => p.Year).Distinct().OrderDescending().ToList(),
        Sessions: papers.Select(p => p.Session).Distinct().Order().ToList(),
        Languages: papers.Where(p => p.Language is not null).Select(p => p.Language!.Value).Distinct().Order().ToList(),
        Grades: papers.Select(p => p.Grade).Distinct().Order().ToList(),
        Kinds: papers.Select(p => p.Kind).Distinct().Order().ToList(),
        PaperNumbers: papers.Where(p => p.PaperNumber is not null).Select(p => p.PaperNumber!.Value).Distinct().Order().ToList());

    /// <summary>
    /// Sorts papers for a sort mode. Newest: year descending, then November, May/June, Feb/March,
    /// exemplar, common paper. Oldest: year ascending with sessions in calendar order. Subject: subject
    /// ascending, then newest first. Ties break on grade, paper number, variant, kind, language, title, id.
    /// </summary>
    private static List<PastPaperOutput> Sort(IEnumerable<PastPaperOutput> papers, PastPaperSort sort)
    {
        var ordered = sort switch
        {
            PastPaperSort.Oldest => papers
                .OrderBy(p => p.Year)
                .ThenBy(p => OldestSessionRank(p.Session))
                .ThenBy(p => p.Subject, StringComparer.OrdinalIgnoreCase),
            PastPaperSort.Subject => papers
                .OrderBy(p => p.Subject, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(p => p.Year)
                .ThenBy(p => NewestSessionRank(p.Session)),
            _ => papers
                .OrderByDescending(p => p.Year)
                .ThenBy(p => NewestSessionRank(p.Session))
                .ThenBy(p => p.Subject, StringComparer.OrdinalIgnoreCase)
        };

        return ordered
            .ThenByDescending(p => p.Grade)
            .ThenBy(p => p.PaperNumber ?? int.MaxValue)
            .ThenBy(p => p.Variant ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Kind)
            .ThenBy(p => p.Language is { } language ? (int)language : int.MaxValue)
            .ThenBy(p => p.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Id, StringComparer.Ordinal)
            .ToList();
    }

    private static int NewestSessionRank(PastPaperSession session) => session switch
    {
        PastPaperSession.November => 0,
        PastPaperSession.MayJune => 1,
        PastPaperSession.FebMarch => 2,
        PastPaperSession.Exemplar => 3,
        _ => 4
    };

    private static int OldestSessionRank(PastPaperSession session) => session switch
    {
        PastPaperSession.FebMarch => 0,
        PastPaperSession.MayJune => 1,
        PastPaperSession.November => 2,
        PastPaperSession.Exemplar => 3,
        _ => 4
    };
}
