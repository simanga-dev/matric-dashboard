using System.Net;
using System.Text;
using MatricDasbhoard.Component.Tests.Fixtures;
using MatricDasbhoard.Domain.Entities;
using MatricDasbhoard.Infrastructure.Features.SearchIndex.Services;
using MatricDasbhoard.Shared;
using Meilisearch;
using Microsoft.Extensions.Logging.Abstractions;

namespace MatricDasbhoard.Component.Tests.Services;

/// <summary>Coverage for the Meilisearch school directory rebuild.</summary>
public class SchoolSearchIndexServiceTests
{
    /// <summary>An empty projection must not delete the live index.</summary>
    [Fact]
    public async Task Rebuild_EmptyDatabase_DoesNotUploadOrDelete()
    {
        await using var db = TestDbContextFactory.Create();
        var handler = new FakeMeilisearch();
        var service = CreateService(db, handler);

        var result = await service.RebuildAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.Indexed);
        Assert.Equal(0, result.Value.Removed);
        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Post && r.Path.Contains("delete-batch"));
        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Post && r.Path.Contains("/documents"));
    }

    /// <summary>Every school is uploaded with the index primary key and stale documents are removed.</summary>
    [Fact]
    public async Task Rebuild_PublishesSchoolsAndRemovesStaleDocuments()
    {
        await using var db = TestDbContextFactory.Create();
        var school = new School("GAUTENG", "District", "100", "100", "Alpha High", 3);
        school.AddPerformance(new SchoolPerformance(2024, 0, 10, 8, 80m));
        db.Schools.Add(school);
        await db.SaveChangesAsync();

        var handler = new FakeMeilisearch(existingDocumentIds: ["100", "999"]);
        var service = CreateService(db, handler);

        var result = await service.RebuildAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Indexed);
        Assert.Equal(1, result.Value.Removed);

        var upload = Assert.Single(
            handler.Requests,
            r => r.Method == HttpMethod.Post && r.Path.Contains("/documents?"));
        Assert.Contains("primaryKey=id", upload.Path);
        Assert.Contains("\"school_id\":\"100\"", upload.Body);
        Assert.Contains("\"id\":\"100\"", upload.Body);

        var delete = Assert.Single(handler.Requests, r => r.Path.Contains("delete-batch"));
        Assert.Equal("[\"999\"]", delete.Body);
    }

    /// <summary>A failed Meilisearch task is reported as a rebuild failure.</summary>
    [Fact]
    public async Task Rebuild_FailedTask_ReturnsServiceUnavailable()
    {
        await using var db = TestDbContextFactory.Create();
        db.Schools.Add(new School("GAUTENG", "District", "100", "100", "Alpha High", 3));
        await db.SaveChangesAsync();

        var handler = new FakeMeilisearch(taskStatus: "failed");
        var service = CreateService(db, handler);

        var result = await service.RebuildAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorMessages.SearchIndex.RebuildFailed, result.Error);
        Assert.Equal(ErrorType.ServiceUnavailable, result.ErrorType);
    }

    /// <summary>A task timeout is a service outage, not an unhandled server error.</summary>
    [Fact]
    public async Task Rebuild_TaskTimeout_ReturnsServiceUnavailable()
    {
        await using var db = TestDbContextFactory.Create();
        db.Schools.Add(new School("GAUTENG", "District", "100", "100", "Alpha High", 3));
        await db.SaveChangesAsync();

        var handler = new FakeMeilisearch(throwTimeout: true);
        var service = CreateService(db, handler);

        var result = await service.RebuildAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorMessages.SearchIndex.Unavailable, result.Error);
        Assert.Equal(ErrorType.ServiceUnavailable, result.ErrorType);
    }

    private static SchoolSearchIndexService CreateService(
        MatricDasbhoard.Infrastructure.Persistence.MatricDasbhoardDbContext db,
        FakeMeilisearch handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://search.test/") };
        return new SchoolSearchIndexService(db, new MeilisearchClient(http), NullLogger<SchoolSearchIndexService>.Instance);
    }

    /// <summary>Records requests and answers the Meilisearch endpoints the rebuild uses.</summary>
    private sealed class FakeMeilisearch(
        IReadOnlyList<string>? existingDocumentIds = null,
        string taskStatus = "succeeded",
        bool throwTimeout = false) : HttpMessageHandler
    {
        private readonly List<Request> _requests = [];

        public IReadOnlyList<Request> Requests => _requests;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.PathAndQuery ?? string.Empty;
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            _requests.Add(new Request(request.Method, path, body));

            if (path.StartsWith("/tasks/", StringComparison.Ordinal))
            {
                if (throwTimeout)
                {
                    throw new MeilisearchTimeoutError("The task timed out.");
                }

                return Json($$"""
                    {"uid":1,"status":"{{taskStatus}}","type":"settingsUpdate","enqueuedAt":"2026-01-01T00:00:00Z"}
                    """);
            }

            if (path.Contains("delete-batch", StringComparison.Ordinal))
            {
                return Json("""{"taskUid":3}""");
            }

            if (request.Method == HttpMethod.Get && path.StartsWith("/indexes/schools/documents", StringComparison.Ordinal))
            {
                var ids = existingDocumentIds ?? [];
                var results = string.Join(',', ids.Select(id => $$"""{"id":"{{id}}"}"""));
                return Json($$"""
                    {"results":[{{results}}],"offset":0,"limit":1000,"total":{{ids.Count}}}
                    """);
            }

            if (request.Method == HttpMethod.Post && path.StartsWith("/indexes/schools/documents", StringComparison.Ordinal))
            {
                return Json("""{"taskUid":2}""");
            }

            return Json("""{"taskUid":1}""");
        }

        private static HttpResponseMessage Json(string json) =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
    }

    private sealed record Request(HttpMethod Method, string Path, string Body);
}
