using CodePath.Modules.Users.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodePath.Modules.Users.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        // ---- Table + PK ----
        b.ToTable("users", "users");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        // ---- Columns ----
        b.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(256)
            .HasColumnType("character varying(256)");

        b.Property(x => x.PasswordHash)
            .IsRequired()
            .HasMaxLength(256);

        b.Property(x => x.FullName)
            .IsRequired()
            .HasMaxLength(200);

        b.Property(x => x.Role)
            .IsRequired()
            .HasConversion<int>();

        b.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        b.Property(x => x.FailedLoginCount)
            .IsRequired()
            .HasDefaultValue(0);

        b.Property(x => x.StudentCode)
            .HasMaxLength(20)
            .HasColumnType("character varying(20)");

        b.Property(x => x.CreatedAt).IsRequired().HasColumnType("timestamptz");
        b.Property(x => x.UpdatedAt).HasColumnType("timestamptz");
        b.Property(x => x.EmailVerifiedAt).HasColumnType("timestamptz");
        b.Property(x => x.LastLoginAt).HasColumnType("timestamptz");
        b.Property(x => x.LockoutEnd).HasColumnType("timestamptz");

        // ---- Indexes ----
        b.HasIndex(x => x.Email)
            .IsUnique()
            .HasDatabaseName("ux_users_email");

        b.HasIndex(x => x.StudentCode)
            .IsUnique()
            .HasFilter("\"StudentCode\" IS NOT NULL")
            .HasDatabaseName("ux_users_student_code");

        b.HasIndex(x => x.ClassId)
            .HasDatabaseName("ix_users_class_id");

        b.HasIndex(x => x.FacultyId)
            .HasDatabaseName("ix_users_faculty_id");

        b.HasIndex(x => x.Role)
            .HasDatabaseName("ix_users_role");

        // ---- Relationships & Delete Behaviors ----
        // User (Student) -> Class: Protect students from being orphaned if class deletion is attempted
        b.HasOne(x => x.Class)
            .WithMany(x => x.Students)
            .HasForeignKey(x => x.ClassId)
            .OnDelete(DeleteBehavior.Restrict);

        // User (Teacher) -> Faculty: Protect faculty from deletion while teachers are assigned
        b.HasOne(x => x.Faculty)
            .WithMany(x => x.Users)
            .HasForeignKey(x => x.FacultyId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Ignore(x => x.DomainEvents);
    }
}
