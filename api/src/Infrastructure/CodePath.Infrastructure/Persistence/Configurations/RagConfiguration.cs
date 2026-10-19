using CodePath.Domain.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace CodePath.Infrastructure.Persistence.Configurations;

public sealed class RagConfiguration : IEntityTypeConfiguration<RagIndexJob>, IEntityTypeConfiguration<RagTeachingDraft>
{
    public void Configure(EntityTypeBuilder<RagIndexJob> b)
    {
        b.ToTable("learning_rag_jobs"); b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasMaxLength(24); b.Property(x => x.ErrorCode).HasMaxLength(80);
        b.HasIndex(x => new { x.TopicId, x.Revision }).IsUnique();
        b.HasIndex(x => new { x.Status, x.NextAttemptAt });
    }
    public void Configure(EntityTypeBuilder<RagTeachingDraft> b)
    {
        b.ToTable("learning_rag_drafts"); b.HasKey(x => x.Id); b.HasIndex(x => x.TopicId);
        b.Property(x => x.Kind).HasMaxLength(16); b.Property(x => x.Language).HasMaxLength(32);
        b.Property(x => x.Status).HasMaxLength(24); b.Property(x => x.Validation).HasMaxLength(80);
        b.Property(x => x.Content).HasColumnType("json"); b.Property(x => x.Sources).HasColumnType("json");
        b.Property(x => x.RowVersion).IsConcurrencyToken();
    }
}
