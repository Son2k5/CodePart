using CodePath.Domain.Learning;
using Microsoft.EntityFrameworkCore;
namespace CodePath.Infrastructure.Persistence;
public partial class AppDbContext
{
    public DbSet<Chapter> Chapters => Set<Chapter>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<TopicExercise> TopicExercises => Set<TopicExercise>();
    public DbSet<TopicProgress> TopicProgress => Set<TopicProgress>();
    public DbSet<BlockProgress> BlockProgress => Set<BlockProgress>();
    public DbSet<Asset> LearningAssets => Set<Asset>();
    public DbSet<ActivityDay> ActivityDays => Set<ActivityDay>();
    public DbSet<ClassScoreConfig> ClassScoreConfigs => Set<ClassScoreConfig>();
    public DbSet<StudentDeadlineExtension> DeadlineExtensions => Set<StudentDeadlineExtension>();
    public DbSet<ScoreEntry> ScoreEntries => Set<ScoreEntry>();
    public DbSet<TopicSearchIndex> TopicSearch => Set<TopicSearchIndex>();
    public DbSet<UserBadge> UserBadges => Set<UserBadge>();
    public DbSet<TopicComment> TopicComments => Set<TopicComment>();
    public DbSet<TopicNote> TopicNotes => Set<TopicNote>();
}
