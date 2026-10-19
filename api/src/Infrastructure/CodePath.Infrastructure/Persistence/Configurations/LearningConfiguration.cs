using CodePath.Domain.Learning;
using CodePath.Domain.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace CodePath.Infrastructure.Persistence.Configurations;
public sealed class LearningConfiguration : IEntityTypeConfiguration<Topic>, IEntityTypeConfiguration<Chapter>,
    IEntityTypeConfiguration<TopicExercise>, IEntityTypeConfiguration<TopicProgress>, IEntityTypeConfiguration<BlockProgress>,
    IEntityTypeConfiguration<Asset>, IEntityTypeConfiguration<ActivityDay>, IEntityTypeConfiguration<ClassScoreConfig>,
    IEntityTypeConfiguration<StudentDeadlineExtension>, IEntityTypeConfiguration<ScoreEntry>, IEntityTypeConfiguration<TopicSearchIndex>,
    IEntityTypeConfiguration<Badge>, IEntityTypeConfiguration<UserBadge>, IEntityTypeConfiguration<TopicComment>, IEntityTypeConfiguration<TopicNote>
{
    public void Configure(EntityTypeBuilder<Topic> b)
    {
        b.ToTable("learning_topics"); b.HasKey(x => x.Id); b.HasQueryFilter(x => !x.IsDeleted);
        b.Property(x => x.Title).HasMaxLength(250); b.Property(x => x.Slug).HasMaxLength(150);
        b.Property(x => x.Summary).HasMaxLength(5000); b.Property(x => x.RowVersion).IsConcurrencyToken();
        b.Property(x => x.CompletionRule).HasColumnType("json"); b.Property(x => x.DraftContent).HasColumnType("json"); b.Property(x => x.PublishedContent).HasColumnType("json");
        b.HasIndex(x => new { x.ClassId, x.Slug }).IsUnique(); b.HasIndex(x => new { x.ClassId, x.ChapterId, x.Order });
        b.HasOne<Class>().WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Chapter>().WithMany().HasForeignKey(x => x.ChapterId).OnDelete(DeleteBehavior.Restrict);
    }
    public void Configure(EntityTypeBuilder<Chapter> b) { b.ToTable("learning_chapters"); b.HasKey(x => x.Id); b.HasQueryFilter(x => !x.IsDeleted); b.Property(x => x.Title).HasMaxLength(250); b.HasIndex(x => new { x.ClassId, x.Order }); b.HasOne<Class>().WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict); }
    public void Configure(EntityTypeBuilder<TopicExercise> b) { b.ToTable("learning_exercises"); b.HasKey(x => new { x.TopicId, x.ProblemId }); b.Property(x => x.Points).HasPrecision(12, 2); b.HasOne<Topic>().WithMany().HasForeignKey(x => x.TopicId).OnDelete(DeleteBehavior.Restrict); }
    public void Configure(EntityTypeBuilder<TopicProgress> b) { b.ToTable("learning_progress"); b.HasKey(x => new { x.UserId, x.TopicId }); b.HasIndex(x => new { x.UserId, x.ClassId }); b.HasOne<Topic>().WithMany().HasForeignKey(x => x.TopicId).OnDelete(DeleteBehavior.Restrict); }
    public void Configure(EntityTypeBuilder<BlockProgress> b) { b.ToTable("learning_block_progress"); b.HasKey(x => new { x.UserId, x.TopicId, x.BlockId }); b.Property(x => x.CoveredRanges).HasColumnType("json"); }
    public void Configure(EntityTypeBuilder<Asset> b) { b.ToTable("learning_assets"); b.HasKey(x => x.Id); b.HasQueryFilter(x => !x.IsDeleted); b.Property(x => x.StorageKey).HasMaxLength(500); b.Property(x => x.MimeType).HasMaxLength(100); }
    public void Configure(EntityTypeBuilder<ActivityDay> b) { b.ToTable("learning_activity_days"); b.HasKey(x => new { x.UserId, x.Date }); }
    public void Configure(EntityTypeBuilder<ClassScoreConfig> b) { b.ToTable("learning_score_configs"); b.HasKey(x => x.ClassId); b.Property(x => x.Configuration).HasColumnType("json"); }
    public void Configure(EntityTypeBuilder<StudentDeadlineExtension> b) { b.ToTable("learning_extensions"); b.HasKey(x => new { x.UserId, x.TopicId }); b.Property(x => x.Reason).HasMaxLength(2000); }
    public void Configure(EntityTypeBuilder<ScoreEntry> b) { b.ToTable("learning_scores"); b.HasKey(x => new { x.UserId, x.Source, x.SourceRefId }); b.Property(x => x.Source).HasMaxLength(32); b.HasIndex(x => new { x.UserId, x.ClassId }); b.Property(x => x.BasePoints).HasPrecision(12, 2); b.Property(x => x.EffectivePoints).HasPrecision(12, 2); b.Property(x => x.PenaltyPercent).HasPrecision(7, 4); b.Property(x => x.LateByMinutes).HasPrecision(18, 4); }
    public void Configure(EntityTypeBuilder<TopicSearchIndex> b) { b.ToTable("learning_search"); b.HasKey(x => x.TopicId); b.HasIndex(x => x.ClassId); b.Property(x => x.Document).HasColumnType("longtext").UseCollation("utf8mb4_0900_ai_ci"); }
    public void Configure(EntityTypeBuilder<Badge> b) { b.ToTable("learning_badges"); b.HasKey(x => x.Code); b.Property(x => x.Code).HasMaxLength(40); b.Property(x => x.Name).HasMaxLength(100); }
    public void Configure(EntityTypeBuilder<UserBadge> b) { b.ToTable("learning_user_badges"); b.HasKey(x => new { x.UserId, x.BadgeCode, x.ClassId }); b.Property(x => x.BadgeCode).HasMaxLength(40); }
    public void Configure(EntityTypeBuilder<TopicComment> b) { b.ToTable("learning_comments"); b.HasKey(x => x.Id); b.HasQueryFilter(x => !x.IsDeleted); b.Property(x => x.Body).HasMaxLength(2000); b.HasIndex(x => new { x.TopicId, x.CreatedAt }); }
    public void Configure(EntityTypeBuilder<TopicNote> b) { b.ToTable("learning_notes"); b.HasKey(x => new { x.UserId, x.TopicId }); b.Property(x => x.Body).HasMaxLength(10000); }
}
