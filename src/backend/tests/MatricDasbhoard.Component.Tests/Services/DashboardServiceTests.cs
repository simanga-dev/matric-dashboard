using MatricDasbhoard.Component.Tests.Fixtures;
using MatricDasbhoard.Domain.Entities;
using MatricDasbhoard.Infrastructure.Features.Dashboard.Services;
using MatricDasbhoard.Infrastructure.Persistence;
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
