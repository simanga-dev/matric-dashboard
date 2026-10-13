using MatricDasbhoard.Application.Features.Dashboard.Dtos;
using MatricDasbhoard.WebApi.Features.Dashboard.Dtos;

namespace MatricDasbhoard.WebApi.Features.Dashboard;

/// <summary>
/// Maps dashboard DTOs between application and WebApi layers.
/// </summary>
internal static class DashboardMapper
{
    /// <summary>
    /// Maps application stats output to WebApi response.
    /// </summary>
    public static DashboardStatsResponse ToResponse(this DashboardStatsOutput output) => new()
    {
        TopSchools = output.TopSchools.ToResponse(),
        ExamCenters = output.ExamCenters.ToResponse(),
        TotalLearners = output.TotalLearners.ToResponse(),
        PassRate = output.PassRate.ToResponse()
    };

    /// <summary>
    /// Maps a single stat item.
    /// </summary>
    public static StatItemResponse ToResponse(this StatItemOutput output) => new()
    {
        Total = output.Total,
        Trend = output.Trend
    };

    /// <summary>
    /// Maps a pass rate trend data point.
    /// </summary>
    public static PassRateTrendResponse ToResponse(this PassRateTrendOutput output) => new()
    {
        Year = output.Year,
        PassRate = output.PassRate,
        TotalLearners = output.TotalLearners
    };

    /// <summary>
    /// Maps aggregated region (country/province/district) stats.
    /// </summary>
    public static RegionStatsResponse ToResponse(this RegionStatsOutput output) => new()
    {
        Scope = output.Scope,
        Name = output.Name,
        Province = output.Province,
        Year = output.Year,
        SchoolCount = output.SchoolCount,
        TotalWrote = output.TotalWrote,
        TotalPassed = output.TotalPassed,
        PassRate = output.PassRate
    };

    /// <summary>
    /// Maps a school entry.
    /// </summary>
    public static SchoolResponse ToResponse(this SchoolOutput output) => new()
    {
        Id = output.Id,
        Name = output.Name,
        Province = output.Province,
        Circuit = output.Circuit,
        TotalWrote = output.TotalWrote,
        TotalPassed = output.TotalPassed,
        PassRate = output.PassRate,
        TotalAchieved = output.TotalAchieved
    };

    /// <summary>
    /// Maps a paginated school list.
    /// </summary>
    public static SchoolListResponse ToResponse(this SchoolListOutput output) => new()
    {
        Items = output.Items.Select(i => i.ToResponse()).ToList(),
        TotalCount = output.TotalCount,
        PageNumber = output.PageNumber,
        PageSize = output.PageSize
    };

    /// <summary>
    /// Maps full school detail.
    /// </summary>
    public static SchoolDetailResponse ToResponse(this SchoolDetailOutput output) => new()
    {
        Id = output.Id,
        Name = output.Name,
        Province = output.Province,
        Circuit = output.Circuit,
        CentreNumber = output.CentreNumber,
        TotalAchieved = output.TotalAchieved,
        Quintile = output.Quintile,
        TotalWrote = output.TotalWrote,
        TotalPassed = output.TotalPassed,
        PassRate = output.PassRate,
        Years = output.Years.Select(y => y.ToResponse()).ToList(),
        Comparison = output.Comparison.ToResponse()
    };

    /// <summary>
    /// Maps a single year's performance.
    /// </summary>
    public static SchoolYearResponse ToResponse(this SchoolYearOutput output) => new()
    {
        Year = output.Year,
        Progressed = output.Progressed,
        TotalWrote = output.TotalWrote,
        TotalPassed = output.TotalPassed,
        PassRate = output.PassRate
    };

    /// <summary>
    /// Maps province and national comparison.
    /// </summary>
    public static SchoolComparisonResponse ToResponse(this SchoolComparisonOutput output) => new()
    {
        ProvinceRank = output.ProvinceRank,
        Year = output.Year,
        ProvinceSchoolCount = output.ProvinceSchoolCount,
        ProvinceAveragePassRate = output.ProvinceAveragePassRate,
        NationalAveragePassRate = output.NationalAveragePassRate
    };
}
