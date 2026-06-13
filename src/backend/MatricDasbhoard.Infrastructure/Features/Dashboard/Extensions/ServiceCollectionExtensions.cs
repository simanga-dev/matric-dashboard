using Microsoft.Extensions.DependencyInjection;
using MatricDasbhoard.Application.Features.Dashboard;
using MatricDasbhoard.Infrastructure.Features.Dashboard.Services;

namespace MatricDasbhoard.Infrastructure.Features.Dashboard.Extensions;

/// <summary>
/// Extension methods for registering dashboard feature services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers dashboard-related services.
    /// </summary>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds dashboard services to the service collection.
        /// </summary>
        public IServiceCollection AddDashboardServices()
        {
            services.AddScoped<IDashboardService, DashboardService>();
            return services;
        }
    }
}
