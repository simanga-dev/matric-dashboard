using System.Text.Json;

namespace MatricDasbhoard.Application.Features.SchoolSpatial;

/// <summary>Bounded structural validation before PostGIS performs topology validation.</summary>
public static class SpatialGeometryValidation
{
    /// <summary>Maximum accepted request size in bytes.</summary>
    public const int MaximumRequestBytes = 1_048_576;
    /// <summary>Checks the full replacement contract and bounded two-dimensional GeoJSON shapes.</summary>
    public static bool IsValid(SaveSpatialInput input)
    {
        if (input.ExpectedRevision < 0 || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length > 2000 ||
            string.IsNullOrWhiteSpace(input.SourceDescription) || input.SourceDescription.Length > 1000 ||
            input.LocationMode is not ("imported" or "override")) return false;
        var count = 0;
        var rings = 0;
        if (input.Boundary is { ValueKind: not JsonValueKind.Null } boundary && !BoundaryValid(boundary, ref count, ref rings)) return false;
        if (input.Footprint is { ValueKind: not JsonValueKind.Null } footprint && !BoundaryValid(footprint, ref count, ref rings)) return false;
        return input.LocationMode == "imported"
            ? input.Location is null or { ValueKind: JsonValueKind.Null }
            : input.Location is { } point && Shape(point, "Point", out var coords) && Position(coords);
    }

    private static bool Shape(JsonElement value, string type, out JsonElement coordinates)
    {
        coordinates = default;
        if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Count() != 2 ||
            !value.TryGetProperty("type", out var t) || t.ValueKind != JsonValueKind.String || t.GetString() != type ||
            !value.TryGetProperty("coordinates", out coordinates)) return false;
        return coordinates.ValueKind == JsonValueKind.Array;
    }

    private static bool BoundaryValid(JsonElement value, ref int count, ref int rings)
    {
        if (Shape(value, "Polygon", out var polygon)) return PolygonValid(polygon, ref count, ref rings);
        if (!Shape(value, "MultiPolygon", out var polygons) || polygons.GetArrayLength() is < 1 or > 100) return false;
        foreach (var p in polygons.EnumerateArray()) if (!PolygonValid(p, ref count, ref rings)) return false;
        return true;
    }

    private static bool PolygonValid(JsonElement polygon, ref int count, ref int rings)
    {
        if (polygon.ValueKind != JsonValueKind.Array || polygon.GetArrayLength() is < 1 or > 100) return false;
        rings += polygon.GetArrayLength();
        if (rings > 100) return false;
        foreach (var ring in polygon.EnumerateArray())
        {
            if (ring.ValueKind != JsonValueKind.Array || ring.GetArrayLength() < 4) return false;
            count += ring.GetArrayLength();
            if (count > 10000) return false;
            foreach (var p in ring.EnumerateArray()) if (!Position(p)) return false;
            var first = ring[0];
            var last = ring[ring.GetArrayLength() - 1];
            if (first[0].GetDouble() != last[0].GetDouble() || first[1].GetDouble() != last[1].GetDouble()) return false;
            if (ring.EnumerateArray().Select(p => (p[0].GetDouble(), p[1].GetDouble())).Distinct().Take(3).Count() < 3) return false;
        }
        return true;
    }

    private static bool Position(JsonElement value) => value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == 2 &&
        value[0].ValueKind == JsonValueKind.Number && value[1].ValueKind == JsonValueKind.Number &&
        value[0].TryGetDouble(out var x) && double.IsFinite(x) && x is >= -180 and <= 180 &&
        value[1].TryGetDouble(out var y) && double.IsFinite(y) && y is >= -90 and <= 90;
}
