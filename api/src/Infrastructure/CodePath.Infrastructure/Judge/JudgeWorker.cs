using System.Text.Json;
using System.Threading.Channels;
using CodePath.Application.Judge;
using CodePath.Domain.Judge;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodePath.Infrastructure.Judge;

public sealed class JudgeQueue
{
    private readonly Channel<Guid> channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(256) { FullMode = BoundedChannelFullMode.DropWrite });
    public void Wake(Guid id) => channel.Writer.TryWrite(id);
    public async Task Wait(CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        try { await channel.Reader.ReadAsync(timeout.Token); } catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
    }
}
public sealed class JudgeWorker(IServiceScopeFactory scopes, JudgeQueue queue, IOptions<JudgeOptions> options, ILogger<JudgeWorker> log) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.WhenAll(
        Enumerable.Range(0, options.Value.Concurrency).Select(_ => Loop(stoppingToken)));
    private async Task Loop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<JudgeDbContext>();
                await db.Submissions.Where(s => (s.Status == Verdict.Running && s.LeaseUntil < DateTime.UtcNow)
                    || s.Status == Verdict.Pending && s.CreatedAt < DateTime.UtcNow.AddSeconds(-options.Value.JobTimeoutSeconds))
                    .ExecuteUpdateAsync(u => u.SetProperty(s => s.Status, Verdict.InternalError).SetProperty(s => s.FinishedAt, DateTime.UtcNow).SetProperty(s => s.LeaseToken, (Guid?)null), ct);
                // Publish after durable completion. Subscribers must deduplicate on SubmissionId.
                var events = await db.FailureEvents.Where(e => e.PublishedAt == null).Take(20).ToListAsync(ct);
                foreach (var e in events)
                {
                    await scope.ServiceProvider.GetRequiredService<IPublisher>().Publish(new FailureNotification(new(e.SubmissionId, e.TestCaseId, e.Verdict)), ct);
                    e.PublishedAt = DateTime.UtcNow;
                }
                await db.SaveChangesAsync(ct);
                var id = await db.Submissions.Where(s => s.Status == Verdict.Pending).OrderBy(s => s.CreatedAt).Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct);
                if (id == null) { await queue.Wait(ct); continue; }
                var token = Guid.NewGuid();
                var lease = DateTime.UtcNow.AddSeconds(options.Value.JobTimeoutSeconds);
                var claimed = await db.Submissions.Where(s => s.Id == id && s.Status == Verdict.Pending)
                    .ExecuteUpdateAsync(u => u.SetProperty(s => s.Status, Verdict.Running).SetProperty(s => s.LeaseToken, token)
                        .SetProperty(s => s.LeaseUntil, lease).SetProperty(s => s.Attempts, s => s.Attempts + 1), ct);
                if (claimed == 0) continue;
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
                deadline.CancelAfter(TimeSpan.FromSeconds(options.Value.JobTimeoutSeconds));
                await Process(scope.ServiceProvider, id.Value, token, deadline.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { log.LogError(ex, "Judge worker failed; unfinished lease will expire."); await Task.Delay(1000, ct); }
        }
    }
    public static async Task Process(IServiceProvider services, Guid id, Guid token, CancellationToken ct)
    {
        var db = services.GetRequiredService<JudgeDbContext>();
        var executor = services.GetRequiredService<ICodeExecutor>();
        var s = await db.Submissions.Include(s => s.Results).SingleAsync(s => s.Id == id, ct);
        if (s.Status != Verdict.Running || s.LeaseToken != token) return;
        var p = JsonSerializer.Deserialize<Problem>(s.Snapshot) ?? throw new InvalidDataException("Missing snapshot.");
        foreach (var t in p.TestCases.OrderBy(t => t.Order))
        {
            if (s.Results.Any(r => r.TestCaseId == t.Id)) continue;
            ExecutionResult result = new(Verdict.InternalError, null, null, 0, 0);
            for (var attempt = 0; attempt < 3; attempt++)
            {
                result = await executor.Execute(s.Language, s.SourceCode, t.Input, new(p.TimeLimitMs, p.MemoryLimitMb), ct);
                if (result.Verdict != Verdict.InternalError) break;
                if (attempt < 2) await Task.Delay(250 * (attempt + 1), ct);
            }
            var verdict = result.Verdict == Verdict.Accepted && !JudgeRules.Equal(t.ExpectedOutput, result.Output ?? "", p.ComparisonMode) ? Verdict.WrongAnswer : result.Verdict;
            await using var progress = await db.Database.BeginTransactionAsync(ct);
            // Fencing update locks the row and prevents a timed-out worker from writing late results.
            if (await db.Submissions.Where(x => x.Id == id && x.LeaseToken == token && x.Status == Verdict.Running && x.LeaseUntil > DateTime.UtcNow)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.LeaseToken, token), ct) == 0) return;
            s.Results.Add(new() { SubmissionId = id, TestCaseId = t.Id, Index = t.Order, IsSample = t.IsSample,
                Input = t.IsSample ? t.Input : null, ExpectedOutput = t.IsSample ? t.ExpectedOutput : null,
                Verdict = verdict, ActualOutput = JudgeRules.Clip(result.Output, t.IsSample ? 16384 : 256),
                ErrorMessage = t.IsSample ? JudgeSanitizer.Sanitize(result.Error) : null, RuntimeMs = result.RuntimeMs, MemoryKb = result.MemoryKb });
            if (verdict == Verdict.CompileError && t.IsSample) s.CompileOutput = JudgeSanitizer.Sanitize(result.Error);
            s.PassedCount = s.Results.Count(r => r.Verdict == Verdict.Accepted);
            s.MaxRuntimeMs = s.Results.Max(r => r.RuntimeMs); s.MaxMemoryKb = s.Results.Max(r => r.MemoryKb);
            await db.SaveChangesAsync(ct); await progress.CommitAsync(ct);
        }
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (await db.Submissions.Where(x => x.Id == id && x.LeaseToken == token && x.Status == Verdict.Running && x.LeaseUntil > DateTime.UtcNow)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.LeaseToken, token), ct) == 0) return;
        s.Status = JudgeRules.Aggregate(s.Results.OrderBy(r => r.Index).Select(r => r.Verdict));
        s.Score = JudgeRules.Score(p, s.Results); s.FinishedAt = DateTime.UtcNow; s.LeaseToken = null;
        if (s.Status == Verdict.Accepted)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO `judge_awards` (`UserId`, `ProblemId`, `SubmissionId`, `Points`) VALUES ({s.UserId}, {s.ProblemId}, {s.Id}, {p.Points}) ON DUPLICATE KEY UPDATE `UserId` = `UserId`", ct);
            // Do not depend on affected-row semantics (UseAffectedRows varies by connection).
            var award = await db.Awards.AsNoTracking().SingleAsync(a => a.UserId == s.UserId && a.ProblemId == s.ProblemId, ct);
            s.AwardedPoints = award.SubmissionId == s.Id ? award.Points : 0;
        }
        else
        {
            var first = s.Results.OrderBy(r => r.Index).First(r => r.Verdict != Verdict.Accepted);
            db.FailureEvents.Add(new() { SubmissionId = id, TestCaseId = first.TestCaseId, Verdict = first.Verdict });
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
