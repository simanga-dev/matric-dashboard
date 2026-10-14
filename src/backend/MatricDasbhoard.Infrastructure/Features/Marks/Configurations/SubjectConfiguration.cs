using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MatricDasbhoard.Domain.Entities;
using MatricDasbhoard.Infrastructure.Persistence.Configurations;

namespace MatricDasbhoard.Infrastructure.Features.Marks.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="Subject"/> entity in the <c>marks</c> schema.
/// </summary>
internal sealed class SubjectConfiguration : BaseEntityConfiguration<Subject>
{
    /// <inheritdoc />
    protected override void ConfigureEntity(EntityTypeBuilder<Subject> builder)
    {
        builder.ToTable("Subjects", "marks");

        builder.Property(e => e.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.IsGateway)
            .IsRequired();

        // Subject code is a stable unique identifier used by the importer upsert
        builder.HasIndex(e => e.Code)
            .IsUnique();

        // Navigation: a subject has many per-school performance records
        builder.HasMany(e => e.Performances)
            .WithOne(e => e.Subject)
            .HasForeignKey(e => e.SubjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
