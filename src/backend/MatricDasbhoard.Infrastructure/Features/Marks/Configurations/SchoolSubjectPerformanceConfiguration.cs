using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MatricDasbhoard.Domain.Entities;
using MatricDasbhoard.Infrastructure.Persistence.Configurations;

namespace MatricDasbhoard.Infrastructure.Features.Marks.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="SchoolSubjectPerformance"/> entity in the <c>marks</c> schema.
/// </summary>
internal sealed class SchoolSubjectPerformanceConfiguration : BaseEntityConfiguration<SchoolSubjectPerformance>
{
    /// <inheritdoc />
    protected override void ConfigureEntity(EntityTypeBuilder<SchoolSubjectPerformance> builder)
    {
        builder.ToTable("SchoolSubjectPerformances", "marks");

        builder.Property(e => e.Year)
            .IsRequired();

        builder.Property(e => e.TotalWrote)
            .IsRequired();

        builder.Property(e => e.Achieved)
            .IsRequired(false);

        builder.Property(e => e.PercentAchieved)
            .IsRequired(false)
            .HasPrecision(5, 1);

        // One subject result per school per subject per year
        builder.HasIndex(e => new { e.SchoolId, e.SubjectId, e.Year })
            .IsUnique()
            .HasDatabaseName("IX_SchoolSubjectPerformances_SchoolId_SubjectId_Year");

        // Index for subject-level trends across schools
        builder.HasIndex(e => new { e.SubjectId, e.Year })
            .HasDatabaseName("IX_SchoolSubjectPerformances_SubjectId_Year");

        // Index for filtering by year
        builder.HasIndex(e => e.Year)
            .HasDatabaseName("IX_SchoolSubjectPerformances_Year");
    }
}
