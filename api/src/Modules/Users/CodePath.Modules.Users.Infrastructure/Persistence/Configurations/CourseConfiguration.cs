using CodePath.Modules.Users.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodePath.Modules.Users.Infrastructure.Persistence.Configurations;

public sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> b)
    {
        b.ToTable("courses", "users");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.SubjectName).IsRequired().HasMaxLength(200);
        b.Property(x => x.SectionCode).IsRequired().HasMaxLength(50);
        b.Property(x => x.Language).IsRequired().HasMaxLength(50);
        b.Property(x => x.Semester).IsRequired().HasMaxLength(50);
        b.Property(x => x.JoinCode).IsRequired().HasMaxLength(20);
        b.Property(x => x.MaxCapacity).IsRequired().HasDefaultValue(60);

        b.Property(x => x.Status).IsRequired().HasConversion<int>();

        b.Property(x => x.TeacherId).IsRequired();

        b.Property(x => x.CreatedAt).IsRequired().HasColumnType("timestamptz");
        b.Property(x => x.UpdatedAt).HasColumnType("timestamptz");

        b.HasIndex(x => x.JoinCode).IsUnique().HasDatabaseName("ux_users_courses_join_code");
        b.HasIndex(x => x.TeacherId).HasDatabaseName("ix_users_courses_teacher_id");

        b.HasOne(x => x.Teacher)
            .WithMany(x => x.TeachingCourses)
            .HasForeignKey(x => x.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Ignore(x => x.DomainEvents);
    }
}