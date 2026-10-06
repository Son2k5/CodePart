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

        builder.Property(r => r.FamilyId)
            .IsRequired();

        builder.Property(r => r.ParentTokenId);

        builder.Property(r => r.TokenHash)
            .IsRequired()
            .HasMaxLength(128)
            .HasCharSet("ascii")
            .UseCollation("ascii_bin");

        builder.Property(r => r.ExpiresAt)
            .IsRequired()
            .HasColumnType("datetime(6)");

        builder.Property(r => r.AbsoluteExpiresAt)
            .IsRequired()
            .HasColumnType("datetime(6)");

        builder.Property(r => r.CreatedByIp)
            .HasMaxLength(64);

        builder.Property(r => r.RevokedAt)
            .HasColumnType("datetime(6)");

        builder.Property(r => r.RevokedByIp)
            .HasMaxLength(64);

        builder.Property(r => r.ReplacedByTokenHash)
            .HasMaxLength(128)
            .HasCharSet("ascii")
            .UseCollation("ascii_bin");

        // Audit Fields (BaseEntity)
        builder.Property(r => r.CreatedAt)
            .IsRequired()
            .HasColumnType("datetime(6)");

        builder.Property(r => r.CreatedBy)
            .HasMaxLength(100);

        builder.Property(r => r.UpdatedAt)
            .HasColumnType("datetime(6)");

        builder.Property(r => r.UpdatedBy)
            .HasMaxLength(100);

        // Indexes
        builder.HasIndex(r => r.TokenHash)
            .IsUnique()
            .HasDatabaseName("ux_refresh_tokens_hash");

        builder.HasIndex(r => r.UserId)
            .HasDatabaseName("ix_refresh_tokens_user");

        builder.HasIndex(r => r.FamilyId)
            .HasDatabaseName("ix_refresh_tokens_family");

        builder.HasIndex(r => new { r.UserId, r.FamilyId, r.RevokedAt })
            .HasDatabaseName("ix_refresh_tokens_active");

        // Relationship & Cascade Constraint with User
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<RefreshToken>()
            .WithMany()
            .HasForeignKey(r => r.ParentTokenId)
            .OnDelete(DeleteBehavior.SetNull);

        // Ignore Domain Events
        builder.Ignore(r => r.DomainEvents);
    }
}
