using CodePath.Modules.Users.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodePath.Modules.Users.Infrastructure.Persistence.Configurations;

public sealed class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> b)
    {
        b.ToTable("enrollments", "users");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.CourseId).IsRequired();
        b.Property(x => x.StudentId).IsRequired();

        b.Property(x => x.Status).IsRequired().HasConversion<int>();

        // Academic evaluation columns
        b.Property(x => x.MidtermScore)
            .HasPrecision(4, 2);

        b.Property(x => x.FinalScore)
            .HasPrecision(4, 2);

        b.Property(x => x.TotalScore)
            .HasPrecision(4, 2);

        b.Property(x => x.IsPassed);

        b.Property(x => x.CreatedAt).IsRequired().HasColumnType("timestamptz");
        b.Property(x => x.UpdatedAt).HasColumnType("timestamptz");

        // Unique constraint: A student cannot enroll in the same course offering multiple times
        b.HasIndex(x => new { x.CourseId, x.StudentId })
            .IsUnique()
            .HasDatabaseName("ux_users_enrollments_course_student");

        // Index on StudentId for fast lookups of student enrollments
        b.HasIndex(x => x.StudentId)
            .HasDatabaseName("ix_users_enrollments_student_id");

        // Use Restrict to preserve academic enrollment records
        b.HasOne(x => x.Course)
            .WithMany(x => x.Enrollments)
            .HasForeignKey(x => x.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Student)
            .WithMany(x => x.Enrollments)
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Ignore(x => x.DomainEvents);
    }
}