using CodePath.Domain.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodePath.Infrastructure.Persistence.Configurations;

public sealed class ClassConfiguration : IEntityTypeConfiguration<Class>
{
    public void Configure(EntityTypeBuilder<Class> builder)
    {
        builder.ToTable("classes");
        builder.HasQueryFilter(c => !c.IsDeleted);
        builder.Property(c => c.Description).HasMaxLength(5000);
        builder.Property(c => c.Thumbnail).HasMaxLength(2048);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.AccessType).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.JoinCode).HasMaxLength(8).UseCollation("ascii_bin").HasCharSet("ascii");
        builder.Property(c => c.RowVersion).IsConcurrencyToken().ValueGeneratedNever();
        // MySQL UNIQUE permits multiple NULLs; filtered indexes are not supported.
        builder.HasIndex(c => c.JoinCode).IsUnique().HasDatabaseName("ux_classes_join_code");
        builder.HasIndex(c => new { c.Status, c.AccessType });
        builder.HasIndex(c => c.Name);

        // Primary Key
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        // Columns & Constraints
        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.AcademicYear)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(c => c.FacultyId)
            .IsRequired(false);

        builder.Property(c => c.TeacherId)
            .IsRequired(false);

        // Audit Fields (BaseEntity)
        builder.Property(c => c.CreatedAt)
            .IsRequired()
            .HasColumnType("datetime(6)");

        builder.Property(c => c.CreatedBy)
            .HasMaxLength(100);

        builder.Property(c => c.UpdatedAt)
            .HasColumnType("datetime(6)");

        builder.Property(c => c.UpdatedBy)
            .HasMaxLength(100);

        // Indexes
        builder.HasIndex(c => new { c.Name, c.AcademicYear, c.FacultyId })
            .IsUnique()
            .HasDatabaseName("ux_classes_name_year_faculty");

        builder.HasIndex(c => c.FacultyId)
            .HasDatabaseName("ix_classes_faculty_id");

        builder.HasIndex(c => c.TeacherId)
            .HasDatabaseName("ix_classes_teacher_id");

        // Relationships
        builder.HasOne(c => c.Faculty)
            .WithMany(f => f.Classes)
            .HasForeignKey(c => c.FacultyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Teacher)
            .WithMany(t => t.ManagedClasses)
            .HasForeignKey(c => c.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        // Ignore Domain Events
        builder.Ignore(c => c.DomainEvents);
    }
}
