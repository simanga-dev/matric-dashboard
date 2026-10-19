using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MatricDasbhoard.Api.Tests.Contracts;
using MatricDasbhoard.Api.Tests.Fixtures;
using MatricDasbhoard.Application.Features.SchoolSpatial;
using MatricDasbhoard.Application.Identity.Constants;
using MatricDasbhoard.Shared;

namespace MatricDasbhoard.Api.Tests.Controllers;

public class SchoolSpatialControllerTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private const string Url = "/api/v1/schools/001234567/spatial";
    public SchoolSpatialControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        factory.ResetMocks();
        _client = factory.CreateClient();
    }
    public void Dispose() => _client.Dispose();

    private static object Body(string reason = "Correct grounds") => new
    {
        expectedRevision = 0,
        boundary = (object?)null,
        locationMode = "imported",
        location = (object?)null,
        reason,
        sourceDescription = "Field visit"
    };

    [Theory]
    [InlineData(false, false, HttpStatusCode.Unauthorized)]
    [InlineData(true, false, HttpStatusCode.Forbidden)]
    [InlineData(false, true, HttpStatusCode.Unauthorized)]
    [InlineData(true, true, HttpStatusCode.Forbidden)]
    public async Task WriteAndHistory_RequireExplicitPermission(bool authenticated, bool history, HttpStatusCode status)
    {
        using var request = new HttpRequestMessage(history ? HttpMethod.Get : HttpMethod.Put, history ? Url + "/history" : Url);
        if (authenticated) request.Headers.Add("Authorization", TestAuth.User());
        if (!history) request.Content = JsonContent.Create(Body());
        var response = await _client.SendAsync(request);
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        await _factory.SchoolSpatialService.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<SaveSpatialInput>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_IsPublicAndPreservesStringEmis()
    {
        _factory.SchoolSpatialService.GetAsync("001234567", Arg.Any<CancellationToken>()).Returns(
            Result<SpatialStateOutput>.Success(new("001234567", null, null, false, "missing", 0, null, [])));
        var response = await _client.GetAsync(Url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SpatialStateContract>();
        Assert.NotNull(body);
        Assert.Equal("001234567", body.EmisNumber);
        Assert.Equal("missing", body.LocationSource);
        Assert.False(body.HasLocationOverride);
        Assert.Equal(0, body.Revision);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(8, json.EnumerateObject().Count());
        Assert.False(json.TryGetProperty("reason", out _));
    }

    [Fact]
    public async Task Save_EditorConflict_ReturnsProblemDetails409()
    {
        _factory.SchoolSpatialService.SaveAsync("001234567", Arg.Any<SaveSpatialInput>(), Arg.Any<CancellationToken>())
            .Returns(Result<SpatialStateOutput>.Failure(SpatialErrors.Conflict, ErrorType.Conflict));
        using var request = new HttpRequestMessage(HttpMethod.Put, Url) { Content = JsonContent.Create(Body()) };
        request.Headers.Add("Authorization", TestAuth.WithPermissions(AppPermissions.Schools.SpatialEdit));
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(409, problem.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Save_InvalidMetadata_StopsBeforeService()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, Url) { Content = JsonContent.Create(Body(" ")) };
        request.Headers.Add("Authorization", TestAuth.WithPermissions(AppPermissions.Schools.SpatialEdit));
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await _factory.SchoolSpatialService.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<SaveSpatialInput>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OpenApi_DeclaresSpatialContractAndConflict()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var path = document.RootElement.GetProperty("paths").GetProperty("/api/v1/schools/{emis}/spatial");
        Assert.True(path.TryGetProperty("get", out _));
        Assert.True(path.GetProperty("put").GetProperty("responses").TryGetProperty("409", out _));
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        Assert.True(schemas.TryGetProperty("SaveSpatialRequest", out _));
        Assert.True(schemas.TryGetProperty("SpatialStateOutput", out _));
        // Optional artifact export uses the in-memory HTTP host, never starts application DB migrations.
        if (Environment.GetEnvironmentVariable("MATRIC_OPENAPI_EXPORT") is { Length: > 0 } export)
            await File.WriteAllTextAsync(export, json);
    }

    [Fact]
    public async Task History_Editor_ReceivesBoundedArray()
    {
        _factory.SchoolSpatialService.GetHistoryAsync("001234567", Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<SpatialHistoryOutput>>.Success([]));
        using var request = new HttpRequestMessage(HttpMethod.Get, Url + "/history");
        request.Headers.Add("Authorization", TestAuth.WithPermissions(AppPermissions.Schools.SpatialEdit));
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", await response.Content.ReadAsStringAsync());
    }
}
