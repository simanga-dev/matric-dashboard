using System.Diagnostics;
using System.Text.Json.Serialization;
using MatricDasbhoard.Application.Features.SearchIndex;
using MatricDasbhoard.Infrastructure.Persistence;
using MatricDasbhoard.Shared;
using Meilisearch;
using Meilisearch.QueryParameters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IndexResult = MatricDasbhoard.Shared.Result<MatricDasbhoard.Application.Features.SearchIndex.SchoolSearchIndexOutput>;
using MeiliIndex = Meilisearch.Index;

namespace MatricDasbhoard.Infrastructure.Features.SearchIndex.Services;

/// <summary>
/// Projects the serving database into the Meilisearch school directory index.
/// <para>
/// Documents carry the fields the previous CSV import published, plus the
/// <c>school_id</c> field the dashboard reads back before hydrating a hit from
/// PostgreSQL. The index is treated as a directory, not a data source: every
/// rebuild upserts all current schools and removes documents that no longer
/// exist in the database.
/// </para>
/// </summary>
internal sealed class SchoolSearchIndexService(
    MatricDasbhoardDbContext dbContext,
    MeilisearchClient meilisearchClient,
    ILogger<SchoolSearchIndexService> logger) : ISchoolSearchIndexService
{
    private const string IndexName = "schools";

    /// <summary>
    /// Primary key of the <c>schools</c> index. The index is created by
    /// <c>scripts/upload_to_meilisearch.py</c>, which writes the EMIS number to
    /// <c>id</c>; <c>school_id</c> is published alongside it because the API
    /// reads that field from hits before hydrating them from PostgreSQL.
    /// Meilisearch cannot change an index's primary key while documents exist.
    /// </summary>
    private const string PrimaryKey = "id";

    private const int BatchSize = 500;
    private const int TaskTimeoutMs = 120_000;
    private const int TaskPollIntervalMs = 100;
    private const int DocumentPageSize = 1_000;

    /// <summary>Years that the index publishes alongside each school.</summary>
    private static readonly int[] PublishedYears = [2022, 2023, 2024];

    /// <inheritdoc />
    public async Task<IndexResult> RebuildAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var index = meilisearchClient.Index(IndexName);

            await ApplySettingsAsync(index, cancellationToken);

            var documents = await BuildDocumentsAsync(cancellationToken);
            logger.LogInformation(
                "Publishing {Count} schools to Meilisearch index '{Index}'", documents.Count, IndexName);

            if (documents.Count == 0)
            {
                // An empty projection usually means the wrong database was reached.
                // Removing every document in that case would empty the live index.
                stopwatch.Stop();
                logger.LogWarning(
                    "Skipped publishing to Meilisearch index '{Index}' because the database returned no schools",
                    IndexName);
                return IndexResult.Success(new SchoolSearchIndexOutput(0, 0, stopwatch.ElapsedMilliseconds));
            }

            await UploadDocumentsAsync(index, documents, cancellationToken);

            var removed = await RemoveStaleDocumentsAsync(index, documents, cancellationToken);

            stopwatch.Stop();
            logger.LogInformation(
                "Meilisearch index '{Index}' rebuilt: {Indexed} indexed, {Removed} removed in {DurationMs} ms",
                IndexName, documents.Count, removed, stopwatch.ElapsedMilliseconds);

            return IndexResult.Success(
                new SchoolSearchIndexOutput(documents.Count, removed, stopwatch.ElapsedMilliseconds));
        }
        catch (SearchIndexTaskException ex)
        {
            logger.LogError(ex, "A Meilisearch task failed while rebuilding the school index");
            return IndexResult.Failure(
                ErrorMessages.SearchIndex.RebuildFailed, ErrorType.ServiceUnavailable);
        }
        catch (MeilisearchTimeoutError ex)
        {
            logger.LogError(ex, "Timed out waiting for a Meilisearch task while rebuilding the school index");
            return IndexResult.Failure(
                ErrorMessages.SearchIndex.Unavailable, ErrorType.ServiceUnavailable);
        }
        catch (MeilisearchApiError ex)
        {
            logger.LogError(ex, "Meilisearch rejected the school index rebuild");
            return IndexResult.Failure(
                ErrorMessages.SearchIndex.Unavailable, ErrorType.ServiceUnavailable);
        }
        catch (MeilisearchCommunicationError ex)
        {
            logger.LogError(ex, "Could not reach Meilisearch while rebuilding the school index");
            return IndexResult.Failure(
                ErrorMessages.SearchIndex.Unavailable, ErrorType.ServiceUnavailable);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP failure while rebuilding the school index");
            return IndexResult.Failure(
                ErrorMessages.SearchIndex.Unavailable, ErrorType.ServiceUnavailable);
        }
    }

    /// <summary>Applies the search, filter, and ranking settings used by directory search.</summary>
    private static async Task ApplySettingsAsync(MeiliIndex index, CancellationToken cancellationToken)
    {
        var settings = new Settings
        {
            SearchableAttributes =
                ["centre_name", "province", "district_name", "emis_number", "centre_number"],
            FilterableAttributes =
                ["province", "district_name", "quintile", "percent_achieved_2024",
                 "percent_achieved_2023", "percent_achieved_2022"],
            SortableAttributes =
                ["centre_name", "percent_achieved_2024", "percent_achieved_2023",
                 "percent_achieved_2022", "total_wrote_2024"],
            RankingRules = ["words", "typo", "proximity", "attribute", "sort", "exactness"]
        };

        var task = await index.UpdateSettingsAsync(settings, cancellationToken);
        await WaitForTaskAsync(index, task.TaskUid, "settings", cancellationToken);
    }

    /// <summary>Reads every non-deleted school with the years the index publishes.</summary>
    private async Task<List<SchoolSearchDocument>> BuildDocumentsAsync(CancellationToken cancellationToken)
    {
        var schools = await dbContext.Schools
            .AsNoTracking()
            .Where(school => !school.IsDeleted)
            .Select(school => new
            {
                school.EmisNumber,
                school.CentreName,
                school.Province,
                school.DistrictName,
                school.CentreNumber,
                school.Quintile,
                Performances = school.Performances
                    .Where(performance => !performance.IsDeleted && PublishedYears.Contains(performance.Year))
                    .Select(performance => new
                    {
                        performance.Year,
                        performance.ProgressedNumber,
                        performance.TotalWrote,
                        performance.TotalAchieved,
                        performance.PercentAchieved
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var documents = new List<SchoolSearchDocument>(schools.Count);

        foreach (var school in schools)
        {
            var p2022 = school.Performances.FirstOrDefault(performance => performance.Year == 2022);
            var p2023 = school.Performances.FirstOrDefault(performance => performance.Year == 2023);
            var p2024 = school.Performances.FirstOrDefault(performance => performance.Year == 2024);

            documents.Add(new SchoolSearchDocument
            {
                Id = school.EmisNumber,
                SchoolId = school.EmisNumber,
                Province = school.Province,
                DistrictName = school.DistrictName,
                EmisNumber = school.EmisNumber,
                CentreNumber = school.CentreNumber,
                CentreName = school.CentreName,
                Quintile = school.Quintile,
                ProgressedNumber2022 = p2022?.ProgressedNumber,
                TotalWrote2022 = p2022?.TotalWrote,
                TotalAchieved2022 = p2022?.TotalAchieved,
                PercentAchieved2022 = (double?)p2022?.PercentAchieved,
                ProgressedNumber2023 = p2023?.ProgressedNumber,
                TotalWrote2023 = p2023?.TotalWrote,
                TotalAchieved2023 = p2023?.TotalAchieved,
                PercentAchieved2023 = (double?)p2023?.PercentAchieved,
                ProgressedNumber2024 = p2024?.ProgressedNumber,
                TotalWrote2024 = p2024?.TotalWrote,
                TotalAchieved2024 = p2024?.TotalAchieved,
                PercentAchieved2024 = (double?)p2024?.PercentAchieved
            });
        }

        return documents;
    }

    /// <summary>Upserts documents in batches and waits for every Meilisearch task.</summary>
    private static async Task UploadDocumentsAsync(MeiliIndex index, List<SchoolSearchDocument> documents,
        CancellationToken cancellationToken)
    {
        var tasks = await index.AddDocumentsInBatchesAsync(documents, BatchSize, PrimaryKey, cancellationToken);

        foreach (var task in tasks)
        {
            await WaitForTaskAsync(index, task.TaskUid, "document upload", cancellationToken);
        }
    }

    /// <summary>Deletes documents for schools that no longer exist in the database.</summary>
    private static async Task<int> RemoveStaleDocumentsAsync(MeiliIndex index, List<SchoolSearchDocument> documents,
        CancellationToken cancellationToken)
    {
        var currentIds = documents.Select(document => document.Id).ToHashSet(StringComparer.Ordinal);
        var existingIds = new List<string>();

        for (var offset = 0; ; offset += DocumentPageSize)
        {
            var page = await index.GetDocumentsAsync<SchoolSearchDocument>(
                new DocumentsQuery { Limit = DocumentPageSize, Offset = offset, Fields = ["id"] },
                cancellationToken);

            var pageIds = (page.Results ?? [])
                .Select(document => document.Id)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();

            existingIds.AddRange(pageIds);

            if (pageIds.Count < DocumentPageSize || existingIds.Count >= page.Total)
            {
                break;
            }
        }

        var staleIds = existingIds
            .Where(id => !currentIds.Contains(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (staleIds.Count == 0)
        {
            return 0;
        }

        var deleteTask = await index.DeleteDocumentsAsync(staleIds, cancellationToken);
        await WaitForTaskAsync(index, deleteTask.TaskUid, "stale document removal", cancellationToken);

        return staleIds.Count;
    }

    /// <summary>Waits for a Meilisearch task and throws when it did not succeed.</summary>
    private static async Task WaitForTaskAsync(MeiliIndex index, int taskUid, string operation,
        CancellationToken cancellationToken)
    {
        var finished = await index.WaitForTaskAsync(taskUid, TaskTimeoutMs, TaskPollIntervalMs, cancellationToken);

        if (finished.Status != TaskInfoStatus.Succeeded)
        {
            var error = finished.Error is null
                ? "no error detail"
                : string.Join(", ", finished.Error.Select(entry => $"{entry.Key}={entry.Value}"));
            throw new SearchIndexTaskException(
                $"Meilisearch {operation} task {finished.Uid} finished with status {finished.Status}: {error}");
        }
    }

    /// <summary>Signals a Meilisearch task that did not complete successfully.</summary>
    private sealed class SearchIndexTaskException(string message) : Exception(message);

    /// <summary>Shape published to Meilisearch. Property names match the legacy CSV import.</summary>
    private sealed class SchoolSearchDocument
    {
        [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
        [JsonPropertyName("school_id")] public string SchoolId { get; init; } = string.Empty;
        [JsonPropertyName("province")] public string Province { get; init; } = string.Empty;
        [JsonPropertyName("district_name")] public string DistrictName { get; init; } = string.Empty;
        [JsonPropertyName("emis_number")] public string EmisNumber { get; init; } = string.Empty;
        [JsonPropertyName("centre_number")] public string CentreNumber { get; init; } = string.Empty;
        [JsonPropertyName("centre_name")] public string CentreName { get; init; } = string.Empty;
        [JsonPropertyName("quintile")] public int Quintile { get; init; }
        [JsonPropertyName("progressed_number_2022")] public int? ProgressedNumber2022 { get; init; }
        [JsonPropertyName("total_wrote_2022")] public int? TotalWrote2022 { get; init; }
        [JsonPropertyName("total_achieved_2022")] public int? TotalAchieved2022 { get; init; }
        [JsonPropertyName("percent_achieved_2022")] public double? PercentAchieved2022 { get; init; }
        [JsonPropertyName("progressed_number_2023")] public int? ProgressedNumber2023 { get; init; }
        [JsonPropertyName("total_wrote_2023")] public int? TotalWrote2023 { get; init; }
        [JsonPropertyName("total_achieved_2023")] public int? TotalAchieved2023 { get; init; }
        [JsonPropertyName("percent_achieved_2023")] public double? PercentAchieved2023 { get; init; }
        [JsonPropertyName("progressed_number_2024")] public int? ProgressedNumber2024 { get; init; }
        [JsonPropertyName("total_wrote_2024")] public int? TotalWrote2024 { get; init; }
        [JsonPropertyName("total_achieved_2024")] public int? TotalAchieved2024 { get; init; }
        [JsonPropertyName("percent_achieved_2024")] public double? PercentAchieved2024 { get; init; }
    }
}
