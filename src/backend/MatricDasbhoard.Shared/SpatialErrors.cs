namespace MatricDasbhoard.Shared;

/// <summary>Stable client-facing school spatial errors.</summary>
public static class SpatialErrors
{
    /// <summary>School does not exist.</summary>
    public const string NotFound = "School not found.";
    /// <summary>Request shape or metadata is invalid.</summary>
    public const string Invalid = "Supply a nonnegative revision, reason (1-2000 characters), source (1-1000 characters), and valid two-dimensional EPSG:4326 GeoJSON. Limits: 100 polygons, 100 total rings, 10000 coordinates.";
    /// <summary>Geometry failed topology validation.</summary>
    public const string InvalidGeometry = "Boundary must be nonempty, valid and have positive area. Check for self-intersections, degenerate rings, and invalid holes.";
    /// <summary>Stale revision.</summary>
    public const string Conflict = "The school map changed. Reload its current revision before saving again.";
    /// <summary>Deletion is not supported.</summary>
    public const string CannotClear = "An existing boundary or marker override cannot be cleared through an ordinary save.";
    /// <summary>Editor permission required.</summary>
    public const string Forbidden = "School map editor permission is required.";
}
