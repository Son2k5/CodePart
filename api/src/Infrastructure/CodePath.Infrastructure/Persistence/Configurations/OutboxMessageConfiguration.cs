using CodePath.Domain.Notifications.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodePath.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.EventType).IsRequired().HasMaxLength(512);

        // Transport envelope: JSON column (event payload is always valid JSON).
        builder.Property(m => m.Payload).HasColumnType("json");

        builder.Property(m => m.OccurredAtUtc).IsRequired().HasColumnType("datetime(6)");
        builder.Property(m => m.NextAttemptAtUtc).IsRequired().HasColumnType("datetime(6)");
        builder.Property(m => m.LeaseUntilUtc).HasColumnType("datetime(6)");
        builder.Property(m => m.ProcessedAtUtc).HasColumnType("datetime(6)");
        builder.Property(m => m.DeadLetteredAtUtc).HasColumnType("datetime(6)");
        builder.Property(m => m.Error).HasMaxLength(1000);

        builder.Property(m => m.Attempts).HasDefaultValue(0);

        // Worker polling path: pending rows whose lease has expired and whose next attempt is due.
        builder.HasIndex(m => new { m.ProcessedAtUtc, m.DeadLetteredAtUtc, m.NextAttemptAtUtc })
            .HasDatabaseName("ix_outbox_messages_dispatch");
    }
}
