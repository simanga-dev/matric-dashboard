using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MatricDasbhoard.Api.Tests.Contracts;
using MatricDasbhoard.Api.Tests.Fixtures;
using MatricDasbhoard.Application.Features.Audit;
using MatricDasbhoard.Application.Features.SearchIndex;
using MatricDasbhoard.Application.Identity.Constants;
using MatricDasbhoard.Shared;

namespace MatricDasbhoard.Api.Tests.Controllers;

public class SearchIndexControllerTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private const string Url = "/api/v1/search-index/schools";
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SearchIndexControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        factory.ResetMocks();
        _client = factory.CreateClient();
    }

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Rebuild_RequiresAuthentication()
    {
        var response = await _client.PostAsync(Url, content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await _factory.SchoolSearchIndexService.DidNotReceive()
            .RebuildAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rebuild_AuthenticatedWithoutPermission_ReturnsForbidden()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Url);
        request.Headers.Add("Authorization", TestAuth.User());

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await _factory.SchoolSearchIndexService.DidNotReceive()
            .RebuildAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rebuild_WithPermission_ReturnsIndexedCounts()
    {
        _factory.SchoolSearchIndexService.RebuildAsync(Arg.Any<CancellationToken>())
            .Returns(Result<SchoolSearchIndexOutput>.Success(new SchoolSearchIndexOutput(8_237, 12, 1_450)));

        using var request = new HttpRequestMessage(HttpMethod.Post, Url);
        request.Headers.Add(
            "Authorization",
            TestAuth.WithPermissions(AppPermissions.SearchIndex.Manage));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SearchIndexRebuildContract>();
        Assert.NotNull(body);
        Assert.Equal(8_237, body.Indexed);
        Assert.Equal(12, body.Removed);
        Assert.Equal(1_450, body.DurationMs);

        await _factory.AuditService.Received(1).LogAsync(
            AuditActions.SearchIndexRebuild,
            Arg.Any<Guid?>(),
            "SearchIndex",
            null,
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rebuild_MeilisearchUnavailable_ReturnsProblemDetails503()
    {
        _factory.SchoolSearchIndexService.RebuildAsync(Arg.Any<CancellationToken>())
            .Returns(Result<SchoolSearchIndexOutput>.Failure(
                ErrorMessages.SearchIndex.Unavailable, ErrorType.ServiceUnavailable));

        using var request = new HttpRequestMessage(HttpMethod.Post, Url);
        request.Headers.Add(
            "Authorization",
            TestAuth.WithPermissions(AppPermissions.SearchIndex.Manage));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(503, problem.GetProperty("status").GetInt32());
    }
}
