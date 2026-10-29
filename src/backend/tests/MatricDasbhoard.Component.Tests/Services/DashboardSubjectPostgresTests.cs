using MatricDasbhoard.Application.Features.Dashboard.Dtos;
using MatricDasbhoard.Component.Tests.Fixtures;
using MatricDasbhoard.Domain.Entities;
using MatricDasbhoard.Infrastructure.Features.Dashboard.Services;
using Meilisearch;

namespace MatricDasbhoard.Component.Tests.Services;

/// <summary>
/// Runs subject aggregation, subject school listing, and the directory province
/// filter against PostgreSQL so the translated SQL is exercised, not just the
/// in-memory provider. The PostGIS fixture is shared by the class, so each test
/// scopes its rows with a unique token (subject code, province, or search term).
/// </summary>
public sealed class DashboardSubjectPostgresTests(PostgisFixture fixture) : IClassFixture<PostgisFixture>
{
    [PostgisFact]
    public async Task SubjectDetail_AggregatesInDatabaseWithWeightedRule()
    {
        await using var db = fixture.Context();
        var token = Token();
        var subject = new Subject("subject-" + token, "Subject " + token);
        var a = new School("GAUTENG-" + token, "District", "a" + token, "a", "A " + token, 3);
        var b = new School("GAUTENG-" + token, "Other", "b" + token, "b", "B " + token, 2);
        var c = new School("LIMPOPO-" + token, "Limpopo District", "c" + token, "c", "C " + token, 1);
        Add(a, subject, 2023, 50, 25, 50.0m);
        Add(a, subject, 2024, 60, 48, 80.0m);
        Add(b, subject, 2024, 40, null, 25.0m);
        Add(c, subject, 2024, 100, 90, null);
        Add(c, subject, 2023, 0, null, null);
        db.Subjects.Add(subject);
        db.Schools.AddRange(a, b, c);
        await db.SaveChangesAsync();
        var service = new DashboardService(db, new MeilisearchClient(UnreachableSearch()));

        var country = (await service.GetSubjectDetailAsync(subject.Code, null, null)).Value;
        var province = (await service.GetSubjectDetailAsync(subject.Code, a.Province, null)).Value;

        Assert.Equal(2024, country.Year);
        Assert.Equal([2023, 2024], country.AvailableYears);
        Assert.Equal(3, country.SchoolCount);
        Assert.Equal(200, country.TotalWrote);
        Assert.Null(country.Achieved);
        Assert.Equal(74.0, country.PercentAchieved);
        Assert.Equal(new SubjectYearOutput(2023, 1, 50, 25, 50.0), country.Trend[0]);
        Assert.Equal(
            [
                new SubjectRegionOutput(a.Province, 2, 100, null, 58.0),
                new SubjectRegionOutput(c.Province, 1, 100, 90, 90.0)
            ],
            country.Provinces);
        Assert.Empty(country.Districts);
        Assert.Equal(58.0, province.PercentAchieved);
        Assert.Equal(
            [
                new SubjectRegionOutput("District", 1, 60, 48, 80.0),
                new SubjectRegionOutput("Other", 1, 40, null, 25.0)
            ],
            province.Districts);
    }

