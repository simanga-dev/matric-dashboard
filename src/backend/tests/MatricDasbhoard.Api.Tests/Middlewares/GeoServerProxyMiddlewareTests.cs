using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MatricDasbhoard.WebApi.Middlewares;
using MatricDasbhoard.WebApi.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace MatricDasbhoard.Api.Tests.Middlewares;

/// <summary>Verifies the proxy's read-only boundary and isolation of upstream credentials.</summary>
public class GeoServerProxyMiddlewareTests
{
    [Theory]
    [InlineData("/wfs", "service=WFS&request=GetFeature&typeName=schools:school_locations")]
    [InlineData("/ows", "SERVICE=WFS&REQUEST=DescribeFeatureType")]
    [InlineData("/schools/ows", "service=WFS&request=GetCapabilities")]
    [InlineData("/schools/school_locations/wfs", "request=GetPropertyValue")]
    [InlineData("/wfs", "request=ListStoredQueries")]
    [InlineData("/wfs", "request=DescribeStoredQueries")]
    [InlineData("/wms", "service=WMS&request=GetMap")]
    [InlineData("/schools/wms", "request=GetFeatureInfo")]
    [InlineData("/schools/school_locations/wms", "request=GetLegendGraphic")]
    [InlineData("/wms", "request=DescribeLayer")]
    [InlineData("/wms", "request=GetStyles")]
    [InlineData("/gwc/service/wmts", "service=WMTS&request=GetTile")]
    [InlineData("/gwc/service/wmts", "request=GetCapabilities")]
    [InlineData("/gwc/service/wmts", "request=GetFeatureInfo")]
    public async Task ReadOnlyRequest_ForwardsCanonicalTargetWithoutCredentials(string path, string query)
    {
        using var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var context = CreateContext(path, query);
        context.Request.Headers.Cookie = "session=private";
        context.Request.Headers.Authorization = "Bearer private";
        context.Request.Headers["X-Api-Key"] = "private";

        await CreateMiddleware(client).Invoke(context);

        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal($"http://geoserver:8080/geoserver{path}?{query}", handler.Target?.OriginalString);
        Assert.Empty(handler.RequestHeaders);
        Assert.False(handler.HasBody);
        Assert.Equal("application/json; charset=utf-8", context.Response.ContentType);
        Assert.Equal("\"layer-v1\"", context.Response.Headers.ETag);
        Assert.False(context.Response.Headers.ContainsKey("Set-Cookie"));
        Assert.False(context.Response.Headers.ContainsKey("WWW-Authenticate"));
        Assert.False(context.Response.Headers.ContainsKey("Location"));
        Assert.False(context.Response.Headers.ContainsKey("Access-Control-Allow-Origin"));
    }

