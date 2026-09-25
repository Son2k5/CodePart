using CodePath.Domain.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodePath.Infrastructure.Users.Persistence.Configurations;

public sealed class ClassConfiguration : IEntityTypeConfiguration<Class>
{
    public void Configure(EntityTypeBuilder<Class> b)
    {
        b.ToTable("classes", "users");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.Name).IsRequired().HasMaxLength(100);
        b.Property(x => x.AcademicYear).IsRequired().HasMaxLength(20);

        b.Property(x => x.FacultyId).IsRequired();
        b.Property(x => x.TeacherId).IsRequired();

        b.Property(x => x.CreatedAt).IsRequired().HasColumnType("timestamptz");
        b.Property(x => x.UpdatedAt).HasColumnType("timestamptz");

        // Unique constraint on class name within academic year and faculty
        b.HasIndex(x => new { x.Name, x.AcademicYear, x.FacultyId })
            .IsUnique()
            .HasDatabaseName("ux_users_classes_name_year_faculty");

        // Indexes for Foreign Keys
        b.HasIndex(x => x.FacultyId).HasDatabaseName("ix_users_classes_faculty_id");
        b.HasIndex(x => x.TeacherId).HasDatabaseName("ix_users_classes_teacher_id");

        b.HasOne(x => x.Faculty)
            .WithMany(x => x.Classes)
            .HasForeignKey(x => x.FacultyId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Teacher)
            .WithMany(x => x.ManagedClasses)
            .HasForeignKey(x => x.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Ignore(x => x.DomainEvents);
    }
}
