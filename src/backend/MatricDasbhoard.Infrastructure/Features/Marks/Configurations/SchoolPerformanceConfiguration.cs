using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MatricDasbhoard.Domain.Entities;
using MatricDasbhoard.Infrastructure.Persistence.Configurations;

namespace MatricDasbhoard.Infrastructure.Features.Marks.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="SchoolPerformance"/> entity in the <c>marks</c> schema.
/// </summary>
internal sealed class SchoolPerformanceConfiguration : BaseEntityConfiguration<SchoolPerformance>
{
    /// <inheritdoc />
    protected override void ConfigureEntity(EntityTypeBuilder<SchoolPerformance> builder)
    {
        builder.ToTable("SchoolPerformances", "marks");

        builder.Property(e => e.Year)
            .IsRequired();

        builder.Property(e => e.ProgressedNumber)
            .IsRequired();

        builder.Property(e => e.TotalWrote)
            .IsRequired();

        builder.Property(e => e.TotalAchieved)
            .IsRequired();

        builder.Property(e => e.PercentAchieved)
            .IsRequired()
            .HasPrecision(5, 1);

        // One performance record per school per year
        builder.HasIndex(e => new { e.SchoolId, e.Year })
            .IsUnique()
            .HasDatabaseName("IX_SchoolPerformances_SchoolId_Year");

        // Index for filtering by year
        builder.HasIndex(e => e.Year)
            .HasDatabaseName("IX_SchoolPerformances_Year");
    }
}
