using CodePath.Domain.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodePath.Infrastructure.Persistence.Configurations;

public sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("courses");

        // Primary Key
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        // Columns & Constraints
        builder.Property(c => c.SubjectName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.SectionCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.Language)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.Semester)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.JoinCode)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(c => c.MaxCapacity)
            .IsRequired()
            .HasDefaultValue(60);

        builder.Property(c => c.Status)
            .IsRequired()
            .HasConversion<int>();

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
        builder.HasIndex(c => c.JoinCode)
            .IsUnique()
            .HasDatabaseName("ux_courses_join_code");

        builder.HasIndex(c => c.TeacherId)
            .HasDatabaseName("ix_courses_teacher_id");

        // Relationships
        builder.HasOne(c => c.Teacher)
            .WithMany(t => t.TeachingCourses)
            .HasForeignKey(c => c.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        // Ignore Domain Events
        builder.Ignore(c => c.DomainEvents);
    }
}
