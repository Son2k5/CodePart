using CodePath.Domain.Auth.Entities;
using CodePath.Domain.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodePath.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        // Primary Key
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        // Columns & Constraints
        builder.Property(r => r.UserId)
            .IsRequired();

        builder.Property(r => r.TokenHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(r => r.ExpiresAt)
            .IsRequired()
            .HasColumnType("timestamptz");

        builder.Property(r => r.CreatedByIp)
            .HasMaxLength(64);

        builder.Property(r => r.RevokedAt)
            .HasColumnType("timestamptz");

        builder.Property(r => r.RevokedByIp)
            .HasMaxLength(64);

        builder.Property(r => r.ReplacedByTokenHash)
            .HasMaxLength(128);

        // Audit Fields (BaseEntity)
        builder.Property(r => r.CreatedAt)
            .IsRequired()
            .HasColumnType("timestamptz");

        builder.Property(r => r.CreatedBy)
            .HasMaxLength(100);

        builder.Property(r => r.UpdatedAt)
            .HasColumnType("timestamptz");

        builder.Property(r => r.UpdatedBy)
            .HasMaxLength(100);

        // Indexes
        builder.HasIndex(r => r.TokenHash)
            .IsUnique()
            .HasDatabaseName("ux_refresh_tokens_hash");

        builder.HasIndex(r => r.UserId)
            .HasDatabaseName("ix_refresh_tokens_user");

        // Relationship & Cascade Constraint with User
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ignore Domain Events
        builder.Ignore(r => r.DomainEvents);
    }
}
