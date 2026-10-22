using MatricDasbhoard.Component.Tests.Fixtures;
using MatricDasbhoard.Domain.Entities;
using MatricDasbhoard.Infrastructure.Features.Dashboard.Services;
using MatricDasbhoard.Infrastructure.Persistence;
using Meilisearch;
using Microsoft.EntityFrameworkCore;

namespace MatricDasbhoard.Component.Tests.Services;

/// <summary>
/// Coverage for ordering the school directory by its last map correction.
/// The PostGIS fixture is shared by the class, so each test scopes its rows
/// with a unique search token instead of resetting the database.
/// </summary>
public sealed class SchoolMapUpdateListingTests(PostgisFixture fixture) : IClassFixture<PostgisFixture>
{
    private static readonly DateTimeOffset Older = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Newer = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    [PostgisFact]
    public async Task UpdatedSort_OrdersByCorrectionAndExposesTimestamp()
    {
        await using var db = fixture.Context();
        var token = Token();
        var older = School("001" + token, $"Older Update {token}");
        var newer = School("002" + token, $"Newer Update {token}");
        var never = School("003" + token, $"Never Updated {token}");
        db.Schools.AddRange(older, newer, never);
        await db.SaveChangesAsync();
        await AddProfileAsync(db, older, Older);
        await AddProfileAsync(db, newer, Newer);
        var service = new DashboardService(db, new MeilisearchClient(UnreachableSearch()));

        var page = await service.GetSchoolsAsync(1, 10, token, "updated");

        Assert.Equal([newer.EmisNumber, older.EmisNumber, never.EmisNumber], page.Items.Select(i => i.Id));
        Assert.Equal(Newer, page.Items[0].MapUpdatedAt);
        Assert.Equal(Older, page.Items[1].MapUpdatedAt);
        Assert.Null(page.Items[2].MapUpdatedAt);
        Assert.Equal(3, page.TotalCount);
    }

    [PostgisFact]
    public async Task DefaultSort_StillReportsMapCorrectionTimestamp()
    {
        await using var db = fixture.Context();
        var token = Token();
        var school = School("004" + token, $"Edited School {token}");
        db.Schools.Add(school);
        await db.SaveChangesAsync();
        await AddProfileAsync(db, school, Newer);
        var service = new DashboardService(db, new MeilisearchClient(UnreachableSearch()));

        var page = await service.GetSchoolsAsync(1, 10, token);

        var item = Assert.Single(page.Items);
        Assert.Equal(school.EmisNumber, item.Id);
        Assert.Equal(Newer, item.MapUpdatedAt);
    }

    [PostgisFact]
    public async Task UpdatedSort_PagesAcrossTheCorrectedOrder()
    {
        await using var db = fixture.Context();
        var token = Token();
        var first = School("010" + token, $"First {token}");
        var second = School("011" + token, $"Second {token}");
        var third = School("012" + token, $"Third {token}");
        db.Schools.AddRange(first, second, third);
        await db.SaveChangesAsync();
        await AddProfileAsync(db, first, Older.AddDays(-2));
        await AddProfileAsync(db, second, Newer);
        await AddProfileAsync(db, third, Older);
        var service = new DashboardService(db, new MeilisearchClient(UnreachableSearch()));

        var page = await service.GetSchoolsAsync(2, 2, token, "updated");

        var item = Assert.Single(page.Items);
        Assert.Equal(first.EmisNumber, item.Id);
        Assert.Equal(3, page.TotalCount);
    }

    [PostgisFact]
    public async Task UpdatedSort_WithoutSearchReturnsTheMostRecentCorrections()
    {
        await using var db = fixture.Context();
        var token = Token();
        // Far-future timestamps keep these rows ahead of other tests' fixtures,
        // since this test cannot scope the unscoped directory query by token.
        var older = School("020" + token, $"Older Global {token}");
        var newest = School("021" + token, $"Newest Global {token}");
        db.Schools.AddRange(older, newest);
        await db.SaveChangesAsync();
        await AddProfileAsync(db, older, new DateTimeOffset(2099, 1, 1, 8, 0, 0, TimeSpan.Zero));
        await AddProfileAsync(db, newest, new DateTimeOffset(2099, 2, 1, 8, 0, 0, TimeSpan.Zero));
        var service = new DashboardService(db, new MeilisearchClient(UnreachableSearch()));

        var page = await service.GetSchoolsAsync(1, 5, null, "updated");

        Assert.Equal([newest.EmisNumber, older.EmisNumber], page.Items.Take(2).Select(i => i.Id));
        Assert.True(page.TotalCount >= 2);
    }

    private static string Token() => Guid.NewGuid().ToString("N")[..12];

    private static School School(string id, string name) => new("GAUTENG", "District", id, id, name, 3);

    private static Task AddProfileAsync(MatricDasbhoardDbContext db, School school, DateTimeOffset changedAt) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO geo.school_spatial_profiles
              (school_id, boundary, location_override, location_footprint, revision, actor_id, changed_at,
               reason, source_description)
            VALUES ({school.Id}, NULL, NULL, NULL, 1, {Guid.NewGuid()}, {changedAt},
               'Corrected marker', 'Field visit')
            """);

    private static HttpClient UnreachableSearch() => new(new UnreachableSearchHandler())
    {
        BaseAddress = new Uri("http://search.test")
    };

    private sealed class UnreachableSearchHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("Connection refused"));
    }
}
