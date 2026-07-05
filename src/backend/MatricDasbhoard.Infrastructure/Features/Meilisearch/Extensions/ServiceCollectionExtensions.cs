using Meilisearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MatricDasbhoard.Infrastructure.Features.Meilisearch.Extensions;

/// <summary>
/// Extension methods for registering Meilisearch services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds a singleton <see cref="MeilisearchClient"/> configured from
    /// <c>MEILI_URL</c> and <c>MEILI_MASTER_KEY</c> environment variables
    /// (injected by Aspire in development and platform env vars in production).
    /// </summary>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers Meilisearch client services.
        /// </summary>
        public IServiceCollection AddMeilisearchServices()
        {
            services.AddSingleton(serviceProvider =>
            {
                var configuration = serviceProvider.GetRequiredService<IConfiguration>();
                var url = configuration["MEILI_URL"] ?? "http://localhost:7700";
                var masterKey = configuration["MEILI_MASTER_KEY"] ?? string.Empty;

                return new MeilisearchClient(url, masterKey);
            });

            return services;
        }
    }
}
