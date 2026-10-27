using System.Text.Json;
using MatricDasbhoard.Api.Tests.Contracts;
using MatricDasbhoard.Application.Features.Dashboard.Dtos;
using MatricDasbhoard.WebApi.Features.Dashboard;
using MatricDasbhoard.WebApi.Features.Dashboard.Dtos;

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
            Comparison: new SchoolComparisonOutput(5, 120, 82.4, 87.3, 2023),
            SubjectsOfferedYear: 2024,
            SubjectsOffered:
            [
                new OfferedSubjectOutput("history", "History", 2023, 20, 85.0)
            ],
            QuintileCode: "Q3",
            Email: "admin@alpha.co.za",
            Telephone: "+27111234567",
            Sector: "Public",
            Phase: "Secondary",
            LandOwnership: "Government",
            BuildingOwnership: "Government",
            IsSection21: true,
            IsNoFeeSchool: true,
            UrbanRural: "Urban",
            Enrolment: new SchoolEnrolmentOutput(2025, 1200, 40));

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
        Assert.Equal(2024, response.SubjectsOfferedYear);
        var offered = Assert.Single(response.SubjectsOffered);
        Assert.Equal("history", offered.Code);
        Assert.Equal("History", offered.Name);
        Assert.Equal(2023, offered.Year);
        Assert.Equal(20, offered.TotalWrote);
        Assert.Equal(85.0, offered.PercentAchieved);
        var offeredJson = document.RootElement.GetProperty("subjectsOffered");
        Assert.Equal(JsonValueKind.Array, offeredJson.ValueKind);
        Assert.Equal("history", offeredJson[0].GetProperty("code").GetString());
        Assert.Equal("Q3", response.QuintileCode);
        Assert.Equal("admin@alpha.co.za", response.Email);
        Assert.Equal("+27111234567", response.Telephone);
        Assert.Equal("Public", response.Sector);
        Assert.Equal("Secondary", response.Phase);
        Assert.Equal("Government", response.LandOwnership);
        Assert.Equal("Government", response.BuildingOwnership);
        Assert.True(response.IsSection21);
        Assert.True(response.IsNoFeeSchool);
        Assert.Equal("Urban", response.UrbanRural);
        var enrolment = Assert.IsType<SchoolEnrolmentResponse>(response.Enrolment);
        Assert.Equal(2025, enrolment.Year);
        Assert.Equal(1200, enrolment.Learners);
        Assert.Equal(40, enrolment.Educators);
        Assert.Equal("admin@alpha.co.za", document.RootElement.GetProperty("email").GetString());
        Assert.Equal(1200, document.RootElement.GetProperty("enrolment").GetProperty("learners").GetInt32());
        Assert.Equal(2023, offeredJson[0].GetProperty("year").GetInt32());
        Assert.Equal(85.0, offeredJson[0].GetProperty("percentAchieved").GetDouble());
        Assert.Equal(2024, document.RootElement.GetProperty("subjectsOfferedYear").GetInt32());
    }

    [Fact]
    public void SchoolDetail_WithoutMasterlistProfile_MapsNulls()
    {
        var output = new SchoolDetailOutput(
            "1003",
            "GAMMA PRIMARY",
            "NORTH WEST",
            "Bojanala",
            "7654321",
            Quintile: null,
            TotalWrote: 0,
            TotalPassed: 0,
            PassRate: 0,
            Years: [],
            Comparison: new SchoolComparisonOutput(0, 0, null, null, 2024));

        var response = output.ToResponse();

        Assert.Null(response.Quintile);
        Assert.Null(response.QuintileCode);
        Assert.Null(response.Email);
        Assert.Null(response.Telephone);
        Assert.Null(response.Sector);
        Assert.Null(response.Phase);
        Assert.Null(response.LandOwnership);
        Assert.Null(response.BuildingOwnership);
        Assert.Null(response.IsSection21);
        Assert.Null(response.IsNoFeeSchool);
        Assert.Null(response.UrbanRural);
        Assert.Null(response.Enrolment);
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
        Assert.Null(response.SubjectsOfferedYear);
        Assert.Empty(response.SubjectsOffered);
    }

    [Fact]
    public void SchoolSubjects_MapsSelectedYearAndAvailableYears()
    {
        var output = new SchoolSubjectsOutput(
            SchoolId: "1001",
            Year: 2024,
            AvailableYears: [2023, 2024],
            Subjects:
            [
                new SchoolSubjectOutput("8f3c...", "accounting", "Accounting", 56, 54, 96.4),
                new SchoolSubjectOutput("b211...", "mathematics", "Mathematics", 0, null, null)
            ]);

        var response = output.ToResponse();

        Assert.Equal("1001", response.SchoolId);
        Assert.Equal(2024, response.Year);
        Assert.Equal([2023, 2024], response.AvailableYears);
        Assert.Equal(2, response.Subjects.Count);
        Assert.Equal("accounting", response.Subjects[0].Code);
        Assert.Equal(96.4, response.Subjects[0].PercentAchieved);
        Assert.Equal(0, response.Subjects[1].TotalWrote);
        Assert.Null(response.Subjects[1].Achieved);
        Assert.Null(response.Subjects[1].PercentAchieved);

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(json);
        Assert.Equal(2024, document.RootElement.GetProperty("year").GetInt32());
        Assert.Equal(2023, document.RootElement.GetProperty("availableYears")[0].GetInt32());
        Assert.Equal("accounting", document.RootElement.GetProperty("subjects")[0].GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("subjects")[1].GetProperty("achieved").ValueKind);
        var contract = JsonSerializer.Deserialize<SchoolSubjectsContract>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(contract);
        Assert.Equal("1001", contract.SchoolId);
        Assert.Equal(2, contract.Subjects.Count);
    }
}
