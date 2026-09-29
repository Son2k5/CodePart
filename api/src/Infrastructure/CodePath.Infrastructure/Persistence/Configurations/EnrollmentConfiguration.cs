using CodePath.Domain.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodePath.Infrastructure.Persistence.Configurations;

public sealed class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.ToTable("enrollments");

        // Primary Key
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        // Columns & Constraints
        builder.Property(e => e.CourseId)
            .IsRequired();

        builder.Property(e => e.StudentId)
            .IsRequired();

        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(e => e.MidtermScore)
            .HasPrecision(4, 2);

        builder.Property(e => e.FinalScore)
            .HasPrecision(4, 2);

        builder.Property(e => e.TotalScore)
            .HasPrecision(4, 2);

        builder.Property(e => e.IsPassed)
            .HasDefaultValue(false);

        // Audit Fields (BaseEntity)
        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasColumnType("timestamptz");

        builder.Property(e => e.CreatedBy)
            .HasMaxLength(100);

        builder.Property(e => e.UpdatedAt)
            .HasColumnType("timestamptz");

        builder.Property(e => e.UpdatedBy)
            .HasMaxLength(100);

        // Indexes
        builder.HasIndex(e => new { e.CourseId, e.StudentId })
            .IsUnique()
            .HasDatabaseName("ux_enrollments_course_student");

        builder.HasIndex(e => e.StudentId)
            .HasDatabaseName("ix_enrollments_student_id");

        // Relationships
        builder.HasOne(e => e.Course)
            .WithMany(c => c.Enrollments)
            .HasForeignKey(e => e.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Student)
            .WithMany(s => s.Enrollments)
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Ignore Domain Events
        builder.Ignore(e => e.DomainEvents);
    }
}
