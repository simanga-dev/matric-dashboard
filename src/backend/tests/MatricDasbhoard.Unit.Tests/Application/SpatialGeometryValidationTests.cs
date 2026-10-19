using System.Text.Json;
using MatricDasbhoard.Application.Features.SchoolSpatial;

namespace MatricDasbhoard.Unit.Tests.Application;

public class SpatialGeometryValidationTests
{
    private static SaveSpatialInput Input(string? boundary = null, string mode = "imported", string? point = null) =>
        new(0, boundary is null ? null : JsonSerializer.Deserialize<JsonElement>(boundary), mode,
            point is null ? null : JsonSerializer.Deserialize<JsonElement>(point), "Correct grounds", "Field evidence");

    [Theory]
    [InlineData("{\"type\":\"Polygon\",\"coordinates\":[[[28,-26],[28.01,-26],[28.01,-26.01],[28,-26]]]}")]
    [InlineData("{\"type\":\"MultiPolygon\",\"coordinates\":[[[[28,-26],[28.01,-26],[28.01,-26.01],[28,-26]]]]}")]
    public void ValidBoundary_PassesStructuralValidation(string json) => Assert.True(SpatialGeometryValidation.IsValid(Input(json)));

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"type\":\"Feature\",\"coordinates\":[]}")]
    [InlineData("{\"type\":\"Polygon\",\"coordinates\":[]}")]
    [InlineData("{\"type\":\"MultiPolygon\",\"coordinates\":[]}")]
    [InlineData("{\"type\":\"Polygon\",\"coordinates\":[[[28,-26],[28.01,-26],[28.01,-26.01],[29,-27]]]}")]
    [InlineData("{\"type\":\"Polygon\",\"coordinates\":[[[181,-26],[28.01,-26],[28.01,-26.01],[181,-26]]]}")]
    [InlineData("{\"type\":\"Polygon\",\"coordinates\":[[[28,-91],[28.01,-26],[28.01,-26.01],[28,-91]]]}")]
    [InlineData("{\"type\":\"Polygon\",\"coordinates\":[[[28,-26,0],[28.01,-26],[28.01,-26.01],[28,-26,0]]]}")]
    [InlineData("{\"type\":\"Polygon\",\"coordinates\":[[[28,-26],[28,-26],[28,-26],[28,-26]]]}")]
    [InlineData("{\"type\":\"Polygon\",\"coordinates\":null}")]
    public void InvalidBoundary_FailsStructuralValidation(string json) => Assert.False(SpatialGeometryValidation.IsValid(Input(json)));

    [Fact]
    public void ValidFootprint_PassesStructuralValidation()
    {
        var footprint = JsonSerializer.Deserialize<JsonElement>(
            """{"type":"MultiPolygon","coordinates":[[[[28,-26],[28.001,-26],[28.001,-26.001],[28,-26]]]]}""");
        Assert.True(SpatialGeometryValidation.IsValid(Input() with { Footprint = footprint }));
    }

    [Theory]
    [InlineData("{\"type\":\"Point\",\"coordinates\":[28,-26]}")]
    [InlineData("{\"type\":\"Polygon\",\"coordinates\":[]}")]
    [InlineData("{\"type\":\"Polygon\",\"coordinates\":[[[28,-26],[28.001,-26],[28,-26]]]}")]
    [InlineData("{}")]
    public void InvalidFootprint_FailsStructuralValidation(string json)
    {
        var footprint = JsonSerializer.Deserialize<JsonElement>(json);
        Assert.False(SpatialGeometryValidation.IsValid(Input() with { Footprint = footprint }));
    }

    [Theory]
    [InlineData("override", null, false)]
    [InlineData("imported", "{\"type\":\"Point\",\"coordinates\":[28,-26]}", false)]
    [InlineData("override", "{\"type\":\"Point\",\"coordinates\":[28,-26]}", true)]
    [InlineData("override", "{\"type\":\"Point\",\"coordinates\":[1e999,-26]}", false)]
    [InlineData("override", "{\"type\":\"Point\",\"coordinates\":[28,-26],\"crs\":{}}", false)]
    [InlineData("unknown", null, false)]
    public void PointModes_AreExplicit(string mode, string? point, bool valid) =>
        Assert.Equal(valid, SpatialGeometryValidation.IsValid(Input(mode: mode, point: point)));

    [Fact]
    public void Metadata_IsRequiredAndBounded()
    {
        var input = Input();
        Assert.True(SpatialGeometryValidation.IsValid(input));
        Assert.False(SpatialGeometryValidation.IsValid(input with { ExpectedRevision = -1 }));
        Assert.False(SpatialGeometryValidation.IsValid(input with { Reason = " " }));
        Assert.False(SpatialGeometryValidation.IsValid(input with { Reason = new string('x', 2001) }));
        Assert.False(SpatialGeometryValidation.IsValid(input with { SourceDescription = " " }));
        Assert.False(SpatialGeometryValidation.IsValid(input with { SourceDescription = new string('x', 1001) }));
    }

    [Fact]
    public void ExcessiveVerticesAndComponents_AreRejected()
    {
        var ring = Enumerable.Repeat(new[] { 28d, -26d }, 10001).ToArray();
        var boundary = JsonSerializer.SerializeToElement(new { type = "Polygon", coordinates = new[] { ring } });
        Assert.False(SpatialGeometryValidation.IsValid(Input() with { Boundary = boundary }));
        var polygons = Enumerable.Repeat(new[] { new[] { new[] { 28d, -26d }, new[] { 29d, -26d }, new[] { 29d, -27d }, new[] { 28d, -26d } } }, 101);
        boundary = JsonSerializer.SerializeToElement(new { type = "MultiPolygon", coordinates = polygons });
        Assert.False(SpatialGeometryValidation.IsValid(Input() with { Boundary = boundary }));
    }
}
