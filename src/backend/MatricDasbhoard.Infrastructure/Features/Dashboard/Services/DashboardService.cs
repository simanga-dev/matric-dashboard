using System.Text.Json.Serialization;
using MatricDasbhoard.Application.Features.Dashboard;
using MatricDasbhoard.Application.Features.Dashboard.Dtos;
using MatricDasbhoard.Shared;
using Meilisearch;

namespace MatricDasbhoard.Infrastructure.Features.Dashboard.Services;

/// <summary>
/// Returns paginated school data from the Meilisearch "schools" index.
/// </summary>
internal sealed class DashboardService : IDashboardService
{
    private static readonly IReadOnlyList<PassRateTrendOutput> PassRateTrends = new List<PassRateTrendOutput>
    {
        new(2008, 62.6, 533561),
        new(2009, 60.6, 552073),
        new(2010, 67.8, 537543),
        new(2011, 70.2, 496090),
        new(2012, 73.9, 511152),
        new(2013, 78.2, 562112),
        new(2014, 75.8, 532860),
        new(2015, 70.7, 644536),
        new(2016, 72.5, 610178),
        new(2017, 75.1, 629155),
        new(2018, 78.2, 624733),
        new(2019, 81.3, 616754),
        new(2020, 76.2, 578468),
        new(2021, 76.4, 706451),
        new(2022, 80.1, 699659),
        new(2023, 82.9, 719541),
        new(2024, 87.3, 737472)
    };

    private const string SchoolsIndex = "schools";

    private readonly MeilisearchClient _meilisearchClient;

    public DashboardService(MeilisearchClient meilisearchClient)
    {
        _meilisearchClient = meilisearchClient;
    }

    public Task<DashboardStatsOutput> GetStatsAsync()
    {
        var stats = new DashboardStatsOutput(
            TopSchools: new StatItemOutput(187, 8.5),
            ExamCenters: new StatItemOutput(6820, 2.1),
            TotalLearners: new StatItemOutput(737472, 5.4),
            PassRate: new StatItemOutput(829, 3.6)
        );
        return Task.FromResult(stats);
    }

    public Task<IReadOnlyList<PassRateTrendOutput>> GetPassRateTrendsAsync(string? years = null)
    {
        var data = years switch
        {
            "5" => PassRateTrends.Where(t => t.Year >= 2020).ToList(),
            "10" => PassRateTrends.Where(t => t.Year >= 2015).ToList(),
            _ => PassRateTrends.ToList()
        };
        return Task.FromResult<IReadOnlyList<PassRateTrendOutput>>(data);
    }

    public async Task<SchoolListOutput> GetSchoolsAsync(int pageNumber, int pageSize, string? search = null)
    {
        var index = _meilisearchClient.Index(SchoolsIndex);
        var searchText = string.IsNullOrWhiteSpace(search) ? string.Empty : search;

        var result = await index.SearchAsync<MeilisearchSchool>(
            searchText,
            new SearchQuery
            {
                Limit = pageSize,
                Offset = (pageNumber - 1) * pageSize
            });

        var schools = result.Hits.Select(hit => ToSchoolOutput(hit)).ToList();

        return new SchoolListOutput(schools, result.Hits.Count, pageNumber, pageSize);
    }

    public async Task<MatricDasbhoard.Shared.Result<SchoolOutput>> GetSchoolByIdAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return MatricDasbhoard.Shared.Result<SchoolOutput>.Failure(
                ErrorMessages.Dashboard.SchoolNotFound, ErrorType.NotFound);
        }

        var index = _meilisearchClient.Index(SchoolsIndex);
        MeilisearchSchool? document;
        try
        {
            document = await index.GetDocumentAsync<MeilisearchSchool>(id, cancellationToken: cancellationToken);
        }
        catch (MeilisearchApiError)
        {
            return MatricDasbhoard.Shared.Result<SchoolOutput>.Failure(
                ErrorMessages.Dashboard.SchoolNotFound, ErrorType.NotFound);
        }

        if (document is null)
        {
            return MatricDasbhoard.Shared.Result<SchoolOutput>.Failure(
                ErrorMessages.Dashboard.SchoolNotFound, ErrorType.NotFound);
        }

        return MatricDasbhoard.Shared.Result<SchoolOutput>.Success(ToSchoolOutput(document));
    }

    private static SchoolOutput ToSchoolOutput(MeilisearchSchool hit) => new(
        Id: hit.Id ?? string.Empty,
        Name: hit.CentreName ?? "Unknown",
        Province: hit.Province ?? string.Empty,
        Circuit: hit.DistrictName ?? string.Empty,
        TotalWrote: (int)(hit.TotalWrote ?? 0),
        TotalPassed: (int)(hit.TotalAchieved ?? 0),
        PassRate: hit.PercentAchieved ?? 0,
        TotalAchieved: null
    );

    /// <summary>
    /// Maps to the Meilisearch school document schema (snake_case field names, per-year metrics).
    /// The <c>id</c> field is the EMIS number, set as the index primary key by the upload script.
    /// </summary>
    private sealed class MeilisearchSchool
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("centre_name")]
        public string? CentreName { get; set; }

        [JsonPropertyName("province")]
        public string? Province { get; set; }

        [JsonPropertyName("district_name")]
        public string? DistrictName { get; set; }

        [JsonPropertyName("total_wrote_2024")]
        public double? TotalWrote { get; set; }

        [JsonPropertyName("total_achieved_2024")]
        public double? TotalAchieved { get; set; }

        [JsonPropertyName("percent_achieved_2024")]
        public double? PercentAchieved { get; set; }
    }
}