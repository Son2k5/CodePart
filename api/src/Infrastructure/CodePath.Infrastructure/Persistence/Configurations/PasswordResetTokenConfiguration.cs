using CodePath.Domain.Auth.Entities;
using CodePath.Domain.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodePath.Infrastructure.Persistence.Configurations;

public sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).ValueGeneratedNever();
        builder.Property(token => token.TokenHash)
            .HasMaxLength(64)
            .HasCharSet("ascii")
            .UseCollation("ascii_bin")
            .IsRequired();
        builder.Property(token => token.ExpiresAt).HasColumnType("datetime(6)").IsRequired();
        builder.Property(token => token.UsedAt).HasColumnType("datetime(6)");
        builder.Property(token => token.RequestedByIp).HasMaxLength(64);
        builder.Property(token => token.CreatedAt).HasColumnType("datetime(6)").IsRequired();
        builder.Property(token => token.CreatedBy).HasMaxLength(100);
        builder.Property(token => token.UpdatedAt).HasColumnType("datetime(6)");
        builder.Property(token => token.UpdatedBy).HasMaxLength(100);

        builder.HasIndex(token => token.TokenHash)
            .IsUnique()
            .HasDatabaseName("ux_password_reset_tokens_hash");
        builder.HasIndex(token => new { token.UserId, token.ExpiresAt })
            .HasDatabaseName("ix_password_reset_tokens_user_expiry");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(token => token.DomainEvents);
    }
}
