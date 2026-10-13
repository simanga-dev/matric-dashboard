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
