using MatricDasbhoard.Component.Tests.Fixtures;
using MatricDasbhoard.Domain.Entities;
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
