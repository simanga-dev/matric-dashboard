using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MatricDasbhoard.Domain.Entities;
using MatricDasbhoard.Infrastructure.Persistence.Configurations;

namespace MatricDasbhoard.Infrastructure.Features.Marks.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="SchoolEnrolment"/> entity in the <c>marks</c> schema.
/// </summary>
internal sealed class SchoolEnrolmentConfiguration : BaseEntityConfiguration<SchoolEnrolment>
{
    /// <inheritdoc />
    protected override void ConfigureEntity(EntityTypeBuilder<SchoolEnrolment> builder)
    {
        builder.ToTable("SchoolEnrolments", "marks");

        builder.Property(e => e.Year)
            .IsRequired();

        builder.Property(e => e.Learners)
            .IsRequired(false);

        builder.Property(e => e.Educators)
            .IsRequired(false);

        // One enrolment record per school per year
        builder.HasIndex(e => new { e.SchoolId, e.Year })
            .IsUnique()
            .HasDatabaseName("IX_SchoolEnrolments_SchoolId_Year");

        // Index for filtering by year
        builder.HasIndex(e => e.Year)
            .HasDatabaseName("IX_SchoolEnrolments_Year");
    }
}
