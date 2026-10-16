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
