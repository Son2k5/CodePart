using CodePath.Domain.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodePath.Infrastructure.Persistence.Configurations;

public sealed class ClassConfiguration : IEntityTypeConfiguration<Class>
{
    public void Configure(EntityTypeBuilder<Class> builder)
    {
        builder.ToTable("classes");

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
            .IsRequired();

        builder.Property(c => c.TeacherId)
            .IsRequired();

        // Audit Fields (BaseEntity)
        builder.Property(c => c.CreatedAt)
            .IsRequired()
            .HasColumnType("timestamptz");

        builder.Property(c => c.CreatedBy)
            .HasMaxLength(100);

        builder.Property(c => c.UpdatedAt)
            .HasColumnType("timestamptz");

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
