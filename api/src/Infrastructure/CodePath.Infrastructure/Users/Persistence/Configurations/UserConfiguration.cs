using CodePath.Domain.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodePath.Infrastructure.Users.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users", "users");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

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
            .HasConversion<string>()
            .HasMaxLength(50);

        b.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        b.Property(x => x.StudentId)
            .HasMaxLength(50)
            .HasColumnType("character varying(50)");

        b.Property(x => x.CreatedAt).IsRequired().HasColumnType("timestamptz");
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedAt).HasColumnType("timestamptz");
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.Property(x => x.EmailVerifiedAt).HasColumnType("timestamptz");
        b.Property(x => x.LastLoginAt).HasColumnType("timestamptz");

        b.HasIndex(x => x.Email).IsUnique().HasDatabaseName("ux_users_email");

        b.HasIndex(x => x.StudentId)
            .IsUnique()
            .HasFilter("\"StudentId\" IS NOT NULL")
            .HasDatabaseName("ux_users_student_id");

        b.HasIndex(x => x.Role).HasDatabaseName("ix_users_role");
        b.HasIndex(x => x.Status).HasDatabaseName("ix_users_status");
        b.HasIndex(x => x.ClassId).HasDatabaseName("ix_users_class_id");
        b.HasIndex(x => x.FacultyId).HasDatabaseName("ix_users_faculty_id");

        b.HasOne(x => x.Class)
            .WithMany(x => x.Students)
            .HasForeignKey(x => x.ClassId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Faculty)
            .WithMany(x => x.Users)
            .HasForeignKey(x => x.FacultyId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Ignore(x => x.DomainEvents);
    }
}