    [PostgisFact]
    public async Task SubjectSchools_SearchesSortsAndPagesInDatabase()
    {
        await using var db = fixture.Context();
        var token = Token();
        var subject = new Subject("subject-" + token, "Subject " + token);
        var alpha = new School("GAUTENG", "District", "9001" + token, "1", "Alpha 100%_Hoërskool", 3);
        var bravo = new School("GAUTENG", "District", "9002" + token, "2", "Bravo High", 4);
        var charlie = new School("LIMPOPO", "District", "9003" + token, "3", "Charlie High", 2);
        Add(alpha, subject, 2024, 30, 20, null);
        Add(bravo, subject, 2024, 60, null, 75.0m);
        Add(charlie, subject, 2024, 10, null, null);
        db.Subjects.Add(subject);
        db.Schools.AddRange(alpha, bravo, charlie);
        await db.SaveChangesAsync();
        var service = new DashboardService(db, new MeilisearchClient(UnreachableSearch()));

        var all = (await service.GetSubjectSchoolsAsync(subject.Code, new SubjectSchoolsInput())).Value;
        var byAchievedAsc = (await service.GetSubjectSchoolsAsync(
            subject.Code, new SubjectSchoolsInput(Sort: "achieved", Order: "asc"))).Value;
        var paged = (await service.GetSubjectSchoolsAsync(
            subject.Code, new SubjectSchoolsInput(PageNumber: 2, PageSize: 1))).Value;
        var literal = (await service.GetSubjectSchoolsAsync(
            subject.Code, new SubjectSchoolsInput(Search: "100%_HOË"))).Value;
        var wildcard = (await service.GetSubjectSchoolsAsync(
            subject.Code, new SubjectSchoolsInput(Search: "b%h"))).Value;
        var byEmis = (await service.GetSubjectSchoolsAsync(
            subject.Code, new SubjectSchoolsInput(Search: "9003" + token.ToUpperInvariant()))).Value;
        var scoped = (await service.GetSubjectSchoolsAsync(
            subject.Code, new SubjectSchoolsInput(Province: "GAUTENG", Sort: "name", Order: "desc"))).Value;

        Assert.Equal(2024, all.Year);
        Assert.Equal([bravo.EmisNumber, alpha.EmisNumber, charlie.EmisNumber], all.Items.Select(i => i.Id));
        Assert.Equal(66.7, all.Items[1].PercentAchieved);
        Assert.Null(all.Items[2].PercentAchieved);
        Assert.Equal([alpha.EmisNumber, bravo.EmisNumber, charlie.EmisNumber], byAchievedAsc.Items.Select(i => i.Id));
        Assert.Equal(3, paged.TotalCount);
        Assert.Equal(alpha.EmisNumber, Assert.Single(paged.Items).Id);
        Assert.Equal(alpha.EmisNumber, Assert.Single(literal.Items).Id);
        Assert.Empty(wildcard.Items);
        Assert.Equal(charlie.EmisNumber, Assert.Single(byEmis.Items).Id);
        Assert.Equal([bravo.EmisNumber, alpha.EmisNumber], scoped.Items.Select(i => i.Id));
    }

    [PostgisFact]
    public async Task Directory_ProvinceFilterAppliesToSearchAndBypassesIndex()
    {
        await using var db = fixture.Context();
        var token = Token();
        var inProvince = new School("GAUTENG", "District", "g" + token, "1", $"Match {token}", 3);
        var otherProvince = new School("LIMPOPO", "District", "l" + token, "2", $"Match {token}", 3);
        db.Schools.AddRange(inProvince, otherProvince);
        await db.SaveChangesAsync();
        var search = new CountingSearchHandler();
        using var http = new HttpClient(search) { BaseAddress = new Uri("http://search.test") };
        var service = new DashboardService(db, new MeilisearchClient(http));

        var filtered = await service.GetSchoolsAsync(1, 10, token, province: "LIMPOPO");
        var sorted = await service.GetSchoolsAsync(1, 10, token, "name", null, "GAUTENG");

        Assert.Equal(0, search.Calls);
        Assert.Equal(otherProvince.EmisNumber, Assert.Single(filtered.Items).Id);
        Assert.Equal(1, filtered.TotalCount);
        Assert.Equal(inProvince.EmisNumber, Assert.Single(sorted.Items).Id);
    }

    [PostgisFact]
    public async Task Directory_ProvinceFilterAppliesToMapUpdatedSort()
    {
        await using var db = fixture.Context();
        var token = Token();
        var inProvince = new School("GAUTENG", "District", "g" + token, "1", $"Updated {token}", 3);
        var otherProvince = new School("LIMPOPO", "District", "l" + token, "2", $"Updated {token}", 3);
        db.Schools.AddRange(inProvince, otherProvince);
        await db.SaveChangesAsync();
        var service = new DashboardService(db, new MeilisearchClient(UnreachableSearch()));

        var page = await service.GetSchoolsAsync(1, 10, token, "mapUpdatedAt", null, "LIMPOPO");

        Assert.Equal(otherProvince.EmisNumber, Assert.Single(page.Items).Id);
        Assert.Equal(1, page.TotalCount);
    }

    private static void Add(School school, Subject subject, int year, int wrote, int? achieved, decimal? percent)
    {
        var performance = new SchoolSubjectPerformance(year, wrote, achieved, percent);
        school.AddSubjectPerformance(performance);
        subject.AddPerformance(performance);
    }

    private static string Token() => Guid.NewGuid().ToString("N")[..12];

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

    /// <summary>Records whether the search index was consulted.</summary>
    private sealed class CountingSearchHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromException<HttpResponseMessage>(new HttpRequestException("Connection refused"));
        }
    }
}