    [Theory]
    [InlineData("/web")]
    [InlineData("/web/wfs")]
    [InlineData("/rest/workspaces")]
    [InlineData("/rest/wfs")]
    [InlineData("/login")]
    [InlineData("/j_spring_security_check")]
    [InlineData("/gwc/rest/layers")]
    [InlineData("/gwc/service/wms")]
    [InlineData("/gwc/service/wmts/layer/default/matrix/0/0/0.png")]
    [InlineData("/unknown")]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("//wfs")]
    [InlineData("/wfs/")]
    [InlineData("/a/b/c/wfs")]
    public async Task UnknownOrAdministrativePath_Returns404WithoutContactingUpstream(string path)
    {
        await AssertDeniedAsync(path, "service=WFS&request=GetFeature", "GET", 404);
    }

    [Theory]
    [InlineData("/wfs/../rest")]
    [InlineData("/../wfs")]
    [InlineData("/schools/./wfs")]
    [InlineData("/%2e%2e/wfs")]
    [InlineData("/%252e%252e/wfs")]
    [InlineData("/schools%2fwfs")]
    [InlineData("/schools\\wfs")]
    [InlineData("/wfs;rest")]
    public async Task TraversalOrAmbiguousPath_Returns400WithoutContactingUpstream(string path)
    {
        await AssertDeniedAsync(path, "service=WFS&request=GetFeature", "GET", 400);
    }

    [Theory]
    [InlineData("/geoserver/%77fs?request=GetFeature")]
    [InlineData("/geoserver/rest/../wfs?request=GetFeature")]
    [InlineData("/geoserver/./wfs?request=GetFeature")]
    public async Task AmbiguousRawPath_Returns400EvenWhenDecodedPathIsAllowlisted(string rawTarget)
    {
        using var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var context = CreateContext("/wfs", "request=GetFeature");
        var feature = Assert.IsAssignableFrom<IHttpRequestFeature>(context.Features.Get<IHttpRequestFeature>());
        feature.RawTarget = rawTarget;

        await CreateMiddleware(client).Invoke(context);

        await AssertProblemAsync(context, 400);
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("service=WFS&request=Transaction", 403)]
    [InlineData("service=WFS&request=LockFeature", 403)]
    [InlineData("service=WFS&request=GetFeatureWithLock", 403)]
    [InlineData("service=WFS&request=CreateStoredQuery", 403)]
    [InlineData("service=WFS&request=DropStoredQuery", 403)]
    [InlineData("service=WMS&request=GetMap", 403)]
    [InlineData("service=WPS&request=Execute", 403)]
    [InlineData("service=WFS&request=GetFeature&REQUEST=Transaction", 400)]
    [InlineData("service=WFS&service=WMS&request=GetFeature", 400)]
    [InlineData("service=WFS&request=", 400)]
    [InlineData("service=WFS", 400)]
    public async Task WriteOrAmbiguousOperation_IsRejected(string query, int status)
    {
        await AssertDeniedAsync("/wfs", query, "GET", status);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    [InlineData("OPTIONS")]
    public async Task UnsupportedMethod_Returns405AndAllowHeader(string method)
    {
        await AssertDeniedAsync("/wfs", "service=WFS&request=GetFeature", method, 405);
    }

    [Fact]
    public async Task Head_ForwardsHeadWithoutResponseBody()
    {
        using var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var context = CreateContext("/wfs", "request=GetFeature", "HEAD");

        await CreateMiddleware(client).Invoke(context);

        Assert.Equal(HttpMethod.Head, handler.Method);
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    public async Task ConditionalRequest_ForwardsOnlyValidatorsAndReturns304MetadataWithoutBody(string method)
    {
        using var handler = new RecordingHandler(HttpStatusCode.NotModified);
        using var client = new HttpClient(handler);
        var context = CreateContext("/wfs", "request=GetFeature", method);
        context.Request.Headers.IfNoneMatch = "\"layer-v1\"";
        context.Request.Headers.IfModifiedSince = "Mon, 12 Oct 2026 12:00:00 GMT";
        context.Request.Headers.IfMatch = "*";
        context.Request.Headers.IfUnmodifiedSince = "Mon, 12 Oct 2026 12:00:00 GMT";
        context.Request.Headers.Cookie = "session=private";
        context.Request.Headers.Authorization = "Bearer private";
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.10";

        await CreateMiddleware(client).Invoke(context);

        Assert.Equal(304, context.Response.StatusCode);
        Assert.Equal(["If-Modified-Since", "If-None-Match"], handler.RequestHeaders.Order().ToArray());
        Assert.Equal("\"layer-v1\"", handler.ConditionalETag);
        Assert.Equal("Mon, 12 Oct 2026 12:00:00 GMT", handler.ConditionalDate);
        Assert.Equal("\"layer-v1\"", context.Response.Headers.ETag);
        Assert.Equal("public, max-age=60", context.Response.Headers.CacheControl);
        Assert.Equal("Mon, 12 Oct 2026 12:00:00 GMT", context.Response.Headers.LastModified);
        Assert.Equal("Accept-Encoding", context.Response.Headers.Vary);
        Assert.Equal("Tue, 13 Oct 2026 12:00:00 GMT", context.Response.Headers.Date);
        Assert.Null(context.Response.ContentType);
        Assert.Null(context.Response.ContentLength);
        Assert.Equal(0, context.Response.Body.Length);
        Assert.False(context.Response.Headers.ContainsKey("Set-Cookie"));
        Assert.False(context.Response.Headers.ContainsKey("WWW-Authenticate"));
        Assert.False(context.Response.Headers.ContainsKey("Location"));
    }

    [Theory]
    [InlineData(HttpStatusCode.Found, 502)]
    [InlineData(HttpStatusCode.TemporaryRedirect, 502)]
    [InlineData(HttpStatusCode.Unauthorized, 502)]
    [InlineData(HttpStatusCode.Forbidden, 502)]
    [InlineData(HttpStatusCode.NotFound, 404)]
    [InlineData(HttpStatusCode.BadRequest, 400)]
    [InlineData(HttpStatusCode.InternalServerError, 500)]
    public async Task UpstreamErrors_ReturnProblemWithoutAuthenticationOrRedirectHeaders(HttpStatusCode upstream, int status)
    {
        using var handler = new RecordingHandler(upstream);
        using var client = new HttpClient(handler);
        var context = CreateContext("/wfs", "request=GetFeature");

        await CreateMiddleware(client).Invoke(context);

        await AssertProblemAsync(context, status);
        Assert.False(context.Response.Headers.ContainsKey("Set-Cookie"));
        Assert.False(context.Response.Headers.ContainsKey("WWW-Authenticate"));
        Assert.False(context.Response.Headers.ContainsKey("Location"));
    }

    [Theory]
    [InlineData(false, 502)]
    [InlineData(true, 504)]
    public async Task UpstreamConnectionFailureOrTimeout_ReturnsGatewayProblem(bool timeout, int status)
    {
        using var handler = new FailingHandler(timeout);
        using var client = new HttpClient(handler);
        var context = CreateContext("/wfs", "request=GetFeature");

        await CreateMiddleware(client).Invoke(context);

        await AssertProblemAsync(context, status);
    }

    [Theory]
    [InlineData(true, 504)]
    [InlineData(false, 502)]
    public async Task PollyTimeoutOrBrokenCircuit_ReturnsGatewayProblem(bool timeout, int status)
    {
        using var handler = new ResilienceFailingHandler(timeout);
        using var client = new HttpClient(handler);
        var context = CreateContext("/wfs", "request=GetFeature");

        await CreateMiddleware(client).Invoke(context);

        await AssertProblemAsync(context, status);
    }

    [Theory]
    [InlineData(false, "/geoserver/wfs")]
    [InlineData(true, "/other/wfs")]
    [InlineData(true, "/geoserver-other/wfs")]
    public async Task DisabledOrUnmatchedProxy_PassesToNext(bool enabled, string path)
    {
        using var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var context = CreateContext("/wfs", "request=GetFeature");
        context.Request.Path = path;
        var reachedNext = false;

        await CreateMiddleware(client, enabled, _ =>
        {
            reachedNext = true;
            return Task.CompletedTask;
        }).Invoke(context);

        Assert.True(reachedNext);
        Assert.Equal(0, handler.Calls);
    }

    private static async Task AssertDeniedAsync(string path, string query, string method, int status)
    {
        using var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var context = CreateContext(path, query, method);

        await CreateMiddleware(client).Invoke(context);

        await AssertProblemAsync(context, status);
        Assert.Equal(0, handler.Calls);
        if (status == 405)
        {
            Assert.Equal("GET, HEAD", context.Response.Headers.Allow);
        }
    }

    private static async Task AssertProblemAsync(HttpContext context, int status)
    {
        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        context.Response.Body.Position = 0;
        var problem = await JsonSerializer.DeserializeAsync<ProblemDetails>(context.Response.Body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(problem);
        Assert.Equal(status, problem.Status);
        Assert.Equal(context.Request.Path.ToString(), problem.Instance);
    }

    private static DefaultHttpContext CreateContext(string path, string query, string method = "GET")
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/geoserver" + path;
        context.Request.QueryString = new QueryString("?" + query);
        context.Request.Method = method;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static GeoServerProxyMiddleware CreateMiddleware(HttpClient client, bool enabled = true,
        RequestDelegate? next = null)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("GeoServer").Returns(client);
        return new GeoServerProxyMiddleware(next ?? (_ => Task.CompletedTask),
            Options.Create(new GeoServerOptions { Enabled = enabled, BaseUrl = "http://geoserver:8080/geoserver" }),
            factory, NullLogger<GeoServerProxyMiddleware>.Instance);
    }

    private sealed class RecordingHandler(HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public Uri? Target { get; private set; }
        public HttpMethod? Method { get; private set; }
        public string[] RequestHeaders { get; private set; } = [];
        public bool HasBody { get; private set; }
        public string? ConditionalETag { get; private set; }
        public string? ConditionalDate { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Target = request.RequestUri;
            Method = request.Method;
            RequestHeaders = request.Headers.Select(header => header.Key).ToArray();
            ConditionalETag = request.Headers.TryGetValues("If-None-Match", out var etag) ? string.Join(", ", etag) : null;
            ConditionalDate = request.Headers.TryGetValues("If-Modified-Since", out var date) ? string.Join(", ", date) : null;
            HasBody = request.Content is not null;
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            };
            response.Headers.TryAddWithoutValidation("Set-Cookie", "JSESSIONID=upstream; Path=/");
            response.Headers.TryAddWithoutValidation("WWW-Authenticate", "Basic realm=GeoServer");
            response.Headers.TryAddWithoutValidation("Location", "http://geoserver:8080/geoserver/web");
            response.Headers.TryAddWithoutValidation("Access-Control-Allow-Origin", "*");
            response.Headers.TryAddWithoutValidation("ETag", "\"layer-v1\"");
            response.Headers.TryAddWithoutValidation("Cache-Control", "public, max-age=60");
            response.Headers.TryAddWithoutValidation("Date", "Tue, 13 Oct 2026 12:00:00 GMT");
            response.Headers.TryAddWithoutValidation("Vary", "Accept-Encoding");
            response.Content.Headers.TryAddWithoutValidation("Last-Modified", "Mon, 12 Oct 2026 12:00:00 GMT");
            return Task.FromResult(response);
        }
    }

    private sealed class FailingHandler(bool timeout) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(timeout
                ? new TaskCanceledException("Upstream timed out")
                : new HttpRequestException("Upstream connection failed"));
    }

    private sealed class ResilienceFailingHandler(bool timeout) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(timeout
                ? new TimeoutRejectedException("Upstream timed out")
                : new BrokenCircuitException("Upstream circuit is open"));
    }
}
