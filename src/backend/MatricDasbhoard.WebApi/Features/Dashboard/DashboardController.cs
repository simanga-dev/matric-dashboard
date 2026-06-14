using MatricDasbhoard.Application.Features.Dashboard;
using MatricDasbhoard.WebApi.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MatricDasbhoard.WebApi.Features.Dashboard;

/// <summary>
/// Public dashboard endpoints providing aggregated stats, trends, and school data.
/// </summary>
[Tags("Dashboard")]
[AllowAnonymous]
public class DashboardController(IDashboardService dashboardService) : ApiController
{
    /// <summary>
    /// Returns aggregated dashboard statistics.
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(Dtos.DashboardStatsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<Dtos.DashboardStatsResponse>> GetStats()
    {
        var stats = await dashboardService.GetStatsAsync();
        return Ok(stats.ToResponse());
    }

    /// <summary>
    /// Returns NSC pass rate trends over multiple years.
    /// </summary>
    /// <param name="years">Optional filter: "5" for last 5 years, "10" for last 10 years. Defaults to all.</param>
    [HttpGet("pass-rate-trends")]
    [ProducesResponseType(typeof(IReadOnlyList<Dtos.PassRateTrendResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Dtos.PassRateTrendResponse>>> GetPassRateTrends(
        [FromQuery] string? years = null)
    {
        var trends = await dashboardService.GetPassRateTrendsAsync(years);
        return Ok(trends.Select(t => t.ToResponse()).ToList());
    }

    /// <summary>
    /// Returns a paginated list of schools with optional search.
    /// </summary>
    [HttpGet("schools")]
    [ProducesResponseType(typeof(Dtos.SchoolListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<Dtos.SchoolListResponse>> GetSchools(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null)
    {
        var schools = await dashboardService.GetSchoolsAsync(pageNumber, pageSize, search);
        return Ok(schools.ToResponse());
    }
}
