using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MatricDasbhoard.Application.Persistence;
using MatricDasbhoard.Infrastructure.Features.Authentication.Extensions;
using MatricDasbhoard.Infrastructure.Persistence.Interceptors;

namespace MatricDasbhoard.Infrastructure.Persistence.Extensions;

/// <summary>
/// Extension methods for registering persistence services (DbContext and repositories).
/// </summary>
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the database context and generic repository.
        /// </summary>
        /// <param name="configuration">The application configuration for reading connection strings.</param>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection AddPersistence(IConfiguration configuration)
        {
            services.ConfigureDbContext(configuration);
            services.AddScoped(typeof(IBaseEntityRepository<>), typeof(BaseEntityRepository<>));

            return services;
        }

        /// <summary>
        /// Registers ASP.NET Identity, JWT authentication, and authentication services.
        /// </summary>
        /// <param name="configuration">The application configuration for reading auth options.</param>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection AddIdentityServices(IConfiguration configuration)
        {
            services.AddIdentity<MatricDasbhoardDbContext>(configuration);

            return services;
        }

        private IServiceCollection ConfigureDbContext(IConfiguration configuration)
        {
            services.AddScoped<AuditingInterceptor>();
            services.AddScoped<UserCacheInvalidationInterceptor>();
            services.AddDbContext<MatricDasbhoardDbContext>((sp, opt) =>
            {
                var connectionString = configuration.GetConnectionString("Database");
                opt.UseNpgsql(connectionString, npgsqlOptions =>
                    npgsqlOptions.EnableRetryOnFailure());
                opt.ConfigureWarnings(w =>
                    w.Log((RelationalEventId.CommandError, LogLevel.Warning)));
                opt.AddInterceptors(
                    sp.GetRequiredService<AuditingInterceptor>(),
                    sp.GetRequiredService<UserCacheInvalidationInterceptor>());
            });
            return services;
        }
    }
}
