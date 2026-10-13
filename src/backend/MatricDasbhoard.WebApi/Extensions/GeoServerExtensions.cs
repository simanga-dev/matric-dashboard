using MatricDasbhoard.WebApi.Middlewares;
using MatricDasbhoard.WebApi.Options;

namespace MatricDasbhoard.WebApi.Extensions;

/// <summary>
/// Extension methods for registering and applying the GeoServer proxy.
/// </summary>
internal static class GeoServerExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers validated GeoServer options and a cookie-free HTTP client that never follows redirects.
        /// </summary>
        public IServiceCollection AddGeoServer(IConfiguration configuration)
        {
            services.AddOptions<GeoServerOptions>()
                .Bind(configuration.GetSection(GeoServerOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddHttpClient("GeoServer")
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    UseCookies = false,
                    AllowAutoRedirect = false,
                    UseDefaultCredentials = false
                });

            return services;
        }
    }

    extension(IApplicationBuilder app)
    {
        /// <summary>
        /// Adds the read-only proxy after authentication, global rate limiting, and authorization.
        /// </summary>
        public IApplicationBuilder UseGeoServerProxy()
        {
            app.UseMiddleware<GeoServerProxyMiddleware>();

            return app;
        }
    }
}
