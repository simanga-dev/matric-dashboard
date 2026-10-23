namespace MatricDasbhoard.WebApi.Features.SearchIndex.Dtos;

/// <summary>
/// Result of publishing the school directory to Meilisearch.
/// </summary>
/// <param name="Indexed">Number of schools written to the index.</param>
/// <param name="Removed">Number of stale documents removed from the index.</param>
/// <param name="DurationMs">Elapsed time for the whole rebuild.</param>
public sealed record RebuildSchoolsResponse(int Indexed, int Removed, long DurationMs);
