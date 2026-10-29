using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MatricDasbhoard.Api.Tests.Contracts;
using MatricDasbhoard.Api.Tests.Fixtures;
using MatricDasbhoard.Domain.Entities;
using MatricDasbhoard.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace MatricDasbhoard.Api.Tests.Controllers;

/// <summary>Checks the published school detail contract through the HTTP pipeline.</summary>
public class DashboardControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    /// <summary>The v1 detail keeps its nullable bachelor-pass field and adds coherent historical context.</summary>
    [Fact]
    public async Task Detail_RetainsV1FieldsAndReturnsSelectedComparisonYear()
    {
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MatricDasbhoardDbContext>();
        var school = new School("GAUTENG", "Central", "contract-school", "123", "Contract School", 3);
        school.AddPerformance(new SchoolPerformance(2023, 0, 100, 80, 80));
        db.Schools.Add(school);
        await db.SaveChangesAsync();

        using var response = await client.GetAsync("/api/v1/dashboard/schools/contract-school");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var legacy = await response.Content.ReadFromJsonAsync<SchoolDetailV1Contract>();
        Assert.NotNull(legacy);
        Assert.Equal("contract-school", legacy.Id);
        Assert.Equal(100, legacy.TotalWrote);
        Assert.Equal(80, legacy.TotalPassed);
        Assert.Null(legacy.TotalAchieved);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("totalAchieved").ValueKind);
        Assert.Equal(2023, json.RootElement.GetProperty("comparison").GetProperty("year").GetInt32());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("subjectsOfferedYear").ValueKind);
        Assert.Empty(json.RootElement.GetProperty("subjectsOffered").EnumerateArray());
        Assert.Null(legacy.SubjectsOfferedYear);
        Assert.Empty(legacy.SubjectsOffered!);
    }

    /// <summary>The subjects endpoint returns available years and the selected year's rows.</summary>
    [Fact]
    public async Task Subjects_ReturnsSelectedYearAndAvailableYears()
    {
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MatricDasbhoardDbContext>();
        var school = new School("GAUTENG", "Central", "subject-school", "456", "Subject School", 3);
        var accounting = new Subject("accounting", "Accounting");
        var history = new Subject("history", "History");
        var accountingPerformance = new SchoolSubjectPerformance(2024, 56, 54, 96.4m);
        var historyPerformance = new SchoolSubjectPerformance(2023, 20, 17, 85.0m);
        school.AddSubjectPerformance(accountingPerformance);
        school.AddSubjectPerformance(historyPerformance);
        accounting.AddPerformance(accountingPerformance);
        history.AddPerformance(historyPerformance);
        db.Subjects.AddRange(accounting, history);
        db.Schools.Add(school);
        await db.SaveChangesAsync();

        using var response = await client.GetAsync("/api/v1/dashboard/schools/subject-school/subjects");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contract = await response.Content.ReadFromJsonAsync<SchoolSubjectsContract>();
        Assert.NotNull(contract);
        Assert.Equal("subject-school", contract.SchoolId);
        Assert.Equal(2024, contract.Year);
        Assert.Equal([2023, 2024], contract.AvailableYears);
        var subject = Assert.Single(contract.Subjects);
        Assert.Equal("accounting", subject.Code);
        Assert.Equal("Accounting", subject.Name);
        Assert.Equal(56, subject.TotalWrote);
        Assert.Equal(54, subject.Achieved);
        Assert.Equal(96.4, subject.PercentAchieved);

        using var detailResponse = await client.GetAsync("/api/v1/dashboard/schools/subject-school");
        var detail = await detailResponse.Content.ReadFromJsonAsync<SchoolDetailV1Contract>();
        Assert.NotNull(detail);
        Assert.Equal(2024, detail.SubjectsOfferedYear);
        Assert.Equal(2, detail.SubjectsOffered!.Count);
        Assert.Equal("accounting", detail.SubjectsOffered[0].Code);
        Assert.Equal(2024, detail.SubjectsOffered[0].Year);
        Assert.Equal("history", detail.SubjectsOffered[1].Code);
        Assert.Equal(2023, detail.SubjectsOffered[1].Year);
        Assert.Equal(20, detail.SubjectsOffered[1].TotalWrote);
        Assert.Equal(85.0, detail.SubjectsOffered[1].PercentAchieved);
        using var detailJson = JsonDocument.Parse(await detailResponse.Content.ReadAsStringAsync());
        var offeredJson = detailJson.RootElement.GetProperty("subjectsOffered").EnumerateArray().ToList();
        Assert.Equal(
            2023,
            offeredJson.Single(o => o.GetProperty("code").GetString() == "history").GetProperty("year").GetInt32());
        Assert.Equal(
            2024,
            offeredJson.Single(o => o.GetProperty("code").GetString() == "accounting").GetProperty("year").GetInt32());
    }

    /// <summary>An explicit year query selects that year's subjects and lists every available year.</summary>
    [Fact]
    public async Task Subjects_WithYearQuerySelectsRequestedYear()
    {
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MatricDasbhoardDbContext>();
        var school = new School("GAUTENG", "Central", "year-query-school", "789", "Year Query School", 3);
        var mathematics = new Subject("mathematics", "Mathematics");
        school.AddSubjectPerformance(new SchoolSubjectPerformance(2023, 40, 30, 75.0m));
        school.AddSubjectPerformance(new SchoolSubjectPerformance(2024, 56, 54, 96.4m));
        foreach (var performance in school.SubjectPerformances)
        {
            mathematics.AddPerformance(performance);
        }

        db.Subjects.Add(mathematics);
        db.Schools.Add(school);
        await db.SaveChangesAsync();

        using var response = await client.GetAsync("/api/v1/dashboard/schools/year-query-school/subjects?year=2023");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal(2023, root.GetProperty("year").GetInt32());
        Assert.Equal([2023, 2024], root.GetProperty("availableYears").EnumerateArray().Select(y => y.GetInt32()));
        var row = Assert.Single(root.GetProperty("subjects").EnumerateArray());
        Assert.Equal(40, row.GetProperty("totalWrote").GetInt32());
        Assert.Equal(30, row.GetProperty("achieved").GetInt32());
        Assert.Equal(75.0, row.GetProperty("percentAchieved").GetDouble());
    }

    /// <summary>Subjects JSON keeps camelCase keys and serializes missing metrics as null.</summary>
    [Fact]
    public async Task Subjects_ReturnsCamelCaseJsonWithNullableNumbers()
    {
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MatricDasbhoardDbContext>();
        var school = new School("GAUTENG", "Central", "json-school", "321", "JSON School", 3);
        var lifeOrientation = new Subject("life-orientation", "Life Orientation");
        var performance = new SchoolSubjectPerformance(2024, 10, null, null);
        school.AddSubjectPerformance(performance);
        lifeOrientation.AddPerformance(performance);
        db.Subjects.Add(lifeOrientation);
        db.Schools.Add(school);
        await db.SaveChangesAsync();

        using var response = await client.GetAsync("/api/v1/dashboard/schools/json-school/subjects");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal("json-school", root.GetProperty("schoolId").GetString());
        Assert.Equal(JsonValueKind.Array, root.GetProperty("availableYears").ValueKind);
        Assert.False(root.TryGetProperty("SchoolId", out _));
        var row = Assert.Single(root.GetProperty("subjects").EnumerateArray());
        var subjectId = row.GetProperty("subjectId").GetString();
        Assert.True(Guid.TryParse(subjectId, out var parsedId));
        Assert.NotEqual(Guid.Empty, parsedId);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("achieved").ValueKind);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("percentAchieved").ValueKind);
    }

    /// <summary>A missing school on the subjects endpoint uses the v1 not-found ProblemDetails response.</summary>
    [Fact]
    public async Task Subjects_MissingSchoolReturns404ProblemDetails()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/dashboard/schools/missing-school/subjects");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(404, json.RootElement.GetProperty("status").GetInt32());
    }

    /// <summary>The region subjects endpoint aggregates offered subjects for a province scope.</summary>
    [Fact]
    public async Task RegionSubjects_AggregatesProvinceSubjectResults()
    {
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MatricDasbhoardDbContext>();
        // A province unique to this test keeps it isolated from other tests that
        // share the in-memory database in this fixture.
        var school = new School("NORTHERN CAPE", "Frances Baard", "region-subject-school", "654", "Region Subject School", 3);
        var accounting = new Subject("accounting", "Accounting");
        var performance = new SchoolSubjectPerformance(2024, 30, 24, 80.0m);
        school.AddSubjectPerformance(performance);
        accounting.AddPerformance(performance);
        db.Subjects.Add(accounting);
        db.Schools.Add(school);
        await db.SaveChangesAsync();

        using var response = await client.GetAsync("/api/v1/dashboard/regions/subjects?province=NORTHERN%20CAPE");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contract = await response.Content.ReadFromJsonAsync<RegionSubjectsContract>();
        Assert.NotNull(contract);
        Assert.Equal("province", contract.Scope);
        Assert.Equal("NORTHERN CAPE", contract.Name);
        Assert.Equal("NORTHERN CAPE", contract.Province);
        Assert.Equal(2024, contract.Year);
        Assert.Equal([2024], contract.AvailableYears);
        Assert.Equal(1, contract.SchoolCount);
        var row = Assert.Single(contract.Subjects);
        Assert.Equal("accounting", row.Code);
        Assert.Equal("Accounting", row.Name);
        Assert.Equal(30, row.TotalWrote);
        Assert.Equal(24, row.Achieved);
        Assert.Equal(80.0, row.PercentAchieved);
        Assert.Equal(1, row.SchoolCount);
    }

    /// <summary>The subject detail endpoint returns scoped totals, trend, and province and district breakdowns.</summary>
    [Fact]
    public async Task SubjectDetail_ReturnsScopedAggregatesAndBreakdowns()
    {
        using var client = factory.CreateClient();
        await SeedSubjectAsync("api-subject-detail");

        using var response = await client.GetAsync(
            "/api/v1/dashboard/subjects/api-subject-detail?province=FREE%20STATE");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contract = await response.Content.ReadFromJsonAsync<SubjectDetailContract>();
        Assert.NotNull(contract);
        Assert.Equal("api-subject-detail", contract.Code);
        Assert.Equal("API Subject", contract.Name);
        Assert.False(contract.IsGateway);
        Assert.Equal("province", contract.Scope);
        Assert.Equal("FREE STATE", contract.ScopeName);
        Assert.Equal("FREE STATE", contract.Province);
        Assert.Null(contract.District);
        Assert.Equal(2024, contract.Year);
        Assert.Equal([2023, 2024], contract.AvailableYears);
        Assert.Equal(2, contract.SchoolCount);
        Assert.Equal(100, contract.TotalWrote);
        Assert.Null(contract.Achieved);
        Assert.Equal(58.0, contract.PercentAchieved);
        Assert.Equal(
            [new SubjectYearContract(2023, 1, 50, 25, 50.0), new SubjectYearContract(2024, 2, 100, null, 58.0)],
            contract.Trend);
        Assert.Equal(
            [
                new SubjectRegionContract("FREE STATE", 2, 100, null, 58.0),
                new SubjectRegionContract("MPUMALANGA", 1, 100, 90, 90.0)
            ],
            contract.Provinces);
        Assert.Equal(
            [
                new SubjectRegionContract("Lejweleputswa", 1, 40, null, 25.0),
                new SubjectRegionContract("Motheo", 1, 60, 48, 80.0)
            ],
            contract.Districts);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("achieved").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("district").ValueKind);
        Assert.True(json.RootElement.TryGetProperty("scopeName", out _));
        Assert.False(json.RootElement.TryGetProperty("ScopeName", out _));
    }

    /// <summary>A scope without data still returns 200 with no year and empty collections.</summary>
    [Fact]
    public async Task SubjectDetail_EmptyScopeReturnsEmptyResult()
    {
        using var client = factory.CreateClient();
        await SeedSubjectAsync("api-subject-empty");

        using var response = await client.GetAsync(
            "/api/v1/dashboard/subjects/api-subject-empty?province=FREE%20STATE&district=Nowhere");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal("district", root.GetProperty("scope").GetString());
        Assert.Equal("Nowhere", root.GetProperty("scopeName").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("year").ValueKind);
        Assert.Empty(root.GetProperty("availableYears").EnumerateArray());
        Assert.Equal(0, root.GetProperty("totalWrote").GetInt32());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("percentAchieved").ValueKind);
        Assert.Empty(root.GetProperty("trend").EnumerateArray());
        Assert.Empty(root.GetProperty("provinces").EnumerateArray());
        Assert.Empty(root.GetProperty("districts").EnumerateArray());
    }

    /// <summary>The subject school list pages, sorts, and reports computed paging properties.</summary>
    [Fact]
    public async Task SubjectSchools_ReturnsSortedPageWithPagingMetadata()
    {
        using var client = factory.CreateClient();
        await SeedSubjectAsync("api-subject-schools");

        using var response = await client.GetAsync(
            "/api/v1/dashboard/subjects/api-subject-schools/schools?pageNumber=1&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contract = await response.Content.ReadFromJsonAsync<SubjectSchoolListContract>();
        Assert.NotNull(contract);
        Assert.Equal(2024, contract.Year);
        Assert.Equal(3, contract.TotalCount);
        Assert.Equal(1, contract.PageNumber);
        Assert.Equal(2, contract.PageSize);
        Assert.Equal(2, contract.TotalPages);
        Assert.False(contract.HasPreviousPage);
        Assert.True(contract.HasNextPage);
        Assert.Equal(
            [
                new SubjectSchoolContract("api-subject-schools-c", "Gamma School", "MPUMALANGA", "Ehlanzeni", 1, 100, 90, 90.0),
                new SubjectSchoolContract("api-subject-schools-a", "Alpha School", "FREE STATE", "Motheo", 3, 60, 48, 80.0)
            ],
            contract.Items);

        using var filtered = await client.GetAsync(
            "/api/v1/dashboard/subjects/api-subject-schools/schools?province=FREE%20STATE&search=BETA&sort=name&order=asc");
        var filteredContract = await filtered.Content.ReadFromJsonAsync<SubjectSchoolListContract>();
        Assert.NotNull(filteredContract);
        var row = Assert.Single(filteredContract.Items);
        Assert.Equal("api-subject-schools-b", row.Id);
        Assert.Null(row.Achieved);
        Assert.Equal(25.0, row.PercentAchieved);
        Assert.Equal(25, filteredContract.PageSize);
    }

    /// <summary>A page number far past the end returns an empty 200 page instead of overflowing the offset.</summary>
    [Fact]
    public async Task SubjectSchools_HugePageNumberReturnsEmptyPage()
    {
        using var client = factory.CreateClient();
        await SeedSubjectAsync("api-subject-huge-page");

        using var response = await client.GetAsync(
            $"/api/v1/dashboard/subjects/api-subject-huge-page/schools?pageNumber={int.MaxValue}&pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contract = await response.Content.ReadFromJsonAsync<SubjectSchoolListContract>();
        Assert.NotNull(contract);
        Assert.Empty(contract.Items);
        Assert.Equal(3, contract.TotalCount);
        Assert.False(contract.HasNextPage);
    }

    /// <summary>Unknown subject codes return a 404 ProblemDetails on both subject endpoints.</summary>
    [Theory]
    [InlineData("/api/v1/dashboard/subjects/missing-subject")]
    [InlineData("/api/v1/dashboard/subjects/missing-subject/schools")]
    public async Task SubjectEndpoints_MissingSubjectReturns404ProblemDetails(string url)
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(404, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Subject not found.", json.RootElement.GetProperty("detail").GetString());
    }

    /// <summary>The school directory filters by an exact province when one is supplied.</summary>
    [Fact]
    public async Task Schools_FiltersByProvince()
    {
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MatricDasbhoardDbContext>();
        db.Schools.AddRange(
            new School("API PROVINCE", "District", "api-province-b", "1", "Bravo Province School", 3),
            new School("API PROVINCE", "District", "api-province-a", "2", "Alpha Province School", 3),
            new School("API OTHER PROVINCE", "District", "api-province-c", "3", "Charlie Province School", 3));
        await db.SaveChangesAsync();

        using var response = await client.GetAsync("/api/v1/dashboard/schools?province=API%20PROVINCE&sort=name");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contract = await response.Content.ReadFromJsonAsync<SchoolListContract>();
        Assert.NotNull(contract);
        Assert.Equal(2, contract.TotalCount);
        Assert.Equal(["api-province-a", "api-province-b"], contract.Items.Select(i => i.Id));
        Assert.All(contract.Items, i => Assert.Equal("API PROVINCE", i.Province));
    }

    /// <summary>
    /// Seeds a non-gateway subject (unique code keeps tests isolated in the shared
    /// database) across two provinces: 2024 Alpha 60/48/80.0 and Beta 40/null/25.0
    /// in FREE STATE, Gamma 100/90/null in MPUMALANGA; 2023 Alpha 50/25/50.0.
    /// </summary>
    private async Task SeedSubjectAsync(string code)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MatricDasbhoardDbContext>();
        var subject = new Subject(code, "API Subject", isGateway: false);
        var alpha = new School("FREE STATE", "Motheo", code + "-a", "1", "Alpha School", 3);
        var beta = new School("FREE STATE", "Lejweleputswa", code + "-b", "2", "Beta School", 2);
        var gamma = new School("MPUMALANGA", "Ehlanzeni", code + "-c", "3", "Gamma School", 1);
        foreach (var (school, performance) in new[]
                 {
                     (alpha, new SchoolSubjectPerformance(2023, 50, 25, 50.0m)),
                     (alpha, new SchoolSubjectPerformance(2024, 60, 48, 80.0m)),
                     (beta, new SchoolSubjectPerformance(2024, 40, null, 25.0m)),
                     (gamma, new SchoolSubjectPerformance(2024, 100, 90, null))
                 })
        {
            school.AddSubjectPerformance(performance);
            subject.AddPerformance(performance);
        }

        db.Subjects.Add(subject);
        db.Schools.AddRange(alpha, beta, gamma);
        await db.SaveChangesAsync();
    }

    /// <summary>A missing school continues to use the v1 not-found ProblemDetails response.</summary>
    [Fact]
    public async Task Detail_MissingSchoolReturns404ProblemDetails()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/dashboard/schools/missing-school");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(404, json.RootElement.GetProperty("status").GetInt32());
    }
}
