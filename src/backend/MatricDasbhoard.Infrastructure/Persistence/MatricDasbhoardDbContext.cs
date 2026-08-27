using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MatricDasbhoard.Domain.Entities;
using MatricDasbhoard.Infrastructure.Features.Audit.Models;
using MatricDasbhoard.Infrastructure.Features.Authentication.Models;
using MatricDasbhoard.Infrastructure.Features.Jobs.Models;
using MatricDasbhoard.Infrastructure.Persistence.Extensions;

namespace MatricDasbhoard.Infrastructure.Persistence;

/// <summary>
/// Application database context.
/// </summary>
internal class MatricDasbhoardDbContext(DbContextOptions<MatricDasbhoardDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    /// <summary>
    /// Gets or sets the refresh tokens table for JWT token rotation.
    /// </summary>
    public DbSet<RefreshToken> RefreshTokens { get; set; }

    /// <summary>
    /// Gets or sets the email tokens table for opaque password-reset and email-verification links.
    /// </summary>
    public DbSet<EmailToken> EmailTokens { get; set; }

    /// <summary>
    /// Gets or sets the paused jobs table for persisting pause state across restarts.
    /// </summary>
    public DbSet<PausedJob> PausedJobs { get; set; }

    /// <summary>
    /// Gets or sets the two-factor authentication challenge tokens for pending 2FA logins.
    /// </summary>
    public DbSet<TwoFactorChallenge> TwoFactorChallenges { get; set; }

    /// <summary>
    /// Gets or sets the external OAuth2 authorization state tokens for pending OAuth flows.
    /// </summary>
    public DbSet<ExternalAuthState> ExternalAuthStates { get; set; }

    /// <summary>
    /// Gets or sets the audit events table for the append-only audit log.
    /// </summary>
    public DbSet<AuditEvent> AuditEvents { get; set; }

    /// <summary>
    /// Gets or sets the external provider configurations for admin-managed OAuth credentials.
    /// </summary>
    public DbSet<ExternalProviderConfig> ExternalProviderConfigs { get; set; }

    /// <summary>
    /// Gets or sets the schools table for matric examination centres.
    /// </summary>
    public DbSet<School> Schools { get; set; }

    /// <summary>
    /// Gets or sets the school performance records table for yearly NSC results.
    /// </summary>
    public DbSet<SchoolPerformance> SchoolPerformances { get; set; }

    /// <summary>
    /// Configures the model by applying all entity configurations from this assembly
    /// and fuzzy search extensions.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MatricDasbhoardDbContext).Assembly);
        modelBuilder.ApplyAuthSchema();
        modelBuilder.ApplyFuzzySearch();
    }
}
