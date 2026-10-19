using MatricDasbhoard.Application.Features.SchoolSpatial;
using MatricDasbhoard.Infrastructure.Features.SchoolSpatial.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MatricDasbhoard.Infrastructure.Features.SchoolSpatial.Extensions;

/// <summary>Registers school spatial persistence services.</summary>
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Adds parameterized PostGIS persistence.</summary>
        public IServiceCollection AddSchoolSpatialServices()
        {
            services.AddScoped<ISchoolSpatialService, SchoolSpatialService>();
            return services;
        }
    }
}
