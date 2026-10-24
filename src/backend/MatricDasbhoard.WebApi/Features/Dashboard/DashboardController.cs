using MatricDasbhoard.Application.Features.Dashboard;
using MatricDasbhoard.Application.Features.Dashboard.Dtos;
using MatricDasbhoard.Shared;
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
    /// Returns aggregated NSC performance for the country, or narrowed to a
    /// province and/or education district.
    /// </summary>
    /// <param name="province">Optional province filter (exact name, e.g. "GAUTENG").</param>
    /// <param name="district">Optional district filter (exact name), typically with a province.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("regions/stats")]
    [ProducesResponseType(typeof(Dtos.RegionStatsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<Dtos.RegionStatsResponse>> GetRegionStats(
        [FromQuery] string? province = null,
        [FromQuery] string? district = null,
        CancellationToken cancellationToken = default)
    {
        var stats = await dashboardService.GetRegionStatsAsync(province, district, cancellationToken);
        return Ok(stats.ToResponse());
    }

    /// <summary>
    /// Returns per-subject NSC performance aggregated across the country, or
    /// narrowed to a province and/or education district.
    /// </summary>
    /// <param name="province">Optional province filter (exact name, e.g. "GAUTENG").</param>
    /// <param name="district">Optional district filter (exact name), typically with a province.</param>
    /// <param name="year">Optional academic year; defaults to the latest year with subject data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("regions/subjects")]
    [ProducesResponseType(typeof(Dtos.RegionSubjectsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<Dtos.RegionSubjectsResponse>> GetRegionSubjects(
        [FromQuery] string? province = null,
        [FromQuery] string? district = null,
        [FromQuery] int? year = null,
        CancellationToken cancellationToken = default)
    {
        var subjects = await dashboardService.GetRegionSubjectsAsync(province, district, year, cancellationToken);
        return Ok(subjects.ToResponse());
    }

    /// <summary>
    /// Returns a single subject's NSC performance for the country, or narrowed to
    /// a province and/or education district, with a yearly trend, a national
    /// province breakdown, and a district breakdown when a province is selected.
    /// </summary>
    /// <param name="code">Stable subject code (e.g. "mathematics").</param>
    /// <param name="province">Optional province filter (exact name, e.g. "GAUTENG").</param>
    /// <param name="district">Optional district filter (exact name), typically with a province.</param>
    /// <param name="year">Optional academic year; defaults to the latest year with data in the scope.</param>
    /// <response code="200">Returns the subject's performance.</response>
    /// <response code="404">If no subject matches the code.</response>
    [HttpGet("subjects/{code}")]
    [ProducesResponseType(typeof(Dtos.SubjectDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Dtos.SubjectDetailResponse>> GetSubjectDetail(
        string code,
        [FromQuery] string? province = null,
        [FromQuery] string? district = null,
        [FromQuery] int? year = null,
        CancellationToken cancellationToken = default)
    {
        var result = await dashboardService.GetSubjectDetailAsync(code, province, district, year, cancellationToken);
        if (!result.IsSuccess)
        {
            return ProblemFactory.Create(result.Error, result.ErrorType);
        }

        return Ok(result.Value.ToResponse());
    }

    /// <summary>
    /// Returns a paginated list of schools that offered a subject in the selected
    /// academic year, with optional scope, search, and per-column sorting.
    /// </summary>
    /// <param name="code">Stable subject code (e.g. "mathematics").</param>
    /// <param name="province">Optional province filter (exact name, e.g. "GAUTENG").</param>
    /// <param name="district">Optional district filter (exact name), typically with a province.</param>
    /// <param name="year">Optional academic year; defaults to the latest year with data in the scope.</param>
    /// <param name="search">Optional case-insensitive school name or EMIS number search.</param>
    /// <param name="pageNumber">One-based page number.</param>
    /// <param name="pageSize">Number of schools per page (1-100).</param>
    /// <param name="sort">Column to sort by: name, province, totalWrote, achieved, or percentAchieved (default).</param>
    /// <param name="order">Sort direction: asc or desc. Defaults to desc for metrics, asc for name and province.</param>
    /// <response code="200">Returns a page of schools.</response>
    /// <response code="404">If no subject matches the code.</response>
    [HttpGet("subjects/{code}/schools")]
    [ProducesResponseType(typeof(Dtos.SubjectSchoolListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Dtos.SubjectSchoolListResponse>> GetSubjectSchools(
        string code,
        [FromQuery] string? province = null,
        [FromQuery] string? district = null,
        [FromQuery] int? year = null,
        [FromQuery] string? search = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? sort = null,
        [FromQuery] string? order = null,
        CancellationToken cancellationToken = default)
    {
        var input = new SubjectSchoolsInput(province, district, year, search, pageNumber, pageSize, sort, order);
        var result = await dashboardService.GetSubjectSchoolsAsync(code, input, cancellationToken);
        if (!result.IsSuccess)
        {
            return ProblemFactory.Create(result.Error, result.ErrorType);
        }

        return Ok(result.Value.ToResponse());
    }

    /// <summary>
    /// Returns a paginated list of schools with optional search, province filter, and per-column sorting.
    /// </summary>
    /// <param name="pageNumber">One-based page number.</param>
    /// <param name="pageSize">Number of schools per page.</param>
    /// <param name="search">Optional name or EMIS number search.</param>
    /// <param name="sort">Column to sort by: name, province, totalWrote, totalPassed, passRate, or mapUpdatedAt.</param>
    /// <param name="order">Sort direction: asc or desc. Defaults to desc for metrics and mapUpdatedAt, asc otherwise.</param>
    /// <param name="province">Optional province filter (exact name, e.g. "GAUTENG").</param>
    [HttpGet("schools")]
    [ProducesResponseType(typeof(Dtos.SchoolListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<Dtos.SchoolListResponse>> GetSchools(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? sort = null,
        [FromQuery] string? order = null,
        [FromQuery] string? province = null)
    {
        var schools = await dashboardService.GetSchoolsAsync(pageNumber, pageSize, search, sort, order, province);
        return Ok(schools.ToResponse());
    }

    /// <summary>
    /// Returns a single school by its stable identifier (EMIS number).
    /// </summary>
    /// <param name="id">The school's stable identifier from the search index.</param>
    /// <response code="200">Returns the school details.</response>
    /// <response code="404">If no school matches the identifier.</response>
    [HttpGet("schools/{id}")]
    [ProducesResponseType(typeof(Dtos.SchoolDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Dtos.SchoolDetailResponse>> GetSchoolById(string id, CancellationToken cancellationToken)
    {
        var result = await dashboardService.GetSchoolByIdAsync(id, cancellationToken);
        if (!result.IsSuccess)
        {
            return ProblemFactory.Create(result.Error, result.ErrorType);
        }

        return Ok(result.Value.ToResponse());
    }

    /// <summary>
    /// Returns per-subject NSC performance for a single school.
    /// </summary>
    /// <param name="id">The school's stable identifier from the search index.</param>
    /// <param name="year">Optional academic year; defaults to the latest year with subject data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the school's subject results.</response>
    /// <response code="404">If no school matches the identifier.</response>
    [HttpGet("schools/{id}/subjects")]
    [ProducesResponseType(typeof(Dtos.SchoolSubjectsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Dtos.SchoolSubjectsResponse>> GetSchoolSubjects(
        string id,
        [FromQuery] int? year = null,
        CancellationToken cancellationToken = default)
    {
        var result = await dashboardService.GetSchoolSubjectsAsync(id, year, cancellationToken);
        if (!result.IsSuccess)
        {
            return ProblemFactory.Create(result.Error, result.ErrorType);
        }

        return Ok(result.Value.ToResponse());
    }
}
