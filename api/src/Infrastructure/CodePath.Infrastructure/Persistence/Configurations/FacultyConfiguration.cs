using CodePath.Domain.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodePath.Infrastructure.Persistence.Configurations;

public sealed class FacultyConfiguration : IEntityTypeConfiguration<Faculty>
{
    public void Configure(EntityTypeBuilder<Faculty> builder)
    {
        builder.ToTable("faculties");

        // Primary Key
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        // Columns & Constraints
        builder.Property(f => f.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(f => f.Code)
            .IsRequired()
            .HasMaxLength(20);

        // Audit Fields (BaseEntity)
        builder.Property(f => f.CreatedAt)
            .IsRequired()
            .HasColumnType("timestamptz");

        builder.Property(f => f.CreatedBy)
            .HasMaxLength(100);

        builder.Property(f => f.UpdatedAt)
            .HasColumnType("timestamptz");

        builder.Property(f => f.UpdatedBy)
            .HasMaxLength(100);

        // Indexes
        builder.HasIndex(f => f.Code)
            .IsUnique()
            .HasDatabaseName("ux_faculties_code");

        // Ignore Domain Events
        builder.Ignore(f => f.DomainEvents);
    }
}
