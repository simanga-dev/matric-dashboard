using System.ComponentModel.DataAnnotations;

namespace MatricDasbhoard.WebApi.Options;

/// <summary>
/// Configuration for the GeoServer proxy. When enabled, requests under
/// <see cref="RoutePrefix"/> are forwarded to <see cref="BaseUrl"/> so the
/// spatial school layer can be served through the API instead of exposing
/// GeoServer directly to clients.
/// Maps to the "GeoServer" section in appsettings.json.
/// </summary>
public sealed class GeoServerOptions : IValidatableObject
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "GeoServer";

    /// <summary>
    /// Gets a value indicating whether the GeoServer proxy is active.
    /// When <c>false</c> the proxy passes requests through untouched.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Gets the absolute HTTP(S) base URL without credentials, query, or fragment, e.g.
    /// <c>http://geoserver:8080/geoserver</c>. Required when <see cref="Enabled"/> is <c>true</c>.
    /// </summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// Gets a non-root path prefix without a trailing slash (e.g. <c>/geoserver</c>).
    /// </summary>
    public string RoutePrefix { get; init; } = "/geoserver";

    /// <summary>Validates the upstream URL and route prefix when the proxy is enabled.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enabled)
        {
            yield break;
        }

        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || string.IsNullOrEmpty(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment)
            || BaseUrl.Any(char.IsWhiteSpace)
            || BaseUrl.IndexOfAny(['%', '\\', '?', '#']) >= 0
            || BaseUrl.Contains("..", StringComparison.Ordinal)
            || BaseUrl.Split('/').Any(segment => segment == ".")
            || uri.AbsolutePath.Contains("//", StringComparison.Ordinal)
            || !IsSafePath(uri.AbsolutePath.TrimEnd('/')))
        {
            yield return new ValidationResult(
                "GeoServer:BaseUrl must be an absolute HTTP(S) URL without credentials, query, fragment, or encoded/traversal paths.",
                [nameof(BaseUrl)]);
        }

        if (string.IsNullOrEmpty(RoutePrefix)
            || !RoutePrefix.StartsWith('/')
            || RoutePrefix.EndsWith('/')
            || !IsSafePath(RoutePrefix))
        {
            yield return new ValidationResult(
                "GeoServer:RoutePrefix must be a non-root absolute path of letters, digits, hyphens, or underscores, without a trailing slash.",
                [nameof(RoutePrefix)]);
        }
    }

    private static bool IsSafePath(string path) => path.Length == 0
        || path.StartsWith('/') && path[1..].Split('/').All(segment => segment.Length > 0
            && segment.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'));
}
