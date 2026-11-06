using CodePath.Domain.Notifications.Entities;
using CodePath.Domain.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodePath.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();

        builder.Property(n => n.RecipientId).IsRequired();
        builder.Property(n => n.Type).IsRequired().HasConversion<int>();
        builder.Property(n => n.Title).IsRequired().HasMaxLength(200);
        builder.Property(n => n.Body).HasMaxLength(2000);

        // MySQL JSON column (spec called for jsonb; the platform runs on MySQL).
        builder.Property(n => n.Payload).HasColumnType("json");

        builder.Property(n => n.DedupKey).HasMaxLength(160);

        builder.Property(n => n.IsRead).HasDefaultValue(false);
        builder.Property(n => n.ReadAt).HasColumnType("datetime(6)");

        // Audit fields (BaseEntity)
        builder.Property(n => n.CreatedAt).IsRequired().HasColumnType("datetime(6)");
        builder.Property(n => n.CreatedBy).HasMaxLength(100);
        builder.Property(n => n.UpdatedAt).HasColumnType("datetime(6)");
        builder.Property(n => n.UpdatedBy).HasMaxLength(100);

        // Inbox read path: (RecipientId, IsRead, CreatedAt DESC)
        builder.HasIndex(n => new { n.RecipientId, n.IsRead, n.CreatedAt })
            .IsDescending(false, false, true)
            .HasDatabaseName("ix_notifications_recipient_read_created");

        // Idempotency backstop. MySQL treats NULL as distinct, so non-deduplicated rows coexist.
        builder.HasIndex(n => new { n.RecipientId, n.DedupKey })
            .IsUnique()
            .HasDatabaseName("ux_notifications_recipient_dedup");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(n => n.RecipientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(n => n.DomainEvents);
    }
}
