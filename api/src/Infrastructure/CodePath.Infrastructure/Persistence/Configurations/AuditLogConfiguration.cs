using CodePath.Domain.Auditing.Entities;
using CodePath.Domain.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodePath.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(log => log.Id);
        builder.Property(log => log.Id).ValueGeneratedNever();

        builder.Property(log => log.Action)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(100);

        builder.Property(log => log.OccurredAt)
            .IsRequired()
            .HasColumnType("timestamptz");

        builder.Property(log => log.IpAddress)
            .HasMaxLength(45);

        builder.Property(log => log.DataJson)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(log => log.CreatedAt)
            .IsRequired()
            .HasColumnType("timestamptz");
        builder.Property(log => log.CreatedBy).HasMaxLength(100);
        builder.Property(log => log.UpdatedAt).HasColumnType("timestamptz");
        builder.Property(log => log.UpdatedBy).HasMaxLength(100);

        builder.HasIndex(log => new { log.OccurredAt, log.Id })
            .IsDescending(true, true)
            .HasDatabaseName("ix_audit_logs_occurred_at_id");
        builder.HasIndex(log => new { log.TargetUserId, log.OccurredAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_audit_logs_target_occurred_at");
        builder.HasIndex(log => log.ActorId)
            .HasDatabaseName("ix_audit_logs_actor_id");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(log => log.ActorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(log => log.TargetUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(log => log.DomainEvents);
    }
}
