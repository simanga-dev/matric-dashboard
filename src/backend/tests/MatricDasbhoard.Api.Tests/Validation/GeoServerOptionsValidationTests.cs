using System.ComponentModel.DataAnnotations;
using MatricDasbhoard.WebApi.Options;

namespace MatricDasbhoard.Api.Tests.Validation;

/// <summary>Checks that proxy configuration cannot introduce ambiguous routes or credentialed upstreams.</summary>
public class GeoServerOptionsValidationTests
{
    [Theory]
    [InlineData("http://geoserver:8080/geoserver", "/geoserver")]
    [InlineData("https://geo.example.com/geoserver/", "/api/geo")]
    [InlineData("http://localhost:8080", "/geo_proxy")]
    public void ValidConfiguration_IsAccepted(string baseUrl, string prefix)
    {
        Assert.True(IsValid(baseUrl, prefix));
    }

    [Theory]
    [InlineData("")]
    [InlineData("/geoserver")]
    [InlineData("ftp://geo.example.com/geoserver")]
    [InlineData("file:///geoserver")]
    [InlineData("https://user:password@geo.example.com/geoserver")]
    [InlineData("https://geo.example.com/geoserver?request=Transaction")]
    [InlineData("https://geo.example.com/geoserver#fragment")]
    [InlineData("https://geo.example.com/a/../geoserver")]
    [InlineData("https://geo.example.com/geoserver/.")]
    [InlineData("https://geo.example.com/geoserver//")]
    [InlineData("https://geo.example.com/%2e%2e/geoserver")]
    [InlineData("https://geo.example.com/a\\geoserver")]
    [InlineData(" https://geo.example.com/geoserver")]
    public void InvalidBaseUrl_IsRejected(string baseUrl)
    {
        Assert.False(IsValid(baseUrl, "/geoserver"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("geoserver")]
    [InlineData("/geoserver/")]
    [InlineData("//geoserver")]
    [InlineData("/a/../geoserver")]
    [InlineData("/%67eoserver")]
    [InlineData("/geoserver?query")]
    [InlineData("/geoserver#fragment")]
    [InlineData("/geo server")]
    public void InvalidPrefix_IsRejected(string prefix)
    {
        Assert.False(IsValid("http://geoserver:8080/geoserver", prefix));
    }

    [Fact]
    public void DisabledProxy_DoesNotRequireUpstreamConfiguration()
    {
        var options = new GeoServerOptions();
        Assert.True(Validator.TryValidateObject(options, new ValidationContext(options), [], true));
    }

    private static bool IsValid(string baseUrl, string prefix)
    {
        var options = new GeoServerOptions { Enabled = true, BaseUrl = baseUrl, RoutePrefix = prefix };
        return Validator.TryValidateObject(options, new ValidationContext(options), [], true);
    }
}
