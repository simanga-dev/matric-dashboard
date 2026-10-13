using System.Text.Json.Serialization;
using MatricDasbhoard.Application.Features.Dashboard;
using MatricDasbhoard.Application.Features.Dashboard.Dtos;
using MatricDasbhoard.Infrastructure.Persistence;
using MatricDasbhoard.Shared;
using Meilisearch;
using Microsoft.EntityFrameworkCore;

namespace MatricDasbhoard.Infrastructure.Features.Dashboard.Services;

/// <summary>
/// Returns dashboard statistics and school data. Browsing and by-id lookups are
/// served from PostgreSQL (the <c>marks</c> schema) via EF Core; free-text search
/// is served from the Meilisearch "schools" index, falling back to a case and
/// accent insensitive database match when the index is unavailable.
/// </summary>
internal sealed class DashboardService : IDashboardService
{
    /// <summary>The performance year surfaced by the dashboard.</summary>
    private const int LatestYear = 2024;

    /// <summary>Name of the Meilisearch index populated by scripts/upload_to_meilisearch.py.</summary>
    private const string SchoolsIndex = "schools";

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

    private readonly MatricDasbhoardDbContext _dbContext;
    private readonly MeilisearchClient _meilisearchClient;

    /// <summary>Creates a dashboard service backed by PostgreSQL and Meilisearch.</summary>
    public DashboardService(MatricDasbhoardDbContext dbContext, MeilisearchClient meilisearchClient)
    {
        _dbContext = dbContext;
        _meilisearchClient = meilisearchClient;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public async Task<RegionStatsOutput> GetRegionStatsAsync(
        string? province,
        string? district,
        CancellationToken cancellationToken = default)
    {
        var performances = _dbContext.SchoolPerformances
            .Where(p => !p.IsDeleted && p.Year == LatestYear && !p.School.IsDeleted);

        var scope = "country";
        var name = "South Africa";
        if (!string.IsNullOrWhiteSpace(province))
        {
            performances = performances.Where(p => p.School.Province == province);
            scope = "province";
            name = province;
        }

        if (!string.IsNullOrWhiteSpace(district))
        {
            performances = performances.Where(p => p.School.DistrictName == district);
            scope = "district";
            name = district;
        }

        var schoolCount = await performances.Select(p => p.SchoolId).Distinct().CountAsync(cancellationToken);
        var totalWrote = await performances.SumAsync(p => (long)p.TotalWrote, cancellationToken);
        var totalPassed = await performances.SumAsync(p => (long)p.TotalAchieved, cancellationToken);
        var passRate = totalWrote > 0 ? Math.Round((double)totalPassed / totalWrote * 100, 1) : 0;

        return new RegionStatsOutput(
            scope,
            name,
            string.IsNullOrWhiteSpace(province) ? null : province,
            LatestYear,
            schoolCount,
            (int)totalWrote,
            (int)totalPassed,
            passRate);
    }

    /// <inheritdoc />
    public async Task<SchoolListOutput> GetSchoolsAsync(int pageNumber, int pageSize, string? search = null)
    {
        var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (term is not null)
        {
            var fromIndex = await SearchSchoolsAsync(term, pageNumber, pageSize);
            if (fromIndex is not null)
            {
                return fromIndex;
            }
        }

        var query = _dbContext.Schools
            .AsNoTracking()
            .Where(s => !s.IsDeleted);

        if (term is not null)
        {
            // Normalize both operands in PostgreSQL, including decomposed accents.
            // strpos treats user input literally, including SQL LIKE wildcards.
            query = _dbContext.Schools.FromSqlInterpolated($"""
                SELECT * FROM marks."Schools"
                WHERE strpos(lower(public.unaccent('public.unaccent'::regdictionary, "CentreName")),
                             lower(public.unaccent('public.unaccent'::regdictionary, {term}))) > 0
                   OR strpos(lower(public.unaccent('public.unaccent'::regdictionary, "EmisNumber")),
                             lower(public.unaccent('public.unaccent'::regdictionary, {term}))) > 0
                """).AsNoTracking().Where(s => !s.IsDeleted);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(s => s.CentreName)
            .ThenBy(s => s.EmisNumber)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SchoolOutput(
                s.EmisNumber,
                s.CentreName,
                s.Province,
                s.DistrictName,
                s.Performances.Where(p => p.Year == LatestYear && !p.IsDeleted)
                    .Select(p => p.TotalWrote).FirstOrDefault(),
                s.Performances.Where(p => p.Year == LatestYear && !p.IsDeleted)
                    .Select(p => p.TotalAchieved).FirstOrDefault(),
                (double)s.Performances.Where(p => p.Year == LatestYear && !p.IsDeleted)
                    .Select(p => p.PercentAchieved).FirstOrDefault(),
                null))
            .ToListAsync();

        return new SchoolListOutput(items, totalCount, pageNumber, pageSize);
    }

    /// <summary>
    /// Free-text search against the Meilisearch "schools" index. Returns
    /// <c>null</c> when the index is unreachable so the caller can fall back to
    /// the database.
    /// </summary>
    private async Task<SchoolListOutput?> SearchSchoolsAsync(string term, int pageNumber, int pageSize)
    {
        try
        {
            var index = _meilisearchClient.Index(SchoolsIndex);
            var result = await index.SearchAsync<MeilisearchSchool>(
                term,
                new SearchQuery
                {
                    Limit = pageSize,
                    Offset = (pageNumber - 1) * pageSize
                }) as SearchResult<MeilisearchSchool>;

            if (result is null)
            {
                return null;
            }

            var schools = result.Hits.Select(ToSchoolOutput).ToList();
            return new SchoolListOutput(schools, result.EstimatedTotalHits, pageNumber, pageSize);
        }
        catch (MeilisearchApiError)
        {
            return null;
        }
        catch (MeilisearchCommunicationError)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<MatricDasbhoard.Shared.Result<SchoolDetailOutput>> GetSchoolByIdAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return MatricDasbhoard.Shared.Result<SchoolDetailOutput>.Failure(
                ErrorMessages.Dashboard.SchoolNotFound, ErrorType.NotFound);
        }

        var school = await _dbContext.Schools
            .AsNoTracking()
            .Where(s => s.EmisNumber == id && !s.IsDeleted)
            .Select(s => new
            {
                s.EmisNumber,
                s.CentreName,
                s.Province,
                s.DistrictName,
                s.CentreNumber,
                s.Quintile,
                Performances = s.Performances
                    .Where(p => !p.IsDeleted)
                    .OrderBy(p => p.Year)
                    .Select(p => new
                    {
                        p.Year,
                        p.ProgressedNumber,
                        p.TotalWrote,
                        p.TotalAchieved,
                        p.PercentAchieved
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (school is null)
        {
            return MatricDasbhoard.Shared.Result<SchoolDetailOutput>.Failure(
                ErrorMessages.Dashboard.SchoolNotFound, ErrorType.NotFound);
        }

        var years = school.Performances
            .Select(p => new SchoolYearOutput(
                p.Year,
                p.ProgressedNumber,
                p.TotalWrote,
                p.TotalAchieved,
                (double)p.PercentAchieved))
            .ToList();

        var latest = years.LastOrDefault(y => y.Year == LatestYear) ?? years.LastOrDefault();

        var comparison = await GetComparisonAsync(
            school.Province, latest?.Year ?? LatestYear, latest?.PassRate, cancellationToken);

        var detail = new SchoolDetailOutput(
            school.EmisNumber,
            school.CentreName,
            school.Province,
            school.DistrictName,
            school.CentreNumber,
            school.Quintile,
            latest?.TotalWrote ?? 0,
            latest?.TotalPassed ?? 0,
            latest?.PassRate ?? 0,
            years,
            comparison);

        return MatricDasbhoard.Shared.Result<SchoolDetailOutput>.Success(detail);
    }

    /// <summary>
    /// Builds province and national context for a school using learner-weighted
    /// pass rates for the selected year (total passed / total wrote).
    /// </summary>
    private async Task<SchoolComparisonOutput> GetComparisonAsync(
        string province,
        int year,
        double? schoolPassRate,
        CancellationToken cancellationToken)
    {
        var provinceQuery = _dbContext.SchoolPerformances
            .Where(p => !p.IsDeleted &&
                        p.Year == year &&
                        !p.School.IsDeleted &&
                        p.School.Province == province);

        var provinceCount = await provinceQuery.CountAsync(cancellationToken);
        var provinceWrote = await provinceQuery.SumAsync(p => (long)p.TotalWrote, cancellationToken);
        var provincePassed = await provinceQuery.SumAsync(p => (long)p.TotalAchieved, cancellationToken);

        var nationalQuery = _dbContext.SchoolPerformances
            .Where(p => !p.IsDeleted && p.Year == year && !p.School.IsDeleted);

        var nationalWrote = await nationalQuery.SumAsync(p => (long)p.TotalWrote, cancellationToken);
        var nationalPassed = await nationalQuery.SumAsync(p => (long)p.TotalAchieved, cancellationToken);

        var threshold = (decimal)(schoolPassRate ?? 0);
        var higher = provinceCount > 0 && schoolPassRate.HasValue
            ? await provinceQuery.CountAsync(p => p.PercentAchieved > threshold, cancellationToken)
            : 0;

        var rank = provinceCount == 0 || !schoolPassRate.HasValue
            ? 0
            : Math.Min(1 + higher, provinceCount);

        return new SchoolComparisonOutput(
            rank,
            provinceCount,
            provinceWrote > 0 ? Math.Round((double)provincePassed / provinceWrote * 100, 1) : null,
            nationalWrote > 0 ? Math.Round((double)nationalPassed / nationalWrote * 100, 1) : null,
            year);
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
    /// Maps a Meilisearch school document (snake_case field names, per-year
    /// metrics). The <c>school_id</c> field is the index primary key.
    /// </summary>
    private sealed class MeilisearchSchool
    {
        [JsonPropertyName("school_id")]
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
