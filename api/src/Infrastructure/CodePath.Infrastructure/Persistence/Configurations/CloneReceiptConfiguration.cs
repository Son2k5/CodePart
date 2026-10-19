using CodePath.Domain.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace CodePath.Infrastructure.Persistence.Configurations;
public sealed class CloneReceiptConfiguration : IEntityTypeConfiguration<CloneReceipt>
{
    public void Configure(EntityTypeBuilder<CloneReceipt> b)
    {
        b.ToTable("learning_clone_receipts"); b.HasKey(x => new { x.TargetClassId, x.Key });
        b.Property(x => x.Key).HasMaxLength(100); b.Property(x => x.RequestHash).HasMaxLength(64); b.Property(x => x.Result).HasColumnType("json");
    }
}
