using System.Text.Json;
using MatricDasbhoard.Application.Features.Audit;
using MatricDasbhoard.Application.Features.SearchIndex;
using MatricDasbhoard.Application.Identity;
using MatricDasbhoard.Application.Identity.Constants;
using MatricDasbhoard.WebApi.Authorization;
using MatricDasbhoard.WebApi.Features.SearchIndex.Dtos;
using MatricDasbhoard.WebApi.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MatricDasbhoard.WebApi.Features.SearchIndex;

/// <summary>
/// Publishes the current school directory to the search index.
/// </summary>
[ApiController]
[Route("api/v1/search-index")]
[Tags("SearchIndex")]
[RequirePermission(AppPermissions.SearchIndex.Manage)]
public sealed class SearchIndexController(
    ISchoolSearchIndexService searchIndexService,
    IAuditService auditService,
    IUserContext userContext) : ControllerBase
{
    /// <summary>
    /// Replaces the Meilisearch <c>schools</c> index with every current school.
    /// </summary>
    /// <returns>Counts from the completed rebuild.</returns>
    /// <response code="200">Returns the number of indexed and removed documents.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="403">If the user lacks the search index management permission.</response>
    /// <response code="429">If the caller exceeds the admin-mutation rate limit.</response>
    /// <response code="503">If Meilisearch is unavailable or a task fails.</response>
    [HttpPost("schools")]
    [EnableRateLimiting(RateLimitPolicies.AdminMutations)]
    [ProducesResponseType(typeof(RebuildSchoolsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<RebuildSchoolsResponse>> RebuildSchools(CancellationToken cancellationToken)
    {
        var result = await searchIndexService.RebuildAsync(cancellationToken);

        if (!result.IsSuccess)
        {
            return ProblemFactory.Create(result.Error, result.ErrorType);
        }

        await auditService.LogAsync(
            AuditActions.SearchIndexRebuild,
            userId: userContext.UserId,
            targetEntityType: "SearchIndex",
            metadata: JsonSerializer.Serialize(new
            {
                indexed = result.Value.Indexed,
                removed = result.Value.Removed,
                durationMs = result.Value.DurationMs
            }),
            ct: cancellationToken);

        return Ok(new RebuildSchoolsResponse(
            result.Value.Indexed,
            result.Value.Removed,
            result.Value.DurationMs));
    }
}
