using CodePath.Domain.Judge;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MySqlConnector;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace CodePath.Infrastructure.Judge;

public sealed class JudgeDbContext(DbContextOptions<JudgeDbContext> options) : DbContext(options)
{
    public DbSet<Problem> Problems => Set<Problem>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<ProblemAward> Awards => Set<ProblemAward>();
    public DbSet<FailureOutbox> FailureEvents => Set<FailureOutbox>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        var ai = b.Entity<CodePath.Domain.Ai.AiInteraction>();
        ai.ToTable("ai_interactions"); ai.HasKey(x => x.Id);
        ai.HasIndex(x => new { x.StudentId, x.ProblemId, x.CreatedAt });
        ai.Property(x => x.Language).HasMaxLength(32);
        ai.Property(x => x.CodeHash).HasMaxLength(64);
        var p = b.Entity<Problem>();
        p.ToTable("judge_problems"); p.HasKey(x => x.Id); p.Ignore(x => x.CanPublish);
        p.HasQueryFilter(x => !x.IsDeleted);
        p.Property(x => x.Title).HasMaxLength(250);
        p.HasMany(x => x.TestCases).WithOne().HasForeignKey(x => x.ProblemId).OnDelete(DeleteBehavior.Cascade);
        p.HasMany(x => x.StarterCodes).WithOne().HasForeignKey(x => x.ProblemId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ProblemStarterCode>().ToTable("judge_starters").HasKey(x => new { x.ProblemId, x.Language });
        b.Entity<TestCase>().ToTable("judge_tests").HasKey(x => x.Id);
        b.Entity<TestCase>().HasIndex(x => new { x.ProblemId, x.Order }).IsUnique();
        var s = b.Entity<Submission>();
        s.ToTable("judge_submissions"); s.HasKey(x => x.Id);
        s.Property(x => x.Score).HasPrecision(12, 2);
        s.Property(x => x.Snapshot).HasColumnType("json");
        // MySQL has no partial indexes. NULL permits unlimited terminal submissions,
        // while the generated value 1 enforces exactly one Pending/Running job.
        s.Property<int?>("ActiveSlot").HasComputedColumnSql("CASE WHEN `Status` IN (7, 8) THEN 1 ELSE NULL END", stored: true);
        s.HasIndex("UserId", "ProblemId", "ActiveSlot").IsUnique();
        s.HasIndex(x => new { x.Status, x.CreatedAt });
        s.HasMany(x => x.Results).WithOne().HasForeignKey(x => x.SubmissionId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<SubmissionResult>().ToTable("judge_results").HasKey(x => new { x.SubmissionId, x.TestCaseId });
        b.Entity<ProblemAward>().ToTable("judge_awards").HasKey(x => new { x.UserId, x.ProblemId });
        b.Entity<FailureOutbox>().ToTable("judge_failure_outbox").HasKey(x => x.SubmissionId);
    }
    public static void ConfigureMySql(DbContextOptionsBuilder options, string connectionString)
    {
        var connection = new MySqlConnectionStringBuilder(connectionString)
        {
            GuidFormat = MySqlGuidFormat.Binary16,
            DateTimeKind = MySqlDateTimeKind.Utc,
            CharacterSet = "utf8mb4",
            // Fencing updates intentionally write the current lease token; count matched rows.
            UseAffectedRows = false
        };
        options.UseMySql(connection.ConnectionString, new MySqlServerVersion(new Version(8, 4, 0)), mysql => mysql
            .DefaultDataTypeMappings(m => m.WithClrBoolean(MySqlBooleanType.TinyInt1).WithClrDateTime(MySqlDateTimeType.DateTime6))
            .MigrationsHistoryTable("__JudgeMigrationsHistory"));
    }
}
public sealed class FailureOutbox
{
    public Guid SubmissionId { get; set; }
    public Guid TestCaseId { get; set; }
    public Verdict Verdict { get; set; }
    public DateTime? PublishedAt { get; set; }
}
public sealed class JudgeDesignFactory : IDesignTimeDbContextFactory<JudgeDbContext>
{
    public JudgeDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<JudgeDbContext>();
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Database")
            ?? Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? new MySqlConnectionStringBuilder
            {
                Server = Environment.GetEnvironmentVariable("Database__Host") ?? "localhost",
                Port = uint.TryParse(Environment.GetEnvironmentVariable("Database__Port"), out var port) ? port : 3306,
                Database = Environment.GetEnvironmentVariable("Database__Name") ?? "codepath",
                UserID = Environment.GetEnvironmentVariable("Database__User") ?? "codepath",
                Password = Environment.GetEnvironmentVariable("Database__Password") ?? ""
            }.ConnectionString;
        JudgeDbContext.ConfigureMySql(options, connection);
        return new(options.Options);
    }
}
