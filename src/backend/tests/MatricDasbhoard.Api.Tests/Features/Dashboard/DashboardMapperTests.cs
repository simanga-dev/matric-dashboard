using System.Text.Json;
using MatricDasbhoard.Api.Tests.Contracts;
using MatricDasbhoard.Application.Features.Dashboard.Dtos;
using MatricDasbhoard.WebApi.Features.Dashboard;

namespace MatricDasbhoard.Api.Tests.Features.Dashboard;

public class DashboardMapperTests
{
    [Fact]
    public void SchoolDetail_MapsAllFieldsIncludingYearsAndComparison()
    {
        var output = new SchoolDetailOutput(
            Id: "1001",
            Name: "ALPHA HIGH SCHOOL",
            Province: "GAUTENG",
            Circuit: "Central",
            CentreNumber: "1234567",
            Quintile: 3,
            TotalWrote: 100,
            TotalPassed: 85,
            PassRate: 85,
            Years:
            [
                new SchoolYearOutput(2023, 2, 90, 70, 77.8),
                new SchoolYearOutput(2024, 3, 100, 85, 85)
            ],
            Comparison: new SchoolComparisonOutput(5, 120, 82.4, 87.3, 2023));

        var response = output.ToResponse();

        Assert.Equal("1001", response.Id);
        Assert.Equal("ALPHA HIGH SCHOOL", response.Name);
        Assert.Equal("GAUTENG", response.Province);
        Assert.Equal("Central", response.Circuit);
        Assert.Equal("1234567", response.CentreNumber);
        Assert.Equal(3, response.Quintile);
        Assert.Equal(85, response.PassRate);
        Assert.Equal(2, response.Years.Count);
        Assert.Equal(2024, response.Years[1].Year);
        Assert.Equal(3, response.Years[1].Progressed);
        Assert.Equal(85, response.Years[1].PassRate);
        Assert.Equal(5, response.Comparison.ProvinceRank);
        Assert.Equal(2023, response.Comparison.Year);
        Assert.Null(response.TotalAchieved);
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("totalAchieved").ValueKind);
        var legacy = JsonSerializer.Deserialize<SchoolDetailV1Contract>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(legacy);
        Assert.Equal(85, legacy.TotalPassed);
        Assert.Null(legacy.TotalAchieved);
        Assert.Equal(120, response.Comparison.ProvinceSchoolCount);
        Assert.Equal(82.4, response.Comparison.ProvinceAveragePassRate);
        Assert.Equal(87.3, response.Comparison.NationalAveragePassRate);
    }

    [Fact]
    public void SchoolDetail_HandlesMissingYearsAndComparison()
    {
        var output = new SchoolDetailOutput(
            "1002",
            "BETA SECONDARY",
            "LIMPOPO",
            "North",
            "0000",
            1,
            0,
            0,
            0,
            [],
            new SchoolComparisonOutput(0, 0, null, null));

        var response = output.ToResponse();

        Assert.Empty(response.Years);
        Assert.Equal(0, response.Comparison.ProvinceRank);
        Assert.Equal(0, response.Comparison.ProvinceSchoolCount);
        Assert.Null(response.Comparison.ProvinceAveragePassRate);
        Assert.Null(response.Comparison.NationalAveragePassRate);
    }
}
