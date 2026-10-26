using System.Linq.Expressions;
using System.Text.Json.Serialization;
using MatricDasbhoard.Application.Features.Dashboard;
using MatricDasbhoard.Application.Features.Dashboard.Dtos;
using MatricDasbhoard.Domain.Entities;
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

    /// <summary>Largest page the subject school list serves in one request.</summary>
    private const int MaxSubjectSchoolsPageSize = 100;

    /// <summary>Default and fallback sort key for the subject school list.</summary>
    private const string SubjectPercentSort = "percentAchieved";

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
        var provinceFilter = string.IsNullOrWhiteSpace(province) ? null : province;
        var (scoped, scope, name) = ScopeSubjectRows(OfferedSubjectRows(), provinceFilter, district);

        var availableYears = await scoped
            .Select(p => p.Year)
            .Distinct()
            .OrderBy(y => y)
            .ToListAsync(cancellationToken);

        var selectedYear = year ?? (availableYears.Count > 0 ? (int?)availableYears[^1] : null);
        if (selectedYear is null)
        {
            return new RegionSubjectsOutput(scope, name, provinceFilter, null, availableYears, 0, []);
        }

        var inYear = scoped.Where(p => p.Year == selectedYear);
        var schoolCount = await inYear.Select(p => p.SchoolId).Distinct().CountAsync(cancellationToken);
        var totals = await AggregateSubjectRows(inYear, p => new
        {
            Code = p.Subject == null ? string.Empty : p.Subject.Code,
            Name = p.Subject == null ? string.Empty : p.Subject.Name
        })
            .ToListAsync(cancellationToken);

        var subjects = totals
            .Select(t =>
            {
                var aggregate = Summarize(t);
                return new RegionSubjectOutput(
                    t.Key.Code,
                    t.Key.Name,
                    aggregate.TotalWrote,
                    aggregate.Achieved,
                    aggregate.PercentAchieved,
                    aggregate.SchoolCount);
            })
            .OrderBy(s => s.Name)
            .ToList();

        return new RegionSubjectsOutput(
            scope,
            name,
            provinceFilter,
            selectedYear,
            availableYears,
            schoolCount,
            subjects);
    }

    /// <inheritdoc />
    public async Task<MatricDasbhoard.Shared.Result<SubjectDetailOutput>> GetSubjectDetailAsync(
        string code,
        string? province,
        string? district,
        int? year = null,
        CancellationToken cancellationToken = default)
    {
        var subject = await FindSubjectAsync(code, cancellationToken);
        if (subject is null)
        {
            return MatricDasbhoard.Shared.Result<SubjectDetailOutput>.Failure(
                ErrorMessages.Dashboard.SubjectNotFound, ErrorType.NotFound);
        }

        var provinceFilter = string.IsNullOrWhiteSpace(province) ? null : province;
        var districtFilter = string.IsNullOrWhiteSpace(district) ? null : district;
        var national = OfferedSubjectRows().Where(p => p.SubjectId == subject.Id);
        var (scoped, scope, scopeName) = ScopeSubjectRows(national, provinceFilter, districtFilter);

        var trend = (await AggregateSubjectRows(scoped, p => p.Year).ToListAsync(cancellationToken))
            .OrderBy(t => t.Key)
            .Select(t =>
            {
                var aggregate = Summarize(t);
                return new SubjectYearOutput(
                    t.Key,
                    aggregate.SchoolCount,
                    aggregate.TotalWrote,
                    aggregate.Achieved,
                    aggregate.PercentAchieved);
            })
            .ToList();

        var availableYears = trend.Select(t => t.Year).ToList();
        var selectedYear = year ?? (availableYears.Count > 0 ? (int?)availableYears[^1] : null);
        if (selectedYear is null)
        {
            return MatricDasbhoard.Shared.Result<SubjectDetailOutput>.Success(new SubjectDetailOutput(
                subject.Code, subject.Name, subject.IsGateway, scope, scopeName, provinceFilter, districtFilter,
                null, availableYears, 0, 0, null, null, trend, [], []));
        }

        // Province comparison is always national so a scoped view can still be
        // placed against every province for the same year.
        var inYear = national.Where(p => p.Year == selectedYear);
        var provinces = await AggregateRegionsAsync(inYear, p => p.School!.Province, cancellationToken);
        var districts = provinceFilter is null
            ? []
            : await AggregateRegionsAsync(
                inYear.Where(p => p.School!.Province == provinceFilter),
                p => p.School!.DistrictName,
                cancellationToken);

        var selected = trend.FirstOrDefault(t => t.Year == selectedYear);

        return MatricDasbhoard.Shared.Result<SubjectDetailOutput>.Success(new SubjectDetailOutput(
            subject.Code,
            subject.Name,
            subject.IsGateway,
            scope,
            scopeName,
            provinceFilter,
            districtFilter,
            selectedYear,
            availableYears,
            selected?.SchoolCount ?? 0,
            selected?.TotalWrote ?? 0,
            selected?.Achieved,
            selected?.PercentAchieved,
            trend,
            provinces,
            districts));
    }

    /// <inheritdoc />
    public async Task<MatricDasbhoard.Shared.Result<SubjectSchoolListOutput>> GetSubjectSchoolsAsync(
        string code,
        SubjectSchoolsInput input,
        CancellationToken cancellationToken = default)
    {
        var subject = await FindSubjectAsync(code, cancellationToken);
        if (subject is null)
        {
            return MatricDasbhoard.Shared.Result<SubjectSchoolListOutput>.Failure(
                ErrorMessages.Dashboard.SubjectNotFound, ErrorType.NotFound);
        }

        var pageSize = Math.Clamp(input.PageSize, 1, MaxSubjectSchoolsPageSize);
        // Cap the page so the row offset always fits in an int; pages past the
        // end simply come back empty.
        var pageNumber = Math.Clamp(input.PageNumber, 1, int.MaxValue / pageSize);
        var (scoped, _, _) = ScopeSubjectRows(
            OfferedSubjectRows().Where(p => p.SubjectId == subject.Id), input.Province, input.District);

        var selectedYear = input.Year ?? await scoped.MaxAsync(p => (int?)p.Year, cancellationToken);
        if (selectedYear is null)
        {
            return MatricDasbhoard.Shared.Result<SubjectSchoolListOutput>.Success(
                new SubjectSchoolListOutput(null, [], 0, pageNumber, pageSize));
        }

        // A school has at most one row per subject and year (unique index), so
        // each row is one school. Percent falls back to the exact count when the
        // report published counts without a rate.
        var rows = scoped
            .Where(p => p.Year == selectedYear)
            .Select(p => new SubjectSchoolRow
            {
                Id = p.School!.EmisNumber,
                Name = p.School.CentreName,
                Province = p.School.Province,
                District = p.School.DistrictName,
                Quintile = p.School.Quintile,
                TotalWrote = p.TotalWrote,
                Achieved = p.Achieved,
                PercentAchieved = p.PercentAchieved ?? (decimal?)p.Achieved * 100m / p.TotalWrote
            });

        var term = string.IsNullOrWhiteSpace(input.Search) ? null : input.Search.Trim().ToLowerInvariant();
        if (term is not null)
        {
            // Contains binds the term as a parameter and matches it literally,
            // so LIKE wildcards in user input have no special meaning.
            rows = rows.Where(r => r.Name.ToLower().Contains(term) || r.Id.ToLower().Contains(term));
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        var key = NormalizeSubjectSchoolSort(input.Sort);
        var page = await OrderSubjectSchools(rows, key, IsSubjectSchoolSortDescending(key, input.Order))
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = page
            .Select(r => new SubjectSchoolOutput(
                r.Id,
                r.Name,
                r.Province,
                r.District,
                r.Quintile,
                r.TotalWrote,
                r.Achieved,
                r.PercentAchieved is null ? null : (double)Math.Round(r.PercentAchieved.Value, 1)))
            .ToList();

        return MatricDasbhoard.Shared.Result<SubjectSchoolListOutput>.Success(
            new SubjectSchoolListOutput(selectedYear, items, totalCount, pageNumber, pageSize));
    }

    /// <summary>Maps an untrusted sort parameter onto a supported column, defaulting to pass percentage.</summary>
    private static string NormalizeSubjectSchoolSort(string? sort) => sort?.Trim().ToLowerInvariant() switch
    {
        "name" => "name",
        "province" => "province",
        "totalwrote" => "totalWrote",
        "achieved" => "achieved",
        _ => SubjectPercentSort
    };

    /// <summary>Explicit asc/desc wins; otherwise metrics read highest-first and names ascending.</summary>
    private static bool IsSubjectSchoolSortDescending(string sort, string? order) =>
        order?.Trim().ToLowerInvariant() switch
        {
            "asc" => false,
            "desc" => true,
            _ => sort is not ("name" or "province")
        };

    /// <summary>
    /// Orders subject school rows. Missing metrics sort last in either direction,
    /// and stable tie-breakers keep pages from overlapping.
    /// </summary>
    private static IQueryable<SubjectSchoolRow> OrderSubjectSchools(
        IQueryable<SubjectSchoolRow> rows,
        string sort,
        bool descending)
    {
        var ordered = sort switch
        {
            "name" => descending ? rows.OrderByDescending(r => r.Name) : rows.OrderBy(r => r.Name),
            "province" => descending ? rows.OrderByDescending(r => r.Province) : rows.OrderBy(r => r.Province),
            "totalWrote" => descending ? rows.OrderByDescending(r => r.TotalWrote) : rows.OrderBy(r => r.TotalWrote),
            "achieved" => descending
                ? rows.OrderBy(r => r.Achieved == null).ThenByDescending(r => r.Achieved)
                : rows.OrderBy(r => r.Achieved == null).ThenBy(r => r.Achieved),
            _ => (descending
                    ? rows.OrderBy(r => r.PercentAchieved == null).ThenByDescending(r => r.PercentAchieved)
                    : rows.OrderBy(r => r.PercentAchieved == null).ThenBy(r => r.PercentAchieved))
                .ThenByDescending(r => r.TotalWrote)
        };

        return ordered.ThenBy(r => r.Name).ThenBy(r => r.Id);
    }

    /// <summary>Looks up a non-deleted subject by its stable code.</summary>
    private async Task<SubjectRef?> FindSubjectAsync(string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return await _dbContext.Subjects
            .AsNoTracking()
            .Where(s => s.Code == code && !s.IsDeleted)
            .Select(s => new SubjectRef(s.Id, s.Code, s.Name, s.IsGateway))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Subject rows that count as offered: learners wrote the subject and neither
    /// the row nor its school is soft-deleted.
    /// </summary>
    private IQueryable<SchoolSubjectPerformance> OfferedSubjectRows() =>
        _dbContext.SchoolSubjectPerformances
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.TotalWrote > 0 && p.School != null && !p.School.IsDeleted);

    /// <summary>
    /// Narrows subject rows to a province and/or district and names the resulting scope.
    /// </summary>
    private static (IQueryable<SchoolSubjectPerformance> Rows, string Scope, string Name) ScopeSubjectRows(
        IQueryable<SchoolSubjectPerformance> rows,
        string? province,
        string? district)
    {
        var scope = "country";
        var name = "South Africa";
        if (!string.IsNullOrWhiteSpace(province))
        {
            rows = rows.Where(p => p.School != null && p.School.Province == province);
            scope = "province";
            name = province;
        }

        if (!string.IsNullOrWhiteSpace(district))
        {
            rows = rows.Where(p => p.School != null && p.School.DistrictName == district);
            scope = "district";
            name = district;
        }

        return (rows, scope, name);
    }

    /// <summary>
    /// Groups subject rows in the database and returns the partial sums the
    /// aggregation rule needs. <see cref="Summarize{TKey}"/> turns them into results.
    /// </summary>
    private static IQueryable<SubjectTotals<TKey>> AggregateSubjectRows<TKey>(
        IQueryable<SchoolSubjectPerformance> rows,
        Expression<Func<SchoolSubjectPerformance, TKey>> key) =>
        rows.GroupBy(key).Select(g => new SubjectTotals<TKey>(
            g.Key,
            g.Select(p => p.SchoolId).Distinct().Count(),
            g.Sum(p => p.TotalWrote),
            g.Count(p => p.Achieved == null),
            g.Sum(p => p.Achieved ?? 0),
            g.Count(p => p.Achieved == null && p.PercentAchieved == null),
            g.Sum(p => p.Achieved != null
                ? (decimal)p.Achieved.Value
                : (p.PercentAchieved ?? 0m) * p.TotalWrote / 100m)));

    /// <summary>
    /// Applies the subject aggregation rule. Historical reports can provide rates
    /// without exact achievement counts, so rates are weighted by cohort size;
    /// unknown results are never interpreted as failures. The achieved count is
    /// only known when every row has one, and the percentage only when every row
    /// has either a count or a rate.
    /// </summary>
    private static SubjectAggregate Summarize<TKey>(SubjectTotals<TKey> totals)
    {
        var achieved = totals.MissingAchieved == 0 ? totals.AchievedSum : (int?)null;
        var percent = totals.MissingResults == 0 && totals.TotalWrote > 0
            ? (double?)Math.Round(totals.WeightedAchieved / totals.TotalWrote * 100m, 1)
            : null;
        return new SubjectAggregate(totals.SchoolCount, totals.TotalWrote, achieved, percent);
    }

    /// <summary>Aggregates subject rows per region name, ordered by name.</summary>
    private static async Task<IReadOnlyList<SubjectRegionOutput>> AggregateRegionsAsync(
        IQueryable<SchoolSubjectPerformance> rows,
        Expression<Func<SchoolSubjectPerformance, string>> region,
        CancellationToken cancellationToken) =>
        (await AggregateSubjectRows(rows, region).ToListAsync(cancellationToken))
            .Select(t =>
            {
                var aggregate = Summarize(t);
                return new SubjectRegionOutput(
                    t.Key,
                    aggregate.SchoolCount,
                    aggregate.TotalWrote,
                    aggregate.Achieved,
                    aggregate.PercentAchieved);
            })
            .OrderBy(r => r.Name)
            .ToList();

    /// <inheritdoc />
    public async Task<SchoolListOutput> GetSchoolsAsync(int pageNumber, int pageSize, string? search = null,
        string? sort = null, string? order = null, string? province = null)
    {
        var key = NormalizeSort(sort);
        var descending = IsDescending(key, order);
        var provinceFilter = string.IsNullOrWhiteSpace(province) ? null : province;

        var page = key == MapUpdatedSort
            ? await GetSchoolsByRecentUpdateAsync(pageNumber, pageSize, search, provinceFilter, descending)
            : await GetSchoolDirectoryPageAsync(pageNumber, pageSize, search, provinceFilter, key, descending);

        return await WithMapUpdatesAsync(page);
    }

    /// <summary>Sort key for the last saved map correction (accepted as "updated" too).</summary>
    private const string MapUpdatedSort = "mapUpdatedAt";

    /// <summary>Maps an untrusted sort parameter onto a supported column, defaulting to name.</summary>
    private static string? NormalizeSort(string? sort) => sort?.Trim().ToLowerInvariant() switch
    {
        null or "" => null,
        "name" => "name",
        "province" => "province",
        "wrote" or "totalwrote" => "totalWrote",
        "passed" or "totalpassed" => "totalPassed",
        "passrate" => "passRate",
        "updated" or "mapupdatedat" => MapUpdatedSort,
        _ => null
    };

    /// <summary>
    /// Metric columns read best highest-first; names and provinces ascending.
    /// The frontend always sends an explicit order, so this only shapes direct API calls.
    /// </summary>
    private static bool IsDescending(string? sort, string? order)
    {
        if (!string.IsNullOrWhiteSpace(order))
        {
            return string.Equals(order.Trim(), "desc", StringComparison.OrdinalIgnoreCase);
        }

        return sort is MapUpdatedSort or "totalWrote" or "totalPassed" or "passRate";
    }

    /// <summary>
    /// Directory page. Without an explicit sort or province filter, free-text
    /// search prefers the Meilisearch relevance index and falls back to the
    /// database when it is unreachable. The index ranks identifiers only, so a
    /// province filter is applied in the database to keep counts and pages exact.
    /// </summary>
    private async Task<SchoolListOutput> GetSchoolDirectoryPageAsync(int pageNumber, int pageSize, string? search,
        string? province, string? sort, bool descending)
    {
        var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (term is not null && sort is null && province is null)
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

        if (province is not null)
        {
            query = query.Where(s => s.Province == province);
        }

        var totalCount = await query.CountAsync();
        var latestYear = await GetLatestYearAsync(CancellationToken.None);

        // Left join onto the single latest-year performance row instead of three
        // correlated subqueries per school. The school directory pages through
        // every row, so per-row subqueries multiply badly.
        var items = await CurrentSchoolOutputs(query, latestYear, sort, descending)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new SchoolListOutput(items, totalCount, pageNumber, pageSize);
    }

    /// <summary>
    /// Orders the directory by the last saved map correction. The ordering
    /// joins a PostGIS-owned table, so it is issued as SQL rather than mapped
    /// into the EF model. Meilisearch relevance ranking is skipped here because
    /// it cannot preserve a global timestamp order across pages.
    /// </summary>
    private async Task<SchoolListOutput> GetSchoolsByRecentUpdateAsync(int pageNumber, int pageSize, string? search,
        string? province, bool descending)
    {
        var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        // Component tests use the in-memory provider, which cannot run the
        // PostGIS ordering join; keep them on the directory order.
        if (!_dbContext.Database.IsNpgsql())
        {
            return await GetSchoolDirectoryPageAsync(pageNumber, pageSize, term, province, null, false);
        }

        var totalCount = await _dbContext.Database.SqlQuery<int>($"""
            SELECT count(*)::int AS "Value"
            FROM marks."Schools" s
            WHERE NOT s."IsDeleted"
              AND ({province}::text IS NULL OR s."Province" = {province}::text)
              AND ({term}::text IS NULL
                OR strpos(lower(public.unaccent('public.unaccent'::regdictionary, s."CentreName")),
                     lower(public.unaccent('public.unaccent'::regdictionary, {term}::text))) > 0
                OR strpos(lower(public.unaccent('public.unaccent'::regdictionary, s."EmisNumber")),
                     lower(public.unaccent('public.unaccent'::regdictionary, {term}::text))) > 0)
            """).SingleAsync();

        var ids = await _dbContext.Database.SqlQuery<string>($"""
            SELECT s."EmisNumber" AS "Value"
            FROM marks."Schools" s
            LEFT JOIN geo.school_spatial_profiles p ON p.school_id = s."Id"
            WHERE NOT s."IsDeleted"
              AND ({province}::text IS NULL OR s."Province" = {province}::text)
              AND ({term}::text IS NULL
                OR strpos(lower(public.unaccent('public.unaccent'::regdictionary, s."CentreName")),
                     lower(public.unaccent('public.unaccent'::regdictionary, {term}::text))) > 0
                OR strpos(lower(public.unaccent('public.unaccent'::regdictionary, s."EmisNumber")),
                     lower(public.unaccent('public.unaccent'::regdictionary, {term}::text))) > 0)
            ORDER BY CASE WHEN {descending} THEN p.changed_at END DESC NULLS LAST,
                     CASE WHEN NOT {descending} THEN p.changed_at END ASC NULLS LAST,
                     s."CentreName", s."EmisNumber"
            LIMIT {pageSize} OFFSET {(pageNumber - 1) * pageSize}
            """).ToListAsync();

        if (ids.Count == 0)
        {
            return new SchoolListOutput([], totalCount, pageNumber, pageSize);
        }

        var latestYear = await GetLatestYearAsync(CancellationToken.None);
        var matching = _dbContext.Schools.AsNoTracking()
            .Where(s => !s.IsDeleted && ids.Contains(s.EmisNumber));
        var byId = (await CurrentSchoolOutputs(matching, latestYear).ToListAsync())
            .ToDictionary(s => s.Id);
        var items = ids.Where(byId.ContainsKey).Select(id => byId[id]).ToList();

        return new SchoolListOutput(items, totalCount, pageNumber, pageSize);
    }

    /// <summary>
    /// Adds the last map-correction timestamp to a page of schools without
    /// mapping the PostGIS profile table into the EF model.
    /// </summary>
    private async Task<SchoolListOutput> WithMapUpdatesAsync(SchoolListOutput page)
    {
        if (page.Items.Count == 0 || !_dbContext.Database.IsNpgsql())
        {
            return page;
        }

        var emisNumbers = page.Items.Select(i => i.Id).ToArray();
        var updates = await _dbContext.Database.SqlQuery<MapUpdateRow>($"""
            SELECT s."EmisNumber" AS "EmisNumber", p.changed_at AS "ChangedAt"
            FROM geo.school_spatial_profiles p
            JOIN marks."Schools" s ON s."Id" = p.school_id
            WHERE s."EmisNumber" = ANY({emisNumbers})
            """).ToListAsync();

        if (updates.Count == 0)
        {
            return page;
        }

        var changedAt = updates.ToDictionary(u => u.EmisNumber, u => u.ChangedAt);
        return page with
        {
            Items = page.Items
                .Select(i => changedAt.TryGetValue(i.Id, out var value) ? i with { MapUpdatedAt = value } : i)
                .ToList()
        };
    }

    /// <summary>Projects current-year data for browsing and ranked search hits.</summary>
    private IQueryable<SchoolOutput> CurrentSchoolOutputs(IQueryable<School> query, int year, string? sort = null,
        bool descending = false)
    {
        var performances = _dbContext.SchoolPerformances
            .Where(p => p.Year == year && !p.IsDeleted);

        // Order while the query still refers to mapped columns; EF cannot
        // translate ordering by members of a positional record projection.
        var rows = from s in query
                   join p in performances on s.Id equals p.SchoolId into schoolPerformances
                   from performance in schoolPerformances.DefaultIfEmpty()
                   select new { School = s, Performance = performance };

        var ordered = sort switch
        {
            "province" => descending
                ? rows.OrderByDescending(r => r.School.Province)
                : rows.OrderBy(r => r.School.Province),
            "totalWrote" => descending
                ? rows.OrderByDescending(r => r.Performance == null ? 0 : r.Performance.TotalWrote)
                : rows.OrderBy(r => r.Performance == null ? 0 : r.Performance.TotalWrote),
            "totalPassed" => descending
                ? rows.OrderByDescending(r => r.Performance == null ? 0 : r.Performance.TotalAchieved)
                : rows.OrderBy(r => r.Performance == null ? 0 : r.Performance.TotalAchieved),
            "passRate" => descending
                ? rows.OrderByDescending(r => r.Performance == null ? 0 : (double)r.Performance.PercentAchieved)
                : rows.OrderBy(r => r.Performance == null ? 0 : (double)r.Performance.PercentAchieved),
            _ => descending
                ? rows.OrderByDescending(r => r.School.CentreName)
                : rows.OrderBy(r => r.School.CentreName)
        };

        // Stable tie-breakers keep pages from overlapping when values repeat.
        return ordered.ThenBy(r => r.School.CentreName).ThenBy(r => r.School.EmisNumber)
            .Select(r => new SchoolOutput(
                r.School.EmisNumber,
                r.School.CentreName,
                r.School.Province,
                r.School.DistrictName,
                r.Performance == null ? 0 : r.Performance.TotalWrote,
                r.Performance == null ? 0 : r.Performance.TotalAchieved,
                r.Performance == null ? 0 : (double)r.Performance.PercentAchieved,
                null));
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

            // The index ranks identifiers only. Its report-year metrics and school
            // metadata may be stale, so hydrate hits from the serving database.
            var ids = result.Hits.Select(hit => hit.Id ?? string.Empty).Distinct().ToList();
            var latestYear = await GetLatestYearAsync(CancellationToken.None);
            var matching = _dbContext.Schools.AsNoTracking()
                .Where(s => !s.IsDeleted && ids.Contains(s.EmisNumber));
            var current = await CurrentSchoolOutputs(matching, latestYear).ToListAsync();
            var byId = current.ToDictionary(s => s.Id);
            var schools = ids.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
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

    /// <summary>
    /// Reads the stable identifier from a ranked Meilisearch school document.
    /// </summary>
    private sealed class MeilisearchSchool
    {
        [JsonPropertyName("school_id")]
        public string? Id { get; set; }
    }

    /// <summary>Identity and display fields of a subject resolved from its code.</summary>
    private sealed record SubjectRef(Guid Id, string Code, string Name, bool IsGateway);

    /// <summary>Database-side partial sums for one group of subject rows.</summary>
    /// <param name="Key">Group key (year, region name, or subject).</param>
    /// <param name="SchoolCount">Distinct schools in the group.</param>
    /// <param name="TotalWrote">Learners who wrote across the group.</param>
    /// <param name="MissingAchieved">Rows without an exact achieved count.</param>
    /// <param name="AchievedSum">Sum of known achieved counts.</param>
    /// <param name="MissingResults">Rows with neither an achieved count nor a rate.</param>
    /// <param name="WeightedAchieved">Achieved counts, or rates weighted by cohort size when counts are missing.</param>
    private sealed record SubjectTotals<TKey>(
        TKey Key,
        int SchoolCount,
        int TotalWrote,
        int MissingAchieved,
        int AchievedSum,
        int MissingResults,
        decimal WeightedAchieved);

    /// <summary>Final aggregate for a group of subject rows.</summary>
    private readonly record struct SubjectAggregate(
        int SchoolCount,
        int TotalWrote,
        int? Achieved,
        double? PercentAchieved);

    /// <summary>Row shape for one school's result in the subject school list.</summary>
    private sealed class SubjectSchoolRow
    {
        /// <summary>School EMIS number.</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>School name.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>School province.</summary>
        public string Province { get; init; } = string.Empty;

        /// <summary>School education district.</summary>
        public string District { get; init; } = string.Empty;

        /// <summary>School quintile.</summary>
        public int Quintile { get; init; }

        /// <summary>Learners who wrote the subject.</summary>
        public int TotalWrote { get; init; }

        /// <summary>Learners who achieved the subject, null when unavailable.</summary>
        public int? Achieved { get; init; }

        /// <summary>Unrounded pass percentage, null when unavailable.</summary>
        public decimal? PercentAchieved { get; init; }
    }

    /// <summary>Row shape for the map-correction timestamp lookup.</summary>
    private sealed class MapUpdateRow
    {
        public string EmisNumber { get; set; } = string.Empty;
        public DateTimeOffset ChangedAt { get; set; }
    }
}
