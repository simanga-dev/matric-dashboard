using MatricDasbhoard.Component.Tests.Fixtures;
using MatricDasbhoard.Application.Features.Dashboard.Dtos;
using MatricDasbhoard.Domain.Entities;
using MatricDasbhoard.Domain.Enums;
using MatricDasbhoard.Infrastructure.Features.Dashboard.Services;
using MatricDasbhoard.Infrastructure.Persistence;
using MatricDasbhoard.Shared;
using Meilisearch;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MatricDasbhoard.Component.Tests.Services;

/// <summary>Regression coverage for database-backed dashboard data.</summary>
public class DashboardServiceTests
{
    /// <summary>Search ranks identifiers but uses the same database cohort and metadata as browsing.</summary>
    [Fact]
    public async Task Search_HydratesCurrentResultsAndPreservesRelevanceOrder()
    {
        await using var db = TestDbContextFactory.Create();
        var a = School("100", "Current A");
        a.AddPerformance(new SchoolPerformance(2024, 0, 10, 5, 50));
        a.AddPerformance(new SchoolPerformance(2025, 0, 20, 18, 90));
        var b = School("200", "Current B");
        b.AddPerformance(new SchoolPerformance(2024, 0, 10, 10, 100));
        var deleted = School("300", "Deleted");
        deleted.SoftDelete();
        db.Schools.AddRange(a, b, deleted);
        await db.SaveChangesAsync();
        using var http = new HttpClient(new RankedSearchHandler()) { BaseAddress = new Uri("http://search.test") };
        var service = new DashboardService(db, new MeilisearchClient(http));

        var browse = await service.GetSchoolsAsync(1, 10);
        var search = await service.GetSchoolsAsync(1, 10, "school");

        Assert.Equal(["200", "100"], search.Items.Select(s => s.Id));
        Assert.Equal(4, search.TotalCount);
        foreach (var hit in search.Items)
            Assert.Equal(browse.Items.Single(s => s.Id == hit.Id), hit);
        Assert.Equal(90, search.Items[1].PassRate);
        Assert.Equal("Current A", search.Items[1].Name);
        Assert.Equal(0, search.Items[0].TotalWrote);
    }

    /// <summary>Duplicate names must not cause overlapping or unstable pages.</summary>
    [Fact]
    public async Task Browse_OrdersDuplicateNamesByEmisNumber()
    {
        await using var db = TestDbContextFactory.Create();
        db.Schools.AddRange(School("300", "Same"), School("100", "Same"), School("200", "Same"));
        await db.SaveChangesAsync();
        using var http = UnreachableSearch();
        var service = new DashboardService(db, new MeilisearchClient(http));

        var first = await service.GetSchoolsAsync(1, 2);
        var second = await service.GetSchoolsAsync(2, 2);

        Assert.Equal(["100", "200"], first.Items.Select(s => s.Id));
        Assert.Equal("300", Assert.Single(second.Items).Id);
        Assert.Equal(3, first.TotalCount);
    }

    /// <summary>Requested column order applies before paging, with stable tie-breakers.</summary>
    [Fact]
    public async Task Browse_SortsByRequestedColumnAndDirection()
    {
        await using var db = TestDbContextFactory.Create();
        var alpha = School("100", "Alpha", "WESTERN CAPE");
        alpha.AddPerformance(new SchoolPerformance(2024, 0, 30, 20, 66.7m));
        var bravo = School("200", "Bravo", "GAUTENG");
        bravo.AddPerformance(new SchoolPerformance(2024, 0, 10, 9, 90m));
        var charlie = School("300", "Charlie", "EASTERN CAPE");
        charlie.AddPerformance(new SchoolPerformance(2024, 0, 20, 15, 75m));
        db.Schools.AddRange(alpha, bravo, charlie);
        await db.SaveChangesAsync();
        using var http = UnreachableSearch();
        var service = new DashboardService(db, new MeilisearchClient(http));

        var byWrote = await service.GetSchoolsAsync(1, 10, null, "totalWrote", "desc");
        var byRate = await service.GetSchoolsAsync(1, 10, null, "passRate");
        var byProvince = await service.GetSchoolsAsync(1, 10, null, "province", "desc");
        var byName = await service.GetSchoolsAsync(1, 10, null, "name", "desc");

        Assert.Equal(["100", "300", "200"], byWrote.Items.Select(s => s.Id));
        Assert.Equal(["200", "300", "100"], byRate.Items.Select(s => s.Id));
        Assert.Equal(["100", "200", "300"], byProvince.Items.Select(s => s.Id));
        Assert.Equal(["300", "200", "100"], byName.Items.Select(s => s.Id));
    }

    /// <summary>Historical totals must be compared against the same academic year.</summary>
    [Fact]
    public async Task Detail_HistoricalSchoolUsesHistoricalCohortAndWeightedAverages()
    {
        await using var db = TestDbContextFactory.Create();
        var school = School("100", "Historical");
        school.AddPerformance(new SchoolPerformance(2023, 0, 10, 8, 80));
        var peer = School("200", "Peer");
        peer.AddPerformance(new SchoolPerformance(2023, 0, 90, 81, 90));
        peer.AddPerformance(new SchoolPerformance(2024, 0, 100, 10, 10));
        var otherProvince = School("300", "Other", "LIMPOPO");
        otherProvince.AddPerformance(new SchoolPerformance(2023, 0, 100, 50, 50));
        var deleted = School("400", "Deleted");
        deleted.AddPerformance(new SchoolPerformance(2023, 0, 100, 100, 100));
        deleted.SoftDelete();
        db.Schools.AddRange(school, peer, otherProvince, deleted);
        await db.SaveChangesAsync();
        using var http = UnreachableSearch();
        var service = new DashboardService(db, new MeilisearchClient(http));

        var result = await service.GetSchoolByIdAsync("100");

        Assert.True(result.IsSuccess);
        Assert.Equal(8, result.Value.TotalPassed);
        Assert.Null(result.Value.TotalAchieved);
        Assert.Equal(2023, result.Value.Comparison.Year);
        Assert.Equal(2, result.Value.Comparison.ProvinceRank);
        Assert.Equal(2, result.Value.Comparison.ProvinceSchoolCount);
        Assert.Equal(89, result.Value.Comparison.ProvinceAveragePassRate);
        Assert.Equal(69.5, result.Value.Comparison.NationalAveragePassRate);
    }

