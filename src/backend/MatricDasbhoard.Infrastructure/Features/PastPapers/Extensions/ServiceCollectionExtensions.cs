using Microsoft.Extensions.DependencyInjection;
using MatricDasbhoard.Application.Features.PastPapers;
using MatricDasbhoard.Infrastructure.Features.PastPapers.Services;

namespace MatricDasbhoard.Infrastructure.Features.PastPapers.Extensions;

/// <summary>
/// Extension methods for registering past paper catalog services.
/// </summary>
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds the past paper catalog service. Requires file storage and caching to be registered.
        /// </summary>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection AddPastPaperServices()
        {
            services.AddScoped<IPastPaperService, PastPaperService>();
            return services;
        }
    }
}
