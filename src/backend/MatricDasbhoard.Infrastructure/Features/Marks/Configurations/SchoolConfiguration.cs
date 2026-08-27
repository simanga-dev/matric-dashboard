using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MatricDasbhoard.Domain.Entities;
using MatricDasbhoard.Infrastructure.Persistence.Configurations;

namespace MatricDasbhoard.Infrastructure.Features.Marks.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="School"/> entity in the <c>marks</c> schema.
/// </summary>
internal sealed class SchoolConfiguration : BaseEntityConfiguration<School>
{
    /// <inheritdoc />
    protected override void ConfigureEntity(EntityTypeBuilder<School> builder)
    {
        builder.ToTable("Schools", "marks");

        builder.Property(e => e.Province)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.DistrictName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.EmisNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.CentreNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.CentreName)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.Quintile)
            .IsRequired();

        // EMIS number is a stable unique identifier for each school
        builder.HasIndex(e => e.EmisNumber)
            .IsUnique()
            .HasDatabaseName("IX_Schools_EmisNumber");

        // Indexes for common filter/sort paths
        builder.HasIndex(e => e.Province)
            .HasDatabaseName("IX_Schools_Province");

        builder.HasIndex(e => e.DistrictName)
            .HasDatabaseName("IX_Schools_DistrictName");

        // Navigation: a school has many performance records
        builder.HasMany(e => e.Performances)
            .WithOne(e => e.School)
            .HasForeignKey(e => e.SchoolId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
