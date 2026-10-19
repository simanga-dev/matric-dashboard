using System.Text.Json;
using MatricDasbhoard.Shared;

namespace MatricDasbhoard.Application.Features.SchoolSpatial;

/// <summary>Authoritative school map reads and atomic, revision-checked corrections.</summary>
public interface ISchoolSpatialService
{
    /// <summary>Reads public geometry without private correction metadata.</summary>
    Task<Result<SpatialStateOutput>> GetAsync(string emis, CancellationToken cancellationToken);
    /// <summary>Replaces the profile while retaining existing boundaries and overrides.</summary>
    Task<Result<SpatialStateOutput>> SaveAsync(string emis, SaveSpatialInput input, CancellationToken cancellationToken);
    /// <summary>Reads the latest 100 revisions, newest first. Requires editor permission.</summary>
    Task<Result<IReadOnlyList<SpatialHistoryOutput>>> GetHistoryAsync(string emis, CancellationToken cancellationToken);
}

/// <summary>Full replacement. Imported mode never creates or clears an override.</summary>
public sealed record SaveSpatialInput(long ExpectedRevision, JsonElement? Boundary, string LocationMode,
    JsonElement? Location, string Reason, string SourceDescription, JsonElement? Footprint = null);

/// <summary>Public canonical EPSG:4326 GeoJSON state. Geometry coordinates are longitude first.</summary>
public sealed record SpatialStateOutput(string EmisNumber, JsonElement? Boundary, JsonElement? Location,
    bool HasLocationOverride, string LocationSource, long Revision, double? AreaSquareMetres,
    IReadOnlyList<string> Warnings, JsonElement? Footprint = null, string FootprintSource = "missing");

/// <summary>Editor-only geometry history with the editor identity and timestamp.</summary>
public sealed record SpatialHistoryOutput(long Revision, DateTimeOffset ChangedAt, string Reason,
    string SourceDescription, JsonElement? BeforeBoundary, JsonElement? AfterBoundary,
    JsonElement? BeforeLocationOverride, JsonElement? AfterLocationOverride,
    JsonElement? BeforeFootprint, JsonElement? AfterFootprint,
    Guid ActorId, string ActorName);
