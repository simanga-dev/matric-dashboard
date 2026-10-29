using MatricDasbhoard.Shared;

namespace MatricDasbhoard.Application.Features.SearchIndex;

/// <summary>
/// Publishes the current school directory to the Meilisearch <c>schools</c> index.
/// </summary>
public interface ISchoolSearchIndexService
{
    /// <summary>
    /// Replaces the index contents with every non-deleted school in the database.
    /// </summary>
    Task<Result<SchoolSearchIndexOutput>> RebuildAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Counts from a completed search index rebuild.
/// </summary>
/// <param name="Indexed">Number of schools written to the index.</param>
/// <param name="Removed">Number of stale documents removed from the index.</param>
/// <param name="DurationMs">Elapsed time for the whole rebuild.</param>
public sealed record SchoolSearchIndexOutput(int Indexed, int Removed, long DurationMs);
