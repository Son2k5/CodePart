using CodePath.Domain.Auth.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodePath.Infrastructure.Auth.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens", "auth");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.UserId).IsRequired();
        b.Property(x => x.TokenHash).IsRequired().HasMaxLength(128);
        b.Property(x => x.ExpiresAt).IsRequired().HasColumnType("timestamptz");

        b.Property(x => x.CreatedByIp).HasMaxLength(64);
        b.Property(x => x.RevokedByIp).HasMaxLength(64);
        b.Property(x => x.ReplacedByTokenHash).HasMaxLength(128);

        b.Property(x => x.RevokedAt).HasColumnType("timestamptz");
        b.Property(x => x.CreatedAt).IsRequired().HasColumnType("timestamptz");
        b.Property(x => x.UpdatedAt).HasColumnType("timestamptz");

        b.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("ux_auth_refresh_tokens_hash");
        b.HasIndex(x => x.UserId).HasDatabaseName("ix_auth_refresh_tokens_user");

        b.Ignore(x => x.DomainEvents);
    }
}
