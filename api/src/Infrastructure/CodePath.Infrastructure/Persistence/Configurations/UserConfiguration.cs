using CodePath.Domain.Users.Entities;
using CodePath.Shared.Kernel.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodePath.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        // Primary Key
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        // Columns & Constraints
        builder.Property(u => u.FullName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256)
            .HasColumnType("character varying(256)");

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(u => u.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(u => u.StudentId)
            .HasMaxLength(50)
            .HasColumnType("character varying(50)");

        builder.Property(u => u.EmailVerifiedAt)
            .HasColumnType("timestamptz");

        builder.Property(u => u.LastLoginAt)
            .HasColumnType("timestamptz");

        // Audit Fields (BaseEntity)
        builder.Property(u => u.CreatedAt)
            .IsRequired()
            .HasColumnType("timestamptz");

        builder.Property(u => u.CreatedBy)
            .HasMaxLength(100);

        builder.Property(u => u.UpdatedAt)
            .HasColumnType("timestamptz");

        builder.Property(u => u.UpdatedBy)
            .HasMaxLength(100);

        // Indexes
        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("ux_users_email");

        builder.HasIndex(u => u.StudentId)
            .IsUnique()
            .HasFilter("\"StudentId\" IS NOT NULL")
            .HasDatabaseName("ux_users_student_id");

        builder.HasIndex(u => u.Role)
            .HasDatabaseName("ix_users_role");

        builder.HasIndex(u => u.Status)
            .HasDatabaseName("ix_users_status");

        builder.HasIndex(u => u.ClassId)
            .HasDatabaseName("ix_users_class_id");

        builder.HasIndex(u => u.FacultyId)
            .HasDatabaseName("ix_users_faculty_id");

        // Relationships
        builder.HasOne(u => u.Class)
            .WithMany(c => c.Students)
            .HasForeignKey(u => u.ClassId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.Faculty)
            .WithMany(f => f.Users)
            .HasForeignKey(u => u.FacultyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Ignore Domain Events
        builder.Ignore(u => u.DomainEvents);
    }
}