    /// <summary>No record means unknown rank, while a recorded zero pass rate remains rankable.</summary>
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 2)]
    public async Task Detail_DistinguishesMissingPerformanceFromZeroPassRate(bool hasPerformance, int expectedRank)
    {
        await using var db = TestDbContextFactory.Create();
        var school = School("100", "School");
        var performance = new SchoolPerformance(2024, 0, 10, 0, 0);
        if (!hasPerformance)
            performance.SoftDelete();
        school.AddPerformance(performance);
        var peer = School("200", "Peer");
        peer.AddPerformance(new SchoolPerformance(2024, 0, 10, 9, 90));
        db.Schools.AddRange(school, peer);
        await db.SaveChangesAsync();
        using var http = UnreachableSearch();
        var service = new DashboardService(db, new MeilisearchClient(http));

        var result = await service.GetSchoolByIdAsync("100");

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedRank, result.Value.Comparison.ProvinceRank);
        Assert.Equal(2024, result.Value.Comparison.Year);
        Assert.Equal(hasPerformance ? 2 : 1, result.Value.Comparison.ProvinceSchoolCount);
    }

    /// <summary>Exercises the real SDK transport failure and PostgreSQL normalization after migrations.</summary>
    [DashboardPostgresFact]
    public async Task Search_CommunicationFailureFallsBackToPostgresWithLiteralAccentInsensitivePagination()
    {
        var connectionString = Environment.GetEnvironmentVariable("DASHBOARD_TEST_POSTGRES")
            ?? throw new InvalidOperationException("DASHBOARD_TEST_POSTGRES must be set.");
        var database = "dashboard_tests_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connectionString);
        await admin.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin);
        await create.ExecuteNonQueryAsync();
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString) { Database = database, Pooling = false };
            var options = new DbContextOptionsBuilder<MatricDasbhoardDbContext>().UseNpgsql(builder.ConnectionString).Options;
            await using var db = new MatricDasbhoardDbContext(options);
            await db.Database.MigrateAsync();
            var deleted = School("004", "École");
            deleted.SoftDelete();
            db.Schools.AddRange(School("003", "École"), School("001", "École"),
                School("002", "École"), School("005", "Plain"), School("006", "100%_School"), deleted);
            await db.SaveChangesAsync();
            var client = new MeilisearchClient("http://127.0.0.1:1");
            // Confirm the SDK wraps the transport exception in the type under review.
            await Assert.ThrowsAsync<MeilisearchCommunicationError>(() => client.Index("schools").SearchAsync<object>("ecole"));
            var service = new DashboardService(db, client);

            foreach (var term in new[] { " eCoLe ", "ÉCOLE", "E\u0301cole", "école" })
            {
                var first = await service.GetSchoolsAsync(1, 2, term);
                var second = await service.GetSchoolsAsync(2, 2, term);
                Assert.Equal(3, first.TotalCount);
                Assert.Equal(["001", "002"], first.Items.Select(s => s.Id));
                Assert.Equal("003", Assert.Single(second.Items).Id);
            }
            Assert.Equal("005", Assert.Single((await service.GetSchoolsAsync(1, 10, "005")).Items).Id);
            Assert.Equal("006", Assert.Single((await service.GetSchoolsAsync(1, 10, "%_")).Items).Id);
            Assert.Empty((await service.GetSchoolsAsync(1, 10, "' OR TRUE; --")).Items);
            var detail = await service.GetSchoolByIdAsync("005");
            Assert.True(detail.IsSuccess);
            Assert.Equal(0, detail.Value.Comparison.ProvinceRank);
            Assert.Null(detail.Value.Comparison.NationalAveragePassRate);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    /// <summary>Region stats aggregate the requested scope with a learner-weighted rate.</summary>
    [Fact]
    public async Task RegionStats_AggregatesScopeWithWeightedPassRate()
    {
        await using var db = TestDbContextFactory.Create();
        var a = School("100", "A");
        a.AddPerformance(new SchoolPerformance(2024, 0, 100, 80, 80));
        var b = School("200", "B");
        b.AddPerformance(new SchoolPerformance(2024, 0, 100, 60, 60));
        var other = School("300", "C", "LIMPOPO");
        other.AddPerformance(new SchoolPerformance(2024, 0, 50, 50, 100));
        db.Schools.AddRange(a, b, other);
        await db.SaveChangesAsync();
        using var http = UnreachableSearch();
        var service = new DashboardService(db, new MeilisearchClient(http));

        var country = await service.GetRegionStatsAsync(null, null);
        Assert.Equal("country", country.Scope);
        Assert.Equal("South Africa", country.Name);
        Assert.Equal(3, country.SchoolCount);
        Assert.Equal(250, country.TotalWrote);
        Assert.Equal(190, country.TotalPassed);
        Assert.Equal(76, country.PassRate);

        var province = await service.GetRegionStatsAsync("GAUTENG", null);
        Assert.Equal("province", province.Scope);
        Assert.Equal(2, province.SchoolCount);
        Assert.Equal(70, province.PassRate);

        var district = await service.GetRegionStatsAsync("GAUTENG", "District");
        Assert.Equal("district", district.Scope);
        Assert.Equal("GAUTENG", district.Province);
        Assert.Equal(2, district.SchoolCount);
    }

    /// <summary>Region stats expose a learner-weighted pass rate trend by year.</summary>
    [Fact]
    public async Task RegionStats_BuildsYearlyWeightedTrend()
    {
        await using var db = TestDbContextFactory.Create();
        var a = School("100", "A");
        a.AddPerformance(new SchoolPerformance(2024, 0, 100, 80, 80));
        a.AddPerformance(new SchoolPerformance(2025, 0, 200, 180, 90));
        var b = School("200", "B");
        b.AddPerformance(new SchoolPerformance(2025, 0, 100, 60, 60));
        db.Schools.AddRange(a, b);
        await db.SaveChangesAsync();
        using var http = UnreachableSearch();
        var service = new DashboardService(db, new MeilisearchClient(http));

        var province = await service.GetRegionStatsAsync("GAUTENG", null);

        Assert.Equal(2, province.Years.Count);
        Assert.Equal(2024, province.Years[0].Year);
        Assert.Equal(80, province.Years[0].PassRate);
        Assert.Equal(2025, province.Years[1].Year);
        Assert.Equal(300, province.Years[1].TotalWrote);
        Assert.Equal(240, province.Years[1].TotalPassed);
        Assert.Equal(80, province.Years[1].PassRate);
    }

    /// <summary>Region subjects aggregate wrote/achieved per subject, excluding other scopes.</summary>
    [Fact]
    public async Task RegionSubjects_AggregatesSubjectsAcrossScope()
    {
        await using var db = TestDbContextFactory.Create();
        var accounting = db.AddSubject("accounting", "Accounting");
        var mathematics = db.AddSubject("mathematics", "Mathematics");
        var a = School("100", "A");
        a.AddSubjectResult(accounting, 2024, 60, 48, 80.0m);
        a.AddSubjectResult(mathematics, 2024, 40, 10, 25.0m);
        var b = School("200", "B");
        b.AddSubjectResult(accounting, 2024, 40, 32, 80.0m);
        var other = School("300", "C", "LIMPOPO");
        other.AddSubjectResult(accounting, 2024, 100, 90, 90.0m);
        db.Schools.AddRange(a, b, other);
        await db.SaveChangesAsync();
        using var http = UnreachableSearch();
        var service = new DashboardService(db, new MeilisearchClient(http));

        var country = await service.GetRegionSubjectsAsync(null, null);

        Assert.Equal("country", country.Scope);
        Assert.Equal("South Africa", country.Name);
        Assert.Equal([2024], country.AvailableYears);
        Assert.Equal(3, country.SchoolCount);
        Assert.Equal(["Accounting", "Mathematics"], country.Subjects.Select(s => s.Name));
        var nationalAccounting = country.Subjects[0];
        Assert.Equal(200, nationalAccounting.TotalWrote);
        Assert.Equal(170, nationalAccounting.Achieved);
        Assert.Equal(85.0, nationalAccounting.PercentAchieved);
        Assert.Equal(3, nationalAccounting.SchoolCount);

        var province = await service.GetRegionSubjectsAsync("GAUTENG", null);

        Assert.Equal("province", province.Scope);
        Assert.Equal(2, province.SchoolCount);
        var provinceAccounting = province.Subjects.Single(s => s.Name == "Accounting");
        Assert.Equal(100, provinceAccounting.TotalWrote);
        Assert.Equal(80, provinceAccounting.Achieved);
        Assert.Equal(80.0, provinceAccounting.PercentAchieved);
    }

    /// <summary>District subjects exclude other districts and provinces, even when names match.</summary>
    [Fact]
    public async Task RegionSubjects_DistrictAggregatesOnlySelectedProvinceAndDistrict()
    {
        await using var db = TestDbContextFactory.Create();
        var mathematics = db.AddSubject("mathematics", "Mathematics");
        var first = School("100", "First");
        first.AddSubjectResult(mathematics, 2024, 60, 48, 80.0m);
        var second = School("200", "Second");
        second.AddSubjectResult(mathematics, 2024, 40, 12, 30.0m);
        var otherDistrict = new School("GAUTENG", "Other district", "300", "300", "Other district school", 3);
        otherDistrict.AddSubjectResult(mathematics, 2024, 100, 100, 100.0m);
        var otherProvince = School("400", "Other province", "LIMPOPO");
        otherProvince.AddSubjectResult(mathematics, 2024, 100, 100, 100.0m);
        db.Schools.AddRange(first, second, otherDistrict, otherProvince);
        await db.SaveChangesAsync();
        using var http = UnreachableSearch();
        var service = new DashboardService(db, new MeilisearchClient(http));

        var result = await service.GetRegionSubjectsAsync("GAUTENG", "District", 2024);

        Assert.Equal("district", result.Scope);
        Assert.Equal("District", result.Name);
        Assert.Equal(2024, result.Year);
        Assert.Equal(2, result.SchoolCount);
        var subject = Assert.Single(result.Subjects);
        Assert.Equal(100, subject.TotalWrote);
        Assert.Equal(60, subject.Achieved);
        Assert.Equal(60.0, subject.PercentAchieved);
        Assert.Equal(2, subject.SchoolCount);
    }

    /// <summary>Region subjects default to the latest year and honour an explicit year.</summary>
    [Fact]
    public async Task RegionSubjects_DefaultsToLatestYearAndHonoursExplicitYear()
    {
        await using var db = TestDbContextFactory.Create();
        var accounting = db.AddSubject("accounting", "Accounting");
        var school = School("100", "A");
        school.AddSubjectResult(accounting, 2023, 40, 30, 75.0m);
        school.AddSubjectResult(accounting, 2024, 50, 45, 90.0m);
        db.Schools.Add(school);
        await db.SaveChangesAsync();
        using var http = UnreachableSearch();
        var service = new DashboardService(db, new MeilisearchClient(http));

        var latest = await service.GetRegionSubjectsAsync("GAUTENG", null);

        Assert.Equal(2024, latest.Year);
        Assert.Equal([2023, 2024], latest.AvailableYears);
        Assert.Equal(50, Assert.Single(latest.Subjects).TotalWrote);

        var older = await service.GetRegionSubjectsAsync("GAUTENG", null, 2023);

        Assert.Equal(2023, older.Year);
        Assert.Equal(40, Assert.Single(older.Subjects).TotalWrote);
    }

    /// <summary>A scope with no offered subjects returns empty years and rows.</summary>
    [Fact]
    public async Task RegionSubjects_EmptyScopeReturnsNoSubjects()
    {
        await using var db = TestDbContextFactory.Create();
        db.Schools.Add(School("100", "A"));
        await db.SaveChangesAsync();
        using var http = UnreachableSearch();
        var service = new DashboardService(db, new MeilisearchClient(http));

        var result = await service.GetRegionSubjectsAsync(null, null);

        Assert.Null(result.Year);
        Assert.Empty(result.AvailableYears);
        Assert.Empty(result.Subjects);
        Assert.Equal(0, result.SchoolCount);

        var blankProvince = await service.GetRegionSubjectsAsync("  ", null);

        Assert.Equal("country", blankProvince.Scope);
        Assert.Null(blankProvince.Province);
    }

    /// <summary>Stats, detail and listings follow the latest year present, not a fixed year.</summary>
    [Fact]
    public async Task Stats_FollowLatestAvailableYear()
    {
        await using var db = TestDbContextFactory.Create();
        var a = School("100", "A");
        a.AddPerformance(new SchoolPerformance(2024, 0, 100, 80, 80));
        a.AddPerformance(new SchoolPerformance(2025, 0, 200, 180, 90));
        var b = School("200", "B");
        b.AddPerformance(new SchoolPerformance(2025, 0, 100, 60, 60));
        db.Schools.AddRange(a, b);
        await db.SaveChangesAsync();
        using var http = UnreachableSearch();
        var service = new DashboardService(db, new MeilisearchClient(http));

        var stats = await service.GetStatsAsync();

        Assert.Equal(2025, stats.Year);
        Assert.Equal(2, stats.ExamCenters.Total);
        Assert.Equal(100.0, stats.ExamCenters.Trend);
        Assert.Equal(300, stats.TotalLearners.Total);
        Assert.Equal(200.0, stats.TotalLearners.Trend);
        Assert.Equal(800, stats.PassRate.Total);
        Assert.Equal(0.0, stats.PassRate.Trend);

        var detail = await service.GetSchoolByIdAsync("100");
        Assert.True(detail.IsSuccess);
        Assert.Equal(2025, detail.Value.Years[^1].Year);
        Assert.Equal(200, detail.Value.TotalWrote);

        var list = await service.GetSchoolsAsync(1, 10);
        Assert.Equal(200, list.Items.Single(s => s.Id == "100").TotalWrote);
    }

    /// <summary>Detail carries the latest subject year and skips subjects with no learners.</summary>
    [Fact]
    public async Task Detail_IncludesLatestYearSubjectsExcludingNotOffered()
    {
        await using var db = TestDbContextFactory.Create();
        var school = School("100", "With Subjects");
        var accounting = db.AddSubject("accounting", "Accounting");
        var mathematics = db.AddSubject("mathematics", "Mathematics");
        school.AddSubjectResult(accounting, 2023, 40, 30, 75.0m);
        school.AddSubjectResult(accounting, 2024, 56, 54, 96.4m);
        school.AddSubjectResult(mathematics, 2024, 0, null, null);
        db.Schools.Add(school);
        await db.SaveChangesAsync();
        using var http = UnreachableSearch();
        var service = new DashboardService(db, new MeilisearchClient(http));

        var result = await service.GetSchoolByIdAsync("100");

        Assert.True(result.IsSuccess);
        Assert.Equal(2024, result.Value.SubjectsOfferedYear);
        var offered = Assert.Single(result.Value.SubjectsOffered!);
        Assert.Equal("accounting", offered.Code);
        Assert.Equal("Accounting", offered.Name);
        Assert.Equal(2024, offered.Year);
        Assert.Equal(56, offered.TotalWrote);
        Assert.Equal(96.4, offered.PercentAchieved);
    }

    /// <summary>Detail offers every subject the school has ever offered, even if last run in an earlier year.</summary>
    [Fact]
    public async Task Detail_IncludesSubjectsOfferedInEarlierYears()
    {
        await using var db = TestDbContextFactory.Create();
        var school = School("100", "Cumulative");
        var history = db.AddSubject("history", "History");
        var accounting = db.AddSubject("accounting", "Accounting");
        school.AddSubjectResult(history, 2023, 20, 17, 85.0m);
        school.AddSubjectResult(accounting, 2023, 40, 30, 75.0m);
        school.AddSubjectResult(accounting, 2024, 56, 54, 96.4m);
        db.Schools.Add(school);
        await db.SaveChangesAsync();
        using var http = UnreachableSearch();
        var service = new DashboardService(db, new MeilisearchClient(http));

        var result = await service.GetSchoolByIdAsync("100");

        Assert.True(result.IsSuccess);
        Assert.Equal(2024, result.Value.SubjectsOfferedYear);
        Assert.Equal(["Accounting", "History"], result.Value.SubjectsOffered!.Select(s => s.Name));
        var historyRow = result.Value.SubjectsOffered!.Single(s => s.Code == "history");
        Assert.Equal(2023, historyRow.Year);
        Assert.Equal(20, historyRow.TotalWrote);
        Assert.Equal(85.0, historyRow.PercentAchieved);
        var accountingRow = result.Value.SubjectsOffered!.Single(s => s.Code == "accounting");
        Assert.Equal(2024, accountingRow.Year);
        Assert.Equal(56, accountingRow.TotalWrote);
    }

    /// <summary>Detail omits subject data for schools that have none.</summary>
    [Fact]
    public async Task Detail_WithoutSubjectsReturnsNullYearAndEmptyList()
    {
        await using var db = TestDbContextFactory.Create();
        db.Schools.Add(School("100", "No Subjects"));
        await db.SaveChangesAsync();
        using var http = UnreachableSearch();
        var service = new DashboardService(db, new MeilisearchClient(http));

        var result = await service.GetSchoolByIdAsync("100");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.SubjectsOfferedYear);
        Assert.Null(result.Value.SubjectsOffered);
    }

    /// <summary>Subjects endpoint lists available years and defaults to the latest.</summary>
    [Fact]
    public async Task Subjects_ReturnsAvailableYearsAndFallsBackToLatest()
    {
        await using var db = TestDbContextFactory.Create();
        var school = School("100", "With Subjects");
        var accounting = db.AddSubject("accounting", "Accounting");
        var mathematics = db.AddSubject("mathematics", "Mathematics");
        school.AddSubjectResult(accounting, 2023, 40, 30, 75.0m);
        school.AddSubjectResult(accounting, 2024, 56, 54, 96.4m);
        school.AddSubjectResult(mathematics, 2024, 0, null, null);
        db.Schools.Add(school);
        db.Schools.Add(School("200", "No Subjects"));
        await db.SaveChangesAsync();
        using var http = UnreachableSearch();
        var service = new DashboardService(db, new MeilisearchClient(http));

        var latest = await service.GetSchoolSubjectsAsync("100");

        Assert.True(latest.IsSuccess);
        Assert.Equal("100", latest.Value.SchoolId);
        Assert.Equal(2024, latest.Value.Year);
        Assert.Equal([2023, 2024], latest.Value.AvailableYears);
        Assert.Equal(["Accounting", "Mathematics"], latest.Value.Subjects.Select(s => s.Name));
        Assert.Equal(56, latest.Value.Subjects[0].TotalWrote);
        Assert.Equal(54, latest.Value.Subjects[0].Achieved);
        Assert.Equal(96.4, latest.Value.Subjects[0].PercentAchieved);
        var notOffered = latest.Value.Subjects.Single(s => s.Code == "mathematics");
        Assert.Equal(0, notOffered.TotalWrote);
        Assert.Null(notOffered.PercentAchieved);

        var selected = await service.GetSchoolSubjectsAsync("100", 2023);

        Assert.True(selected.IsSuccess);
        Assert.Equal(2023, selected.Value.Year);
        var only = Assert.Single(selected.Value.Subjects);
        Assert.Equal("accounting", only.Code);
        Assert.Equal(30, only.Achieved);

        var empty = await service.GetSchoolSubjectsAsync("200");

        Assert.True(empty.IsSuccess);
        Assert.Null(empty.Value.Year);
        Assert.Empty(empty.Value.AvailableYears);
        Assert.Empty(empty.Value.Subjects);
    }

    /// <summary>Unknown school ids fail with the shared not-found error.</summary>
    [Fact]
    public async Task Subjects_UnknownSchoolReturnsNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        db.Schools.Add(School("100", "Known"));
        await db.SaveChangesAsync();
        using var http = UnreachableSearch();
        var service = new DashboardService(db, new MeilisearchClient(http));

        var result = await service.GetSchoolSubjectsAsync("missing");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Equal(ErrorMessages.Dashboard.SchoolNotFound, result.Error);
    }

    /// <summary>Soft-deleted subject rows and subjects are excluded from detail and subject results.</summary>
    [Fact]
    public async Task Subjects_ExcludeSoftDeletedRowsAndSubjects()
    {
        await using var db = TestDbContextFactory.Create();
        var school = School("100", "With Subjects");
        var accounting = db.AddSubject("accounting", "Accounting");
        var physics = db.AddSubject("physics", "Physical Sciences");
        school.AddSubjectResult(accounting, 2023, 40, 30, 75.0m);
        var removed = school.AddSubjectResult(accounting, 2024, 56, 54, 96.4m);
        school.AddSubjectResult(physics, 2024, 20, 15, 75.0m);
        removed.SoftDelete();
        physics.SoftDelete();
        db.Schools.Add(school);
        await db.SaveChangesAsync();
        using var http = UnreachableSearch();
        var service = new DashboardService(db, new MeilisearchClient(http));

        var detail = await service.GetSchoolByIdAsync("100");
        var subjects = await service.GetSchoolSubjectsAsync("100");

        Assert.True(detail.IsSuccess);
        Assert.Equal(2023, detail.Value.SubjectsOfferedYear);
        var offered = Assert.Single(detail.Value.SubjectsOffered!);
        Assert.Equal("accounting", offered.Code);
        Assert.Equal(2023, offered.Year);
        Assert.True(subjects.IsSuccess);
        Assert.Equal([2023], subjects.Value.AvailableYears);
        Assert.Equal(2023, subjects.Value.Year);
        Assert.Equal("accounting", Assert.Single(subjects.Value.Subjects).Code);
    }

    /// <summary>Subject rows are ordered by display name in detail and subject results.</summary>
    [Fact]
    public async Task Subjects_AreOrderedByName()
    {
        await using var db = TestDbContextFactory.Create();
        var school = School("100", "Ordered");
        var mathematics = db.AddSubject("mathematics", "Mathematics");
        var accounting = db.AddSubject("accounting", "Accounting");
        var english = db.AddSubject("english", "English Home Language");
        school.AddSubjectResult(mathematics, 2024, 10, 8, 80.0m);
        school.AddSubjectResult(accounting, 2024, 20, 18, 90.0m);
        school.AddSubjectResult(english, 2024, 30, 27, 90.0m);
        db.Schools.Add(school);
        await db.SaveChangesAsync();
        using var http = UnreachableSearch();
        var service = new DashboardService(db, new MeilisearchClient(http));

        var detail = await service.GetSchoolByIdAsync("100");
        var subjects = await service.GetSchoolSubjectsAsync("100");

        Assert.Equal(
            ["Accounting", "English Home Language", "Mathematics"],
            detail.Value.SubjectsOffered!.Select(s => s.Name));
        Assert.Equal(
            ["Accounting", "English Home Language", "Mathematics"],
            subjects.Value.Subjects.Select(s => s.Name));
    }

    /// <summary>Detail falls back to the latest year with an offered subject, or null when none exists.</summary>
    [Fact]
    public async Task Detail_FallsBackToLatestYearWithOfferedSubject()
    {
        await using var db = TestDbContextFactory.Create();
        var school = School("100", "Fallback");
        var accounting = db.AddSubject("accounting", "Accounting");
        var mathematics = db.AddSubject("mathematics", "Mathematics");
        school.AddSubjectResult(accounting, 2023, 40, 30, 75.0m);
        school.AddSubjectResult(mathematics, 2024, 0, null, null);
        var noOffers = School("200", "No Offers");
        var lifeOrientation = db.AddSubject("life-orientation", "Life Orientation");
        noOffers.AddSubjectResult(lifeOrientation, 2024, 0, null, null);
        db.Schools.AddRange(school, noOffers);
        await db.SaveChangesAsync();
        using var http = UnreachableSearch();
        var service = new DashboardService(db, new MeilisearchClient(http));

        var fallback = await service.GetSchoolByIdAsync("100");

        Assert.True(fallback.IsSuccess);
        Assert.Equal(2023, fallback.Value.SubjectsOfferedYear);
        var offered = Assert.Single(fallback.Value.SubjectsOffered!);
        Assert.Equal("accounting", offered.Code);
        Assert.Equal(2023, offered.Year);
        Assert.Equal(40, offered.TotalWrote);

        var none = await service.GetSchoolByIdAsync("200");

        Assert.True(none.IsSuccess);
        Assert.Null(none.Value.SubjectsOfferedYear);
        Assert.Null(none.Value.SubjectsOffered);
    }

    /// <summary>Directory browsing honours an exact province filter, including with an explicit sort.</summary>
    [Fact]
    public async Task Browse_FiltersByProvince()
    {
        await using var db = TestDbContextFactory.Create();
        var alpha = School("100", "Alpha", "GAUTENG");
        alpha.AddPerformance(new SchoolPerformance(2024, 0, 30, 20, 66.7m));
        var bravo = School("200", "Bravo", "LIMPOPO");
        bravo.AddPerformance(new SchoolPerformance(2024, 0, 10, 9, 90m));
        var charlie = School("300", "Charlie", "GAUTENG");
        charlie.AddPerformance(new SchoolPerformance(2024, 0, 20, 15, 75m));
        db.Schools.AddRange(alpha, bravo, charlie, School("400", "Gauteng lower", "gauteng"));
        await db.SaveChangesAsync();
        using var http = UnreachableSearch();
        var service = new DashboardService(db, new MeilisearchClient(http));

        var browse = await service.GetSchoolsAsync(1, 10, province: "GAUTENG");
        var sorted = await service.GetSchoolsAsync(1, 10, null, "passRate", "desc", "GAUTENG");
        var updated = await service.GetSchoolsAsync(1, 10, null, "mapUpdatedAt", null, "LIMPOPO");
        var unfiltered = await service.GetSchoolsAsync(1, 10, province: " ");

        Assert.Equal(["100", "300"], browse.Items.Select(s => s.Id));
        Assert.Equal(2, browse.TotalCount);
        Assert.Equal(["300", "100"], sorted.Items.Select(s => s.Id));
        Assert.Equal("200", Assert.Single(updated.Items).Id);
        Assert.Equal(4, unfiltered.TotalCount);
    }

    /// <summary>Unknown and soft-deleted subject codes fail with the subject not-found error.</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("removed")]
    [InlineData(" ")]
    public async Task SubjectEndpoints_UnknownSubjectReturnsNotFound(string code)
    {
        await using var db = TestDbContextFactory.Create();
        db.AddSubject("removed", "Removed").SoftDelete();
        await db.SaveChangesAsync();
        using var http = UnreachableSearch();
        var service = new DashboardService(db, new MeilisearchClient(http));

        var detail = await service.GetSubjectDetailAsync(code, null, null);
        var schools = await service.GetSubjectSchoolsAsync(code, new SubjectSchoolsInput());

        Assert.True(detail.IsFailure);
        Assert.Equal(ErrorType.NotFound, detail.ErrorType);
        Assert.Equal(ErrorMessages.Dashboard.SubjectNotFound, detail.Error);
        Assert.True(schools.IsFailure);
        Assert.Equal(ErrorType.NotFound, schools.ErrorType);
        Assert.Equal(ErrorMessages.Dashboard.SubjectNotFound, schools.Error);
    }

    /// <summary>Country detail aggregates with the cohort-weighted rule, a yearly trend, and every province.</summary>
    [Fact]
    public async Task SubjectDetail_CountryAggregatesTrendAndProvinces()
    {
        await using var db = TestDbContextFactory.Create();
        var service = await SeedSubjectScenarioAsync(db);

        var result = await service.GetSubjectDetailAsync("mathematics", null, null);

        Assert.True(result.IsSuccess);
        var detail = result.Value;
        Assert.Equal("mathematics", detail.Code);
        Assert.Equal("Mathematics", detail.Name);
        Assert.True(detail.IsGateway);
        Assert.Equal("country", detail.Scope);
        Assert.Equal("South Africa", detail.ScopeName);
        Assert.Null(detail.Province);
        Assert.Null(detail.District);
        Assert.Equal(2024, detail.Year);
        Assert.Equal([2023, 2024], detail.AvailableYears);
        Assert.Equal(3, detail.SchoolCount);
        Assert.Equal(200, detail.TotalWrote);
        Assert.Null(detail.Achieved);
        Assert.Equal(74.0, detail.PercentAchieved);

        Assert.Equal([2023, 2024], detail.Trend.Select(t => t.Year));
        Assert.Equal(new SubjectYearOutput(2023, 1, 50, 25, 50.0), detail.Trend[0]);
        Assert.Equal(new SubjectYearOutput(2024, 3, 200, null, 74.0), detail.Trend[1]);

        Assert.Equal(
            [
                new SubjectRegionOutput("GAUTENG", 2, 100, null, 58.0),
                new SubjectRegionOutput("LIMPOPO", 1, 100, 90, 90.0)
            ],
            detail.Provinces);
        Assert.Empty(detail.Districts);
    }

    /// <summary>Province and district scopes narrow totals but keep the national province comparison.</summary>
    [Fact]
    public async Task SubjectDetail_ProvinceAndDistrictScopes()
    {
        await using var db = TestDbContextFactory.Create();
        var service = await SeedSubjectScenarioAsync(db);

        var province = (await service.GetSubjectDetailAsync("mathematics", "GAUTENG", null)).Value;

        Assert.Equal("province", province.Scope);
        Assert.Equal("GAUTENG", province.ScopeName);
        Assert.Equal("GAUTENG", province.Province);
        Assert.Null(province.District);
        Assert.Equal(2024, province.Year);
        Assert.Equal([2023, 2024], province.AvailableYears);
        Assert.Equal(2, province.SchoolCount);
        Assert.Equal(100, province.TotalWrote);
        Assert.Null(province.Achieved);
        Assert.Equal(58.0, province.PercentAchieved);
        Assert.Equal(["GAUTENG", "LIMPOPO"], province.Provinces.Select(p => p.Name));
        Assert.Equal(
            [
                new SubjectRegionOutput("District", 1, 60, 48, 80.0),
                new SubjectRegionOutput("Other", 1, 40, null, 25.0)
            ],
            province.Districts);

        var district = (await service.GetSubjectDetailAsync("mathematics", "GAUTENG", "Other")).Value;

        Assert.Equal("district", district.Scope);
        Assert.Equal("Other", district.ScopeName);
        Assert.Equal("GAUTENG", district.Province);
        Assert.Equal("Other", district.District);
        Assert.Equal([2024], district.AvailableYears);
        Assert.Equal(1, district.SchoolCount);
        Assert.Equal(40, district.TotalWrote);
        Assert.Equal(25.0, district.PercentAchieved);
        Assert.Single(district.Trend);
        Assert.Equal(2, district.Provinces.Count);
        Assert.Equal(2, district.Districts.Count);

        var limpopo = (await service.GetSubjectDetailAsync("mathematics", "LIMPOPO", null)).Value;

        Assert.Equal([2024], limpopo.AvailableYears);
        Assert.Equal(90, limpopo.Achieved);
        Assert.Equal("Limpopo District", Assert.Single(limpopo.Districts).Name);
    }

    /// <summary>An explicit year selects that year's totals and province breakdown.</summary>
    [Fact]
    public async Task SubjectDetail_HonoursExplicitYear()
    {
        await using var db = TestDbContextFactory.Create();
        var service = await SeedSubjectScenarioAsync(db);

        var result = (await service.GetSubjectDetailAsync("mathematics", null, null, 2023)).Value;

        Assert.Equal(2023, result.Year);
        Assert.Equal([2023, 2024], result.AvailableYears);
        Assert.Equal(1, result.SchoolCount);
        Assert.Equal(50, result.TotalWrote);
        Assert.Equal(25, result.Achieved);
        Assert.Equal(50.0, result.PercentAchieved);
        Assert.Equal(2, result.Trend.Count);
        Assert.Equal(new SubjectRegionOutput("GAUTENG", 1, 50, 25, 50.0), Assert.Single(result.Provinces));
    }

    /// <summary>A scope without data still succeeds with no year, empty collections, and zero totals.</summary>
    [Fact]
    public async Task SubjectDetail_EmptyScopeReturnsEmptyResult()
    {
        await using var db = TestDbContextFactory.Create();
        var service = await SeedSubjectScenarioAsync(db);

        var result = await service.GetSubjectDetailAsync("mathematics", "NORTH WEST", null);

        Assert.True(result.IsSuccess);
        Assert.Equal("province", result.Value.Scope);
        Assert.Equal("NORTH WEST", result.Value.ScopeName);
        Assert.Null(result.Value.Year);
        Assert.Empty(result.Value.AvailableYears);
        Assert.Equal(0, result.Value.SchoolCount);
        Assert.Equal(0, result.Value.TotalWrote);
        Assert.Null(result.Value.Achieved);
        Assert.Null(result.Value.PercentAchieved);
        Assert.Empty(result.Value.Trend);
        Assert.Empty(result.Value.Provinces);
        Assert.Empty(result.Value.Districts);
    }

    /// <summary>A row with neither a count nor a rate makes the aggregate rate unknown, never zero.</summary>
    [Fact]
    public async Task SubjectDetail_UnknownResultsMakePercentNull()
    {
        await using var db = TestDbContextFactory.Create();
        var physics = db.AddSubject("physics", "Physical Sciences", isGateway: false);
        var known = School("100", "Known");
        known.AddSubjectResult(physics, 2024, 50, 40, 80.0m);
        var unknown = School("200", "Unknown");
        unknown.AddSubjectResult(physics, 2024, 50);
        db.Schools.AddRange(known, unknown);
        await db.SaveChangesAsync();
        using var http = UnreachableSearch();
        var service = new DashboardService(db, new MeilisearchClient(http));

        var result = (await service.GetSubjectDetailAsync("physics", null, null)).Value;

        Assert.False(result.IsGateway);
        Assert.Equal(100, result.TotalWrote);
        Assert.Null(result.Achieved);
        Assert.Null(result.PercentAchieved);
        Assert.Null(Assert.Single(result.Provinces).PercentAchieved);
    }

    /// <summary>The default order is pass percentage descending, unknown rates last.</summary>
    [Fact]
    public async Task SubjectSchools_DefaultsToLatestYearAndPercentDescending()
    {
        await using var db = TestDbContextFactory.Create();
        var service = await SeedSubjectSchoolsAsync(db);

        var result = await service.GetSubjectSchoolsAsync("mathematics", new SubjectSchoolsInput());

        Assert.True(result.IsSuccess);
        var page = result.Value;
        Assert.Equal(2024, page.Year);
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(1, page.PageNumber);
        Assert.Equal(25, page.PageSize);
        Assert.Equal(["100", "200", "500", "300", "400"], page.Items.Select(i => i.Id));
        Assert.Equal(new SubjectSchoolOutput("100", "Alpha High", "GAUTENG", "District", 3, 100, 80, 80.0), page.Items[0]);
        Assert.Equal(new SubjectSchoolOutput("200", "Bravo High", "GAUTENG", "District", 4, 60, null, 75.0), page.Items[1]);
        Assert.Equal(66.7, page.Items[2].PercentAchieved);
        Assert.Null(page.Items[4].Achieved);
        Assert.Null(page.Items[4].PercentAchieved);
    }

    /// <summary>Equal rates fall back to the larger cohort first.</summary>
    [Fact]
    public async Task SubjectSchools_EqualRatesOrderByCohortSize()
    {
        await using var db = TestDbContextFactory.Create();
        var mathematics = db.AddSubject("mathematics", "Mathematics");
        var small = School("100", "A Small");
        small.AddSubjectResult(mathematics, 2024, 10, 5, 50.0m);
        var large = School("200", "B Large");
        large.AddSubjectResult(mathematics, 2024, 90, 45, 50.0m);
        db.Schools.AddRange(small, large);
        await db.SaveChangesAsync();
        var service = new DashboardService(db, new MeilisearchClient(UnreachableSearch()));

        var page = (await service.GetSubjectSchoolsAsync("mathematics", new SubjectSchoolsInput())).Value;

        Assert.Equal(["200", "100"], page.Items.Select(i => i.Id));
    }

    /// <summary>Requested columns and directions apply before paging; missing metrics sort last; unknown sorts use the default.</summary>
    [Fact]
    public async Task SubjectSchools_SortsByRequestedColumn()
    {
        await using var db = TestDbContextFactory.Create();
        var service = await SeedSubjectSchoolsAsync(db);

        async Task<IEnumerable<string>> Ids(string? sort, string? order = null) =>
            (await service.GetSubjectSchoolsAsync("mathematics", new SubjectSchoolsInput(Sort: sort, Order: order)))
            .Value.Items.Select(i => i.Id);

        Assert.Equal(["100", "200", "300", "400", "500"], await Ids("name"));
        Assert.Equal(["500", "400", "300", "200", "100"], await Ids("name", "desc"));
        Assert.Equal(["100", "200", "300", "400", "500"], await Ids("province"));
        Assert.Equal(["500", "100", "200", "300", "400"], await Ids("province", "desc"));
        Assert.Equal(["100", "300", "200", "500", "400"], await Ids("totalWrote"));
        Assert.Equal(["400", "500", "200", "300", "100"], await Ids("TOTALWROTE", "asc"));
        Assert.Equal(["100", "300", "500", "200", "400"], await Ids("achieved"));
        Assert.Equal(["500", "300", "100", "200", "400"], await Ids("achieved", "asc"));
        Assert.Equal(["300", "500", "200", "100", "400"], await Ids("percentAchieved", "asc"));
        Assert.Equal(["100", "200", "500", "300", "400"], await Ids("bogus", "sideways"));
    }

    /// <summary>Pages are clamped to sensible bounds and expose the full match count.</summary>
    [Fact]
    public async Task SubjectSchools_PagesAndClampsBounds()
    {
        await using var db = TestDbContextFactory.Create();
        var service = await SeedSubjectSchoolsAsync(db);

        var second = (await service.GetSubjectSchoolsAsync(
            "mathematics", new SubjectSchoolsInput(PageNumber: 2, PageSize: 2))).Value;
        var clampedLow = (await service.GetSubjectSchoolsAsync(
            "mathematics", new SubjectSchoolsInput(PageNumber: 0, PageSize: 0))).Value;
        var clampedHigh = (await service.GetSubjectSchoolsAsync(
            "mathematics", new SubjectSchoolsInput(PageSize: 500))).Value;
        var beyond = (await service.GetSubjectSchoolsAsync(
            "mathematics", new SubjectSchoolsInput(PageNumber: 9, PageSize: 2))).Value;

        Assert.Equal(["500", "300"], second.Items.Select(i => i.Id));
        Assert.Equal(5, second.TotalCount);
        Assert.Equal(1, clampedLow.PageNumber);
        Assert.Equal(1, clampedLow.PageSize);
        Assert.Equal("100", Assert.Single(clampedLow.Items).Id);
        Assert.Equal(100, clampedHigh.PageSize);
        Assert.Equal(5, clampedHigh.Items.Count);
        Assert.Empty(beyond.Items);
        Assert.Equal(5, beyond.TotalCount);

        foreach (var size in new[] { 1, 25, 100 })
        {
            var huge = await service.GetSubjectSchoolsAsync(
                "mathematics", new SubjectSchoolsInput(PageNumber: int.MaxValue, PageSize: size));
            Assert.True(huge.IsSuccess);
            Assert.Empty(huge.Value.Items);
            Assert.Equal(5, huge.Value.TotalCount);
        }
    }

    /// <summary>Search matches names and EMIS numbers case-insensitively and treats wildcards literally.</summary>
    [Fact]
    public async Task SubjectSchools_SearchesNameAndEmis()
    {
        await using var db = TestDbContextFactory.Create();
        var service = await SeedSubjectSchoolsAsync(db);

        async Task<IEnumerable<string>> Ids(string search) =>
            (await service.GetSubjectSchoolsAsync("mathematics", new SubjectSchoolsInput(Search: search, Sort: "name")))
            .Value.Items.Select(i => i.Id);

        Assert.Equal(["100"], await Ids("  ALPHA "));
        Assert.Equal(["300"], await Ids("300"));
        Assert.Equal(["300", "400"], await Ids("high_"));
        Assert.Empty(await Ids("h_gh"));
        Assert.Empty(await Ids("%"));
        var counted = (await service.GetSubjectSchoolsAsync("mathematics", new SubjectSchoolsInput(Search: "high"))).Value;
        Assert.Equal(4, counted.TotalCount);
    }

    /// <summary>Province, district, and year filters scope the list; an empty scope has no year.</summary>
    [Fact]
    public async Task SubjectSchools_FiltersByScopeAndYear()
    {
        await using var db = TestDbContextFactory.Create();
        var service = await SeedSubjectSchoolsAsync(db);

        var province = (await service.GetSubjectSchoolsAsync(
            "mathematics", new SubjectSchoolsInput(Province: "GAUTENG", Sort: "name"))).Value;
        var district = (await service.GetSubjectSchoolsAsync(
            "mathematics", new SubjectSchoolsInput(Province: "GAUTENG", District: "Other", Sort: "name"))).Value;
        var older = (await service.GetSubjectSchoolsAsync(
            "mathematics", new SubjectSchoolsInput(Year: 2023))).Value;
        var limpopoLatest = (await service.GetSubjectSchoolsAsync(
            "mathematics", new SubjectSchoolsInput(Province: "LIMPOPO"))).Value;
        var empty = (await service.GetSubjectSchoolsAsync(
            "mathematics", new SubjectSchoolsInput(Province: "NORTH WEST"))).Value;

        Assert.Equal(["100", "200", "300", "400"], province.Items.Select(i => i.Id));
        Assert.Equal(["300", "400"], district.Items.Select(i => i.Id));
        Assert.Equal(2023, older.Year);
        Assert.Equal("100", Assert.Single(older.Items).Id);
        Assert.Equal(40, older.Items[0].TotalWrote);
        Assert.Equal(2024, limpopoLatest.Year);
        Assert.Equal("500", Assert.Single(limpopoLatest.Items).Id);
        Assert.Null(empty.Year);
        Assert.Empty(empty.Items);
        Assert.Equal(0, empty.TotalCount);
    }

    /// <summary>
    /// Mathematics across two provinces and districts. 2024: A (count + rate),
    /// B (rate only), C (count only, Limpopo); 2023: A only. Rows that must be
    /// ignored: no learners, a soft-deleted row, a soft-deleted school, and a
    /// different subject.
    /// </summary>
    private static async Task<DashboardService> SeedSubjectScenarioAsync(MatricDasbhoardDbContext db)
    {
        var mathematics = db.AddSubject("mathematics", "Mathematics");
        var accounting = db.AddSubject("accounting", "Accounting");
        var a = School("100", "A");
        a.AddSubjectResult(mathematics, 2023, 50, 25, 50.0m);
        a.AddSubjectResult(mathematics, 2024, 60, 48, 80.0m);
        a.AddSubjectResult(accounting, 2024, 500, 500, 100.0m);
        var b = new School("GAUTENG", "Other", "200", "200", "B", 2);
        b.AddSubjectResult(mathematics, 2024, 40, null, 25.0m);
        var c = new School("LIMPOPO", "Limpopo District", "300", "300", "C", 1);
        c.AddSubjectResult(mathematics, 2024, 100, 90, null);
        var notOffered = School("400", "Not offered");
        notOffered.AddSubjectResult(mathematics, 2024, 0);
        var deletedRow = School("500", "Deleted row");
        deletedRow.AddSubjectResult(mathematics, 2024, 1000, 0, 0m).SoftDelete();
        var deletedSchool = School("600", "Deleted school", "NORTH WEST");
        deletedSchool.AddSubjectResult(mathematics, 2024, 1000, 0, 0m);
        deletedSchool.SoftDelete();
        db.Schools.AddRange(a, b, c, notOffered, deletedRow, deletedSchool);
        await db.SaveChangesAsync();
        return new DashboardService(db, new MeilisearchClient(UnreachableSearch()));
    }

    /// <summary>
    /// Five 2024 mathematics schools with distinct sort keys plus one 2023 row and
    /// one row with no learners: 100 Alpha 100/80/80.0, 200 Bravo 60/null/75.0,
    /// 300 Charlie (Other) 80/40/50.0, 400 Delta (Other) 10/null/null, and
    /// 500 Echo in Limpopo 30/20/null (rate derived as 66.7).
    /// </summary>
    private static async Task<DashboardService> SeedSubjectSchoolsAsync(MatricDasbhoardDbContext db)
    {
        var mathematics = db.AddSubject("mathematics", "Mathematics");
        var a = new School("GAUTENG", "District", "100", "100", "Alpha High", 3);
        a.AddSubjectResult(mathematics, 2023, 40, 20, 50.0m);
        a.AddSubjectResult(mathematics, 2024, 100, 80, 80.0m);
        var b = new School("GAUTENG", "District", "200", "200", "Bravo High", 4);
        b.AddSubjectResult(mathematics, 2024, 60, null, 75.0m);
        var c = new School("GAUTENG", "Other", "300", "300", "Charlie High_1", 2);
        c.AddSubjectResult(mathematics, 2024, 80, 40, 50.0m);
        var d = new School("GAUTENG", "Other", "400", "400", "Delta High_2", 1);
        d.AddSubjectResult(mathematics, 2024, 10);
        var e = new School("LIMPOPO", "Limpopo District", "500", "500", "Echo College", 5);
        e.AddSubjectResult(mathematics, 2024, 30, 20, null);
        var notOffered = new School("GAUTENG", "District", "600", "600", "Foxtrot High", 3);
        notOffered.AddSubjectResult(mathematics, 2024, 0);
        db.Schools.AddRange(a, b, c, d, e, notOffered);
        await db.SaveChangesAsync();
        return new DashboardService(db, new MeilisearchClient(UnreachableSearch()));
    }

    /// <summary>Detail returns the masterlist profile and the most recent enrolment row.</summary>
    [Fact]
    public async Task Detail_ReturnsProfileAndMostRecentEnrolment()
    {
        await using var db = TestDbContextFactory.Create();
        var school = School("100", "Profile School");
        school.AddPerformance(new SchoolPerformance(2024, 0, 10, 8, 80));
        school.ApplyMasterlistProfile(new SchoolMasterlistProfile(
            Email: "admin@profile.co.za",
            Telephone: "+27111234567",
            LandOwnership: OwnershipType.Government,
            LandOwnershipRaw: "Govt",
            BuildingOwnership: OwnershipType.Government,
            BuildingOwnershipRaw: "Govt Education",
            IsSection21: true,
            IsNoFeeSchool: true,
            UrbanRural: SettlementType.Urban,
            Sector: SchoolSector.Public,
            Phase: SchoolPhase.Secondary,
            Quintile: 4,
            QuintileCode: "Q4",
            DataYear: 2025));
        school.AddEnrolment(new SchoolEnrolment(2024, 1100, 38));
        school.AddEnrolment(new SchoolEnrolment(2025, 1200, 40));
        db.Schools.Add(school);
        await db.SaveChangesAsync();
        using var http = UnreachableSearch();
        var service = new DashboardService(db, new MeilisearchClient(http));

        var result = await service.GetSchoolByIdAsync("100");

        Assert.True(result.IsSuccess);
        var detail = result.Value;
        // The report's quintile wins over the masterlist's, but the raw code is kept.
        Assert.Equal(3, detail.Quintile);
        Assert.Equal("Q4", detail.QuintileCode);
        Assert.Equal("admin@profile.co.za", detail.Email);
        Assert.Equal("+27111234567", detail.Telephone);
        Assert.Equal("Public", detail.Sector);
        Assert.Equal("Secondary", detail.Phase);
        Assert.Equal("Government", detail.LandOwnership);
        Assert.Equal("Government", detail.BuildingOwnership);
        Assert.True(detail.IsSection21);
        Assert.True(detail.IsNoFeeSchool);
        Assert.Equal("Urban", detail.UrbanRural);
        Assert.NotNull(detail.Enrolment);
        Assert.Equal(2025, detail.Enrolment.Year);
        Assert.Equal(1200, detail.Enrolment.Learners);
        Assert.Equal(40, detail.Enrolment.Educators);
    }

    private static School School(string id, string name, string province = "GAUTENG") =>
        new(province, "District", id, id, name, 3);

    private static HttpClient UnreachableSearch() => new(new UnreachableSearchHandler())
    {
        BaseAddress = new Uri("http://search.test")
    };

    private sealed class UnreachableSearchHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("Connection refused"));
    }

    private sealed class RankedSearchHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {"hits":[
                        {"school_id":"200","centre_name":"Stale B","total_wrote_2024":10,"percent_achieved_2024":100},
                        {"school_id":"100","centre_name":"Stale A","total_wrote_2024":10,"percent_achieved_2024":50},
                        {"school_id":"300"},{"school_id":"missing"}
                    ],"estimatedTotalHits":4,"offset":0,"limit":10,"processingTimeMs":0,"query":"school"}
                    """, System.Text.Encoding.UTF8, "application/json")
            });
    }
}

/// <summary>Enables PostgreSQL coverage when an administrative connection to a test server is supplied.</summary>
public sealed class DashboardPostgresFactAttribute : FactAttribute
{
    /// <summary>Skips explicitly when PostgreSQL integration testing is not configured.</summary>
    public DashboardPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DASHBOARD_TEST_POSTGRES")))
            Skip = "Set DASHBOARD_TEST_POSTGRES to a PostgreSQL test server connection with CREATE DATABASE permission.";
    }
}
