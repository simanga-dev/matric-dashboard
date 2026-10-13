using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using MatricDasbhoard.WebApi.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace MatricDasbhoard.WebApi.Middlewares;

/// <summary>
/// Proxies allowlisted read-only OGC GET/HEAD requests under <see cref="GeoServerOptions.RoutePrefix"/>.
/// Client credentials and upstream cookies, authentication headers, and redirects are never forwarded.
/// </summary>
/// <remarks>
/// <para>
/// GeoServer is never exposed directly to browsers: the API is the single entry point,
/// matching the pattern used by the sibling glf-core project.
/// </para>
/// <para>
/// Only KVP requests using <c>GET</c> or <c>HEAD</c> are forwarded. XML POST and WMTS REST bindings are unsupported.
/// </para>
/// </remarks>
public class GeoServerProxyMiddleware(
    RequestDelegate next,
    IOptions<GeoServerOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<GeoServerProxyMiddleware> logger)
{
    private const string HttpClientName = "GeoServer";

    private static readonly string[] ConditionalRequestHeaders = ["If-None-Match", "If-Modified-Since"];

    private static readonly HashSet<string> CacheMetadataHeaders =
        new(["Cache-Control", "Expires", "ETag", "Last-Modified", "Date", "Vary"], StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ResponseHeaders =
        new(["Content-Type", "Content-Length", "Content-Encoding", "Content-Language",
            "Cache-Control", "Expires", "ETag", "Last-Modified", "Date", "Vary"], StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ReservedSegments =
        new(["web", "rest", "login", "logout", "j_spring_security_check", "j_spring_security_logout",
            "gwc", "security"], StringComparer.OrdinalIgnoreCase);

    private readonly GeoServerOptions _options = options.Value;

    /// <summary>
    /// Forwards matching requests to GeoServer, or calls the next middleware when
    /// the proxy is disabled or the path does not match the configured prefix.
    /// </summary>
    public async Task Invoke(HttpContext context)
    {
        if (!_options.Enabled
            || string.IsNullOrWhiteSpace(_options.BaseUrl)
            || !context.Request.Path.StartsWithSegments(_options.RoutePrefix, out var remainder))
        {
            await next(context);
            return;
        }

        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            context.Response.Headers.Allow = "GET, HEAD";
            await WriteProblemAsync(context, StatusCodes.Status405MethodNotAllowed, "Method not allowed.");
            return;
        }

        var path = remainder.Value ?? string.Empty;
        var rawPath = context.Features.Get<IHttpRequestFeature>()?.RawTarget?.Split('?')[0] ?? path;
        if (rawPath.IndexOfAny(['%', '\\', ';']) >= 0
            || rawPath.Split('/').Any(segment => segment is "." or "..")
            || path.IndexOfAny(['%', '\\', ';']) >= 0
            || path.Split('/').Any(segment => segment is "." or ".."))
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Invalid GeoServer path.");
            return;
        }

        var endpoint = GetEndpoint(path);
        if (endpoint is null)
        {
            await WriteProblemAsync(context, StatusCodes.Status404NotFound, "GeoServer endpoint not found.");
            return;
        }

        var query = context.Request.Query;
        if (!TryGetSingleParameter(query, "service", out var service)
            || !TryGetSingleParameter(query, "request", out var operation)
            || string.IsNullOrEmpty(operation))
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "A single OGC service and request operation are required.");
            return;
        }

        service = string.IsNullOrEmpty(service) && endpoint != "ows" ? endpoint : service;
        if (!IsReadOnlyOperation(endpoint, service, operation))
        {
            await WriteProblemAsync(context, StatusCodes.Status403Forbidden, "OGC service or operation is not allowed.");
            return;
        }

        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var target = $"{baseUrl}{remainder}{context.Request.QueryString}";

        using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), target);
        foreach (var header in ConditionalRequestHeaders)
        {
            if (context.Request.Headers.TryGetValue(header, out var values))
            {
                request.Headers.TryAddWithoutValidation(header, values.ToArray());
            }
        }

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                context.RequestAborted);

            var notModified = response.StatusCode == System.Net.HttpStatusCode.NotModified;
            if (!response.IsSuccessStatusCode && !notModified)
            {
                var status = (int)response.StatusCode;
                // Redirects and authentication challenges indicate an unexpected upstream endpoint.
                if (status < 400 || status is 401 or 403)
                {
                    status = StatusCodes.Status502BadGateway;
                }

                await WriteProblemAsync(context, status, "GeoServer could not complete the request.");
                return;
            }

            context.Response.StatusCode = (int)response.StatusCode;
            var allowedHeaders = notModified ? CacheMetadataHeaders : ResponseHeaders;

            foreach (var header in response.Headers)
            {
                if (allowedHeaders.Contains(header.Key))
                {
                    context.Response.Headers[header.Key] = header.Value.ToArray();
                }
            }

            foreach (var header in response.Content.Headers)
            {
                if (allowedHeaders.Contains(header.Key))
                {
                    context.Response.Headers[header.Key] = header.Value.ToArray();
                }
            }

            if (!notModified && !HttpMethods.IsHead(context.Request.Method))
            {
                await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
            }
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected - nothing to write.
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or BrokenCircuitException)
        {
            logger.LogError(ex, "GeoServer request to {Target} failed.", target);
            await WriteProblemAsync(context, StatusCodes.Status502BadGateway, "GeoServer is unavailable.");
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutRejectedException)
        {
            logger.LogError(ex, "GeoServer request to {Target} timed out.", target);
            await WriteProblemAsync(context, StatusCodes.Status504GatewayTimeout, "GeoServer request timed out.");
        }
    }

    private static string? GetEndpoint(string path)
    {
        if (path.Equals("/gwc/service/wmts", StringComparison.Ordinal))
        {
            return "wmts";
        }

        var segments = path.Split('/');
        // Global, workspace, and layer virtual services only, with canonical unencoded segments.
        if (segments.Length is < 2 or > 4 || segments[0].Length != 0
            || segments.Skip(1).Any(segment => segment.Length == 0
                || ReservedSegments.Contains(segment)
                || !segment.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
        {
            return null;
        }

        return segments[^1] is "ows" or "wms" or "wfs" ? segments[^1] : null;
    }

    private static bool TryGetSingleParameter(IQueryCollection query, string name, out string value)
    {
        value = string.Empty;
        var parameters = query.Where(pair => pair.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (parameters.Length == 0)
        {
            return true;
        }

        if (parameters.Length != 1 || parameters[0].Value.Count != 1)
        {
            return false;
        }

        value = parameters[0].Value[0] ?? string.Empty;
        return true;
    }

    private static bool IsReadOnlyOperation(string endpoint, string service, string operation)
    {
        service = service.ToLowerInvariant();
        operation = operation.ToLowerInvariant();
        if (endpoint != "ows" && endpoint != service)
        {
            return false;
        }

        return service switch
        {
            "wms" => operation is "getcapabilities" or "getmap" or "getfeatureinfo" or "getlegendgraphic"
                or "describelayer" or "getstyles",
            "wfs" => operation is "getcapabilities" or "describefeaturetype" or "getfeature" or "getpropertyvalue"
                or "liststoredqueries" or "describestoredqueries",
            "wmts" => endpoint == "wmts" && operation is "getcapabilities" or "gettile" or "getfeatureinfo",
            _ => false
        };
    }

    private static async Task WriteProblemAsync(HttpContext context, int status, string detail)
    {
        if (context.Response.HasStarted)
        {
            // A partial OGC response must fail at the transport layer, never look like a complete success.
            context.Abort();
            return;
        }

        context.Response.StatusCode = status;
        foreach (var header in ResponseHeaders)
        {
            context.Response.Headers.Remove(header);
        }

        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = "GeoServer proxy error",
            Detail = detail,
            Instance = context.Request.Path
        }, options: null, contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }
}
