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
    public async Task<DashboardStatsOutput> GetStatsAsync()
    {
        var latestYear = await GetLatestYearAsync(CancellationToken.None);
        var (schools, learners, top, rate) = await AggregateYearAsync(latestYear, CancellationToken.None);
        var (prevSchools, prevLearners, prevTop, prevRate) = latestYear > 0
            ? await AggregateYearAsync(latestYear - 1, CancellationToken.None)
            : (0, 0, 0, 0.0);

        return new DashboardStatsOutput(
            latestYear,
            new StatItemOutput(top, PercentChange(top, prevTop)),
            new StatItemOutput(schools, PercentChange(schools, prevSchools)),
            new StatItemOutput(learners, PercentChange(learners, prevLearners)),
            new StatItemOutput((int)Math.Round(rate * 10), PercentChange(rate, prevRate)));
    }

    /// <summary>
    /// Latest academic year with any performance data, or zero when the
    /// database has no results yet.
    /// </summary>
    private async Task<int> GetLatestYearAsync(CancellationToken cancellationToken)
    {
        var latest = await _dbContext.SchoolPerformances
            .Where(p => !p.IsDeleted)
            .MaxAsync(p => (int?)p.Year, cancellationToken);
        return latest ?? 0;
    }

    /// <summary>
    /// Aggregate a year into school count, learners, 100% schools, and weighted
    /// pass rate. Used to derive both the current values and their year-on-year trends.
    /// </summary>
    private async Task<(int Schools, int Learners, int Top, double Rate)> AggregateYearAsync(
        int year,
        CancellationToken cancellationToken)
    {
        var rows = await _dbContext.SchoolPerformances
            .Where(p => !p.IsDeleted && p.Year == year && !p.School.IsDeleted)
            .Select(p => new { p.SchoolId, p.TotalWrote, p.TotalAchieved })
            .ToListAsync(cancellationToken);

        var schools = rows.Select(r => r.SchoolId).Distinct().Count();
        var learners = rows.Sum(r => r.TotalWrote);
        var passed = rows.Sum(r => r.TotalAchieved);
        var top = rows.Count(r => r.TotalWrote > 0 && r.TotalAchieved == r.TotalWrote);
        var rate = learners > 0 ? (double)passed / learners * 100 : 0;
        return (schools, learners, top, rate);
    }

    /// <summary>Percentage change from previous to current, or null when there is no base.</summary>
    private static double? PercentChange(double current, double previous) =>
        previous > 0 ? Math.Round((current - previous) / previous * 100, 1) : null;

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
        var latestYear = await GetLatestYearAsync(cancellationToken);
        var scoped = _dbContext.SchoolPerformances
            .Where(p => !p.IsDeleted && !p.School.IsDeleted);

        var scope = "country";
        var name = "South Africa";
        if (!string.IsNullOrWhiteSpace(province))
        {
            scoped = scoped.Where(p => p.School.Province == province);
            scope = "province";
            name = province;
        }

        if (!string.IsNullOrWhiteSpace(district))
        {
            scoped = scoped.Where(p => p.School.DistrictName == district);
            scope = "district";
            name = district;
        }

        var performances = scoped.Where(p => p.Year == latestYear);
        var schoolCount = await performances.Select(p => p.SchoolId).Distinct().CountAsync(cancellationToken);
        var totalWrote = await performances.SumAsync(p => (long)p.TotalWrote, cancellationToken);
        var totalPassed = await performances.SumAsync(p => (long)p.TotalAchieved, cancellationToken);
        var passRate = totalWrote > 0 ? Math.Round((double)totalPassed / totalWrote * 100, 1) : 0;

        // Learner-weighted pass rate per year across the scope, oldest first.
        var trendRows = await scoped
            .GroupBy(p => p.Year)
            .Select(g => new
            {
                Year = g.Key,
                TotalWrote = g.Sum(p => (long)p.TotalWrote),
                TotalPassed = g.Sum(p => (long)p.TotalAchieved)
            })
            .OrderBy(r => r.Year)
            .ToListAsync(cancellationToken);

        var years = trendRows
            .Select(r => new RegionYearOutput(
                r.Year,
                (int)r.TotalWrote,
                (int)r.TotalPassed,
                r.TotalWrote > 0 ? Math.Round((double)r.TotalPassed / r.TotalWrote * 100, 1) : 0))
            .ToList();

        return new RegionStatsOutput(
            scope,
            name,
            string.IsNullOrWhiteSpace(province) ? null : province,
            latestYear,
            schoolCount,
            (int)totalWrote,
            (int)totalPassed,
            passRate,
            years);
    }

    /// <inheritdoc />
    public async Task<RegionSubjectsOutput> GetRegionSubjectsAsync(
        string? province,
        string? district,
        int? year = null,
        CancellationToken cancellationToken = default)
    {
        // Only subjects with learners who wrote count as offered.
        var scoped = _dbContext.SchoolSubjectPerformances
            .Where(p => !p.IsDeleted && p.TotalWrote > 0 && p.School != null && !p.School.IsDeleted);

        var scope = "country";
        var name = "South Africa";
        if (!string.IsNullOrWhiteSpace(province))
        {
            scoped = scoped.Where(p => p.School != null && p.School.Province == province);
            scope = "province";
            name = province;
        }

        if (!string.IsNullOrWhiteSpace(district))
        {
            scoped = scoped.Where(p => p.School != null && p.School.DistrictName == district);
            scope = "district";
            name = district;
        }

        var availableYears = await scoped
            .Select(p => p.Year)
            .Distinct()
            .OrderBy(y => y)
            .ToListAsync(cancellationToken);

        var selectedYear = year ?? (availableYears.Count > 0 ? (int?)availableYears[^1] : null);
        if (selectedYear is null)
        {
            return new RegionSubjectsOutput(scope, name, province, null, availableYears, 0, []);
        }

        var rows = await scoped
            .Where(p => p.Year == selectedYear)
            .Select(p => new
            {
                p.SchoolId,
                Code = p.Subject == null ? string.Empty : p.Subject.Code,
                SubjectName = p.Subject == null ? string.Empty : p.Subject.Name,
                p.TotalWrote,
                p.Achieved
            })
            .ToListAsync(cancellationToken);

        var schoolCount = rows.Select(r => r.SchoolId).Distinct().Count();

        // Sum learners and achievement per subject; the pass percentage is
        // recomputed from the totals so it stays learner-weighted when a
        // subject's per-school percentages are missing.
        var subjects = rows
            .GroupBy(r => new { r.Code, r.SubjectName })
            .Select(g =>
            {
                var wrote = g.Sum(r => r.TotalWrote);
                var achieved = g.Any(r => r.Achieved.HasValue)
                    ? g.Sum(r => r.Achieved ?? 0)
                    : (int?)null;
                var percent = achieved is not null && wrote > 0
                    ? (double?)Math.Round((double)achieved.Value / wrote * 100, 1)
                    : null;
                return new RegionSubjectOutput(
                    g.Key.Code,
                    g.Key.SubjectName,
                    wrote,
                    achieved,
                    percent,
                    g.Select(r => r.SchoolId).Distinct().Count());
            })
            .OrderBy(s => s.Name)
            .ToList();

        return new RegionSubjectsOutput(
            scope,
            name,
            string.IsNullOrWhiteSpace(province) ? null : province,
            selectedYear,
            availableYears,
            schoolCount,
            subjects);
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
        var latestYear = await GetLatestYearAsync(CancellationToken.None);

        // Left join onto the single latest-year performance row instead of three
        // correlated subqueries per school. The school directory pages through
        // every row, so per-row subqueries multiply badly.
        var performances = _dbContext.SchoolPerformances
            .Where(p => p.Year == latestYear && !p.IsDeleted);

        var items = await (from s in query
                           join p in performances on s.Id equals p.SchoolId into schoolPerformances
                           from performance in schoolPerformances.DefaultIfEmpty()
                           orderby s.CentreName, s.EmisNumber
                           select new SchoolOutput(
                               s.EmisNumber,
                               s.CentreName,
                               s.Province,
                               s.DistrictName,
                               performance == null ? 0 : performance.TotalWrote,
                               performance == null ? 0 : performance.TotalAchieved,
                               performance == null ? 0 : (double)performance.PercentAchieved,
                               null))
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
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

        var latestYear = await GetLatestYearAsync(cancellationToken);
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
                    .ToList(),
                SubjectPerformances = s.SubjectPerformances
                    .Where(p => !p.IsDeleted)
                    .Select(p => new
                    {
                        Code = p.Subject == null ? string.Empty : p.Subject.Code,
                        Name = p.Subject == null ? string.Empty : p.Subject.Name,
                        p.Year,
                        p.TotalWrote,
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

        // Performances are ordered oldest first, so the last record is the
        // school's most recent year with data.
        var latest = years.LastOrDefault();

        var comparison = await GetComparisonAsync(
            school.Province, latest?.Year ?? latestYear, latest?.PassRate, cancellationToken);

        // Only subjects with learners who wrote count as offered. Aggregate over
        // every year so subjects last offered in an earlier year stay visible,
        // each carrying its own most recent offered year.
        var offeredRows = school.SubjectPerformances
            .Where(p => p.TotalWrote > 0)
            .ToList();

        var subjectsOfferedYear = offeredRows.Count == 0
            ? (int?)null
            : offeredRows.Max(p => p.Year);

        var subjectsOffered = offeredRows.Count == 0
            ? null
            : offeredRows
                .GroupBy(p => new { p.Code, p.Name })
                .Select(g => g.OrderByDescending(p => p.Year).First())
                .OrderBy(p => p.Name)
                .Select(p => new OfferedSubjectOutput(
                    p.Code,
                    p.Name,
                    p.Year,
                    p.TotalWrote,
                    p.PercentAchieved is null ? null : (double?)p.PercentAchieved))
                .ToList();

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
            comparison,
            SubjectsOfferedYear: subjectsOfferedYear,
            SubjectsOffered: subjectsOffered);

        return MatricDasbhoard.Shared.Result<SchoolDetailOutput>.Success(detail);
    }

    /// <inheritdoc />
    public async Task<MatricDasbhoard.Shared.Result<SchoolSubjectsOutput>> GetSchoolSubjectsAsync(
        string id,
        int? year = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return MatricDasbhoard.Shared.Result<SchoolSubjectsOutput>.Failure(
                ErrorMessages.Dashboard.SchoolNotFound, ErrorType.NotFound);
        }

        var school = await _dbContext.Schools
            .AsNoTracking()
            .Where(s => s.EmisNumber == id && !s.IsDeleted)
            .Select(s => new
            {
                s.EmisNumber,
                SubjectPerformances = s.SubjectPerformances
                    .Where(p => !p.IsDeleted)
                    .Select(p => new
                    {
                        p.SubjectId,
                        Code = p.Subject == null ? string.Empty : p.Subject.Code,
                        Name = p.Subject == null ? string.Empty : p.Subject.Name,
                        p.Year,
                        p.TotalWrote,
                        p.Achieved,
                        p.PercentAchieved
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (school is null)
        {
            return MatricDasbhoard.Shared.Result<SchoolSubjectsOutput>.Failure(
                ErrorMessages.Dashboard.SchoolNotFound, ErrorType.NotFound);
        }

        var availableYears = school.SubjectPerformances
            .Select(p => p.Year)
            .Distinct()
            .OrderBy(y => y)
            .ToList();

        var selectedYear = year ?? (availableYears.Count > 0 ? (int?)availableYears[^1] : null);

        var subjects = selectedYear is null
            ? new List<SchoolSubjectOutput>()
            : school.SubjectPerformances
                .Where(p => p.Year == selectedYear)
                .OrderBy(p => p.Name)
                .Select(p => new SchoolSubjectOutput(
                    p.SubjectId.ToString(),
                    p.Code,
                    p.Name,
                    p.TotalWrote,
                    p.Achieved,
                    p.PercentAchieved is null ? null : (double?)p.PercentAchieved))
                .ToList();

        return MatricDasbhoard.Shared.Result<SchoolSubjectsOutput>.Success(
            new SchoolSubjectsOutput(
                school.EmisNumber,
                selectedYear,
                availableYears,
                subjects));
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
