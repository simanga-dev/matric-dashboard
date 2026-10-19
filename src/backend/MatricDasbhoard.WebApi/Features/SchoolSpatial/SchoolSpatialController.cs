using FluentValidation;
using MatricDasbhoard.Application.Features.SchoolSpatial;
using MatricDasbhoard.Application.Identity.Constants;
using MatricDasbhoard.Shared;
using MatricDasbhoard.WebApi.Authorization;
using MatricDasbhoard.WebApi.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace MatricDasbhoard.WebApi.Features.SchoolSpatial;

/// <summary>School grounds and independently corrected marker geometry.</summary>
[ApiController]
[Route("api/v1/schools/{emis}/spatial")]
[Tags("SchoolSpatial")]
public sealed class SchoolSpatialController(ISchoolSpatialService service) : ControllerBase
{
    /// <summary>Returns authoritative public school map state in EPSG:4326.</summary>
    [HttpGet, AllowAnonymous]
    [ProducesResponseType(typeof(SpatialStateOutput), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SpatialStateOutput>> Get(string emis, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(emis, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ProblemFactory.Create(result.Error, result.ErrorType);
    }

    /// <summary>Atomically replaces grounds and marker mode, with optimistic concurrency.</summary>
    [HttpPut, Authorize, RequirePermission(AppPermissions.Schools.SpatialEdit)]
    [RequestSizeLimit(SpatialGeometryValidation.MaximumRequestBytes)]
    [ProducesResponseType(typeof(SpatialStateOutput), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<SpatialStateOutput>> Save(string emis, SaveSpatialRequest request, CancellationToken cancellationToken)
    {
        var result = await service.SaveAsync(emis, request.ToInput(), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ProblemFactory.Create(result.Error, result.ErrorType);
    }

    /// <summary>Returns the latest 100 correction revisions, newest first, for editors only.</summary>
    [HttpGet("history"), Authorize, RequirePermission(AppPermissions.Schools.SpatialEdit)]
    [ProducesResponseType(typeof(IReadOnlyList<SpatialHistoryOutput>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<SpatialHistoryOutput>>> History(string emis, CancellationToken cancellationToken)
    {
        var result = await service.GetHistoryAsync(emis, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ProblemFactory.Create(result.Error, result.ErrorType);
    }
}

/// <summary>Full replacement. Null cannot remove an existing boundary or override.</summary>
public sealed class SaveSpatialRequest
{
    /// <summary>Revision loaded from GET, or zero before the first save.</summary>
    public required long ExpectedRevision { get; init; }
    /// <summary>GeoJSON Polygon or MultiPolygon, longitude first, EPSG:4326.</summary>
    public required JsonElement? Boundary { get; init; }
    /// <summary>Either imported or override. Imported requires a null location.</summary>
    public required string LocationMode { get; init; }
    /// <summary>GeoJSON Point for override mode, otherwise null.</summary>
    public required JsonElement? Location { get; init; }
    /// <summary>Required correction reason, at most 2000 characters.</summary>
    public required string Reason { get; init; }
    /// <summary>Required evidence description, at most 1000 characters. Never published.</summary>
    public required string SourceDescription { get; init; }
    /// <summary>Maps the HTTP request to the application contract.</summary>
    internal SaveSpatialInput ToInput() => new(ExpectedRevision, Boundary, LocationMode, Location, Reason, SourceDescription);
}

/// <summary>Validates bounded request shapes before database access.</summary>
public sealed class SaveSpatialRequestValidator : AbstractValidator<SaveSpatialRequest>
{
    /// <summary>Registers full replacement and GeoJSON rules.</summary>
    public SaveSpatialRequestValidator() => RuleFor(x => x).Must(x => SpatialGeometryValidation.IsValid(x.ToInput())).WithMessage(SpatialErrors.Invalid);
}
