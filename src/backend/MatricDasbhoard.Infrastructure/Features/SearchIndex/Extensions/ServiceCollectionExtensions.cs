using MatricDasbhoard.Application.Features.SearchIndex;
using MatricDasbhoard.Infrastructure.Features.SearchIndex.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MatricDasbhoard.Infrastructure.Features.SearchIndex.Extensions;

/// <summary>Registers school search index maintenance services.</summary>
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Adds the Meilisearch school index publisher.</summary>
        public IServiceCollection AddSchoolSearchIndexServices()
        {
            services.AddScoped<ISchoolSearchIndexService, SchoolSearchIndexService>();
            return services;
        }
    }
}
