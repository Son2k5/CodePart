using System.Text.Json;
using CodePath.Application.Judge;
using CodePath.Application.Learning;
using CodePath.Domain.Learning;
using CodePath.Domain.Judge;
using CodePath.Domain.Users.Entities;
using CodePath.Shared.Kernel.Common;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
namespace CodePath.Infrastructure.Learning;
public sealed partial class LearningService
{
    public async Task<Result<object>> Outline(Guid id, Actor a, CancellationToken ct)
    {
        await AutoEnroll(id, a, ct); await Access(id, a, false, ct);
        var topics = await Ordered(id, ct);
        var progress = await db.TopicProgress.AsNoTracking().Where(p => p.ClassId == id && p.UserId == a.Id).ToDictionaryAsync(p => p.TopicId, ct);
        var chapters = await db.Chapters.AsNoTracking().Where(c => c.ClassId == id).OrderBy(c => c.Order).ToListAsync(ct);
        var visible = topics.Where(t => t.Status == TopicStatus.Published).ToList();
        var done = visible.Count(t => progress.GetValueOrDefault(t.Id)?.Status == LearningStatus.Completed);
        var rows = topics.Select((t, i) => new { t.Id, t.ChapterId, t.Title, t.Slug, t.Order, t.EstimatedMinutes,
            lockedReason = t.LockedReason(Now, i == 0 || progress.GetValueOrDefault(topics[i - 1].Id)?.Status == LearningStatus.Completed),
            status = progress.GetValueOrDefault(t.Id)?.Status.ToString() ?? "NotStarted" }).ToList();
        return Ok(new { completedTopics = done, totalTopics = visible.Count, percent = visible.Count == 0 ? 0 : done * 100m / visible.Count,
            chapters = chapters.Select(c => new { c.Id, c.Title, c.Order, topics = rows.Where(t => t.ChapterId == c.Id) }) });
    }
    public async Task<Result<object>> Read(Guid classId, string slug, Actor a, CancellationToken ct)
    {
        await AutoEnroll(classId, a, ct); await Access(classId, a, false, ct);
        var t = await db.Topics.SingleOrDefaultAsync(t => t.ClassId == classId && t.Slug == slug, ct);
        if (t == null) Reject("NOT_FOUND", "Topic not found.");
        await CheckUnlocked(t!, a, ct);
        var progress = await db.TopicProgress.FindAsync([a.Id, t!.Id], ct);
        if (progress == null) { progress = new() { UserId = a.Id, TopicId = t.Id, ClassId = classId, Status = LearningStatus.InProgress }; db.TopicProgress.Add(progress); }
        progress.LastAccessedAt = Now; await db.SaveChangesAsync(ct);
        var exercises = await VisibleExercises(t.Id, ct);
        var problemIds = exercises.Select(e => e.ProblemId).ToArray();
        var submissions = await judge.Submissions.AsNoTracking().Where(s => s.UserId == a.Id && s.TopicId == t.Id && problemIds.Contains(s.ProblemId)).ToListAsync(ct);
        var scores = await db.ScoreEntries.AsNoTracking().Where(s => s.UserId == a.Id && s.TopicId == t.Id).ToListAsync(ct);
        var extension = await db.DeadlineExtensions.FindAsync([a.Id, t.Id], ct); var due = extension?.NewDueAt ?? t.DueAt;
        var ordered = await Ordered(classId, ct); var position = ordered.FindIndex(x => x.Id == t.Id);
        return Ok(new { t.Id, t.Title, t.Slug, t.Summary, t.PublishedVersion, content = JsonSerializer.Deserialize<ContentBlock[]>(t.PublishedContent, Json),
            exercises = exercises.Select(e => new { e.ProblemId, e.Order, e.Points, e.IsRequired,
                status = submissions.Any(s => s.ProblemId == e.ProblemId && s.Status == Verdict.Accepted) ? "Accepted" : submissions.Any(s => s.ProblemId == e.ProblemId) ? "Attempted" : "NotAttempted",
                effectivePoints = scores.FirstOrDefault(s => s.SourceRefId == ExerciseScoreId(t.Id, e.ProblemId))?.EffectivePoints ?? 0, dueAt = due, remainingSeconds = due.HasValue ? (double?)(due.Value - Now).TotalSeconds : null }),
            progress, previous = position > 0 ? ordered[position - 1].Slug : null, next = position + 1 < ordered.Count ? ordered[position + 1].Slug : null });
    }
    private async Task<List<TopicExercise>> VisibleExercises(Guid topic, CancellationToken ct)
    {
        var exercises = await db.TopicExercises.Where(e => e.TopicId == topic && !e.IsDraft).OrderBy(e => e.Order).ToListAsync(ct);
        var ids = exercises.Select(e => e.ProblemId).ToArray();
        var visible = await judge.Problems.Where(p => ids.Contains(p.Id) && p.Status == ProblemStatus.Published).Select(p => p.Id).ToListAsync(ct);
        return exercises.Where(e => visible.Contains(e.ProblemId)).ToList();
    }
    public async Task<Result<Guid>> SubmissionContext(Guid topicId, Guid problemId, Actor a, CancellationToken ct)
    {
        var t = await Topic(topicId, ct); if (t == null) return Result<Guid>.Failure("Topic not found.", "NOT_FOUND");
        await Access(t.ClassId, a, false, ct); await CheckUnlocked(t, a, ct);
        if (!await db.ClassEnrollments.AnyAsync(e => e.ClassId == t.ClassId && e.StudentId == a.Id && e.Status == ClassEnrollmentStatus.Active, ct)) return Result<Guid>.Failure("Enrollment required.", "NOT_ENROLLED");
        if (!await db.TopicExercises.AnyAsync(e => e.TopicId == topicId && e.ProblemId == problemId && !e.IsDraft, ct)) return Result<Guid>.Failure("Problem is not attached to topic.", "CONTENT_INVALID");
        return Result<Guid>.Success(t.ClassId);
    }
    public Task<Result<object>> Progress(Guid id, HeartbeatInput? heartbeat, Actor a, CancellationToken ct) => db.Database.CreateExecutionStrategy().ExecuteAsync(() => ProgressCore(id, heartbeat, a, ct));
    private async Task<Result<object>> ProgressCore(Guid id, HeartbeatInput? heartbeat, Actor a, CancellationToken ct)
    {
        var t = await Topic(id, ct); if (t == null) Reject("NOT_FOUND", "Topic not found.");
        await Access(t!.ClassId, a, false, ct); await CheckUnlocked(t, a, ct);
        // A topic row lock serializes progress creation, completion events and concurrent judge delivery.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT Id FROM learning_topics WHERE Id = {id} FOR UPDATE", ct);
        if (heartbeat != null)
        {
            var blocks = JsonSerializer.Deserialize<ContentBlock[]>(t.PublishedContent, Json)!;
            var block = blocks.SingleOrDefault(b => b.BlockId == heartbeat.BlockId);
            if (block == null) Reject("BLOCK_INVALID", "Published block not found.");
            var key = $"learning:heartbeat:{a.Id}:{id}:{heartbeat.BlockId}";
            if (!await redis.GetDatabase().StringSetAsync(key, "1", TimeSpan.FromSeconds(10), When.NotExists)) return Ok(new { ignored = true });
            var p = await db.BlockProgress.FindAsync([a.Id, id, heartbeat.BlockId], ct);
            var isNew = p == null;
            if (p == null) { p = new() { UserId = a.Id, TopicId = id, BlockId = heartbeat.BlockId, UpdatedAt = Now }; db.BlockProgress.Add(p); }
            if (block!.Type == "Video")
            {
                var duration = block.Data.GetProperty("durationSec").GetDouble();
                if (heartbeat.PositionSec is not { } pos || !double.IsFinite(pos) || pos < 0 || pos > duration || heartbeat.Page != null) Reject("BLOCK_INVALID", "Invalid video heartbeat.");
                var ranges = JsonSerializer.Deserialize<CoveredRange[]>(p.CoveredRanges, Json)!;
                var merged = Coverage.Heartbeat(ranges, isNew ? null : p.LastPositionSec, heartbeat.PositionSec!.Value, duration, Now - p.UpdatedAt);
                p.CoveredRanges = JsonSerializer.Serialize(merged, Json); p.DurationSec = duration; p.LastPositionSec = heartbeat.PositionSec;
                if (merged.Sum(r => r.End - r.Start) >= 30) await Activity(a.Id, ct);
            }
            else if (block.Type == "Pdf")
            {
                var total = block.Data.GetProperty("pageCount").GetInt32();
                if (heartbeat.Page is not { } page || page < 1 || page > total || heartbeat.TotalPages != total || heartbeat.PositionSec != null) Reject("BLOCK_INVALID", "PDF page count must match published metadata.");
                p.MaxPage = Math.Max(p.MaxPage ?? 0, heartbeat.Page!.Value); p.TotalPages = total;
            }
            else Reject("BLOCK_INVALID", "Only video and PDF blocks accept heartbeats.");
            p.UpdatedAt = Now;
        }
        var progress = await Evaluate(t, a.Id, heartbeat == null, ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Ok(progress);
    }
    private async Task<TopicProgress> Evaluate(Topic t, Guid user, bool manual, CancellationToken ct)
    {
        var p = await db.TopicProgress.FindAsync([user, t.Id], ct);
        if (p == null) { p = new() { UserId = user, TopicId = t.Id, ClassId = t.ClassId, Status = LearningStatus.InProgress }; db.TopicProgress.Add(p); }
        p.LastAccessedAt = Now;
        var exercises = await VisibleExercises(t.Id, ct);
        var passed = await judge.Submissions.Where(s => s.TopicId == t.Id && s.UserId == user && s.Status == Verdict.Accepted).Select(s => s.ProblemId).Distinct().ToListAsync(ct);
        p.ExercisePassedCount = exercises.Count(e => passed.Contains(e.ProblemId)); p.ExerciseTotalCount = exercises.Count;
        var blocks = JsonSerializer.Deserialize<ContentBlock[]>(t.PublishedContent, Json)!;
        var stored = await db.BlockProgress.Where(b => b.UserId == user && b.TopicId == t.Id).ToListAsync(ct);
        var progress = stored.Concat(db.BlockProgress.Local.Where(b => b.UserId == user && b.TopicId == t.Id)).DistinctBy(b => b.BlockId).ToDictionary(b => b.BlockId);
        var videos = blocks.Where(b => b.Type == "Video").Select(b => progress.TryGetValue(b.BlockId, out var bp) ? Coverage.Percent(JsonSerializer.Deserialize<CoveredRange[]>(bp.CoveredRanges, Json)!, b.Data.GetProperty("durationSec").GetDouble()) : 0);
        var pdfs = blocks.Where(b => b.Type == "Pdf").Select(b => progress.TryGetValue(b.BlockId, out var bp) ? (bp.MaxPage ?? 0) * 100m / b.Data.GetProperty("pageCount").GetInt32() : 0);
        var rule = JsonSerializer.Deserialize<CompletionRule>(t.CompletionRule, Json)!;
        if (manual && !CompletionEvaluator.CanMarkManually(rule)) Reject("CONTENT_INVALID", "This topic requires tracked completion.");
        if (p.Status != LearningStatus.Completed && CompletionEvaluator.Complete(rule, videos, pdfs, p.ExercisePassedCount, exercises.Count, exercises.Count(e => e.IsRequired && passed.Contains(e.ProblemId)), exercises.Count(e => e.IsRequired), manual))
        {
            p.Status = LearningStatus.Completed; p.CompletedAt = Now;
            var ext = await db.DeadlineExtensions.FindAsync([user, t.Id], ct); p.CompletedLate = (ext?.NewDueAt ?? t.DueAt) < Now;
            await Activity(user, ct); await Award(user, "FIRST_TOPIC", t.ClassId, ct);
            await publisher.Publish(new TopicCompleted(user, t.Id, t.ClassId), ct);
            var all = await Ordered(t.ClassId, ct); var done = await db.TopicProgress.Where(x => x.UserId == user && x.ClassId == t.ClassId && x.Status == LearningStatus.Completed).Select(x => x.TopicId).ToListAsync(ct); done.Add(t.Id);
            if (all.Where(x => x.ChapterId == t.ChapterId && x.Status == TopicStatus.Published).All(x => done.Contains(x.Id)))
            { await Award(user, "CHAPTER_COMPLETE", t.ClassId, ct); await publisher.Publish(new ChapterCompleted(user, t.ChapterId, t.ClassId), ct); }
            if (all.Where(x => x.Status == TopicStatus.Published).All(x => done.Contains(x.Id)))
            { await Award(user, "CLASS_COMPLETE", t.ClassId, ct); await publisher.Publish(new ClassCompleted(user, t.ClassId), ct); }
        }
        return p;
    }
    private async Task Award(Guid user, string code, Guid classId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT IGNORE INTO learning_user_badges (UserId, BadgeCode, ClassId, AwardedAt) VALUES ({user}, {code}, {classId}, {Now})", ct);
    }
    private async Task Activity(Guid user, CancellationToken ct)
    {
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(Now, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")));
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT IGNORE INTO learning_activity_days (UserId, Date) VALUES ({user}, {date})", ct);
        var days = await db.ActivityDays.Where(a => a.UserId == user).Select(a => a.Date).ToListAsync(ct);
        var streak = StreakCalculator.Calculate(days, date);
        if (streak.Current >= 7) await Award(user, "STREAK_7", Guid.Empty, ct);
        if (streak.Current >= 30) await Award(user, "STREAK_30", Guid.Empty, ct);
    }
    public Task Judged(SubmissionJudgedEvent e, CancellationToken ct) => db.Database.CreateExecutionStrategy().ExecuteAsync(() => JudgedCore(e, ct));
    private async Task JudgedCore(SubmissionJudgedEvent e, CancellationToken ct)
    {
        var t = await Topic(e.TopicId, ct); if (t == null) return;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT Id FROM learning_topics WHERE Id = {t.Id} FOR UPDATE", ct);
        await Activity(e.UserId, ct);
        var submissions = await judge.Submissions.AsNoTracking().Where(s => s.TopicId == t.Id && s.UserId == e.UserId && s.FinishedAt != null).ToListAsync(ct);
        if (submissions.Any(s => s.Status == Verdict.Accepted)) await Award(e.UserId, "FIRST_ACCEPTED", t.ClassId, ct);
        var c = await db.Classes.SingleAsync(c => c.Id == t.ClassId, ct);
        if (c.Mode == ClassMode.Managed)
        {
            var config = await db.ClassScoreConfigs.FindAsync([t.ClassId], ct);
            var policy = config == null ? new LatePolicy() : JsonSerializer.Deserialize<ScoreConfiguration>(config.Configuration, Json)!.LatePolicy;
            var ext = await db.DeadlineExtensions.FindAsync([e.UserId, t.Id], ct);
            foreach (var exercise in await VisibleExercises(t.Id, ct))
            {
                var candidates = submissions.Where(s => s.ProblemId == exercise.ProblemId).Select(s => {
                    var snapshot = JsonSerializer.Deserialize<Problem>(s.Snapshot)!;
                    return (snapshot.Points == 0 ? 0 : exercise.Points * s.Score / snapshot.Points, s.CreatedAt);
                });
                var best = ScoreCalculator.Best(candidates, t.DueAt, ext?.NewDueAt, policy);
                // Source includes topic ID so shared problems in different topics do not overwrite scores.
                var source = "Exercise"; var refId = ExerciseScoreId(t.Id, exercise.ProblemId);
                var score = await db.ScoreEntries.FindAsync([e.UserId, source, refId], ct);
                if (score == null) { score = new() { UserId = e.UserId, ClassId = t.ClassId, TopicId = t.Id, Source = source, SourceRefId = refId }; db.ScoreEntries.Add(score); }
                score.BasePoints = best.BasePoints; score.EffectivePoints = best.EffectivePoints; score.PenaltyPercent = best.PenaltyPercent; score.LateByMinutes = best.LateByMinutes;
            }
        }
        await Evaluate(t, e.UserId, false, ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    private static Guid ExerciseScoreId(Guid topic, Guid problem) => new(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{topic}:{problem}"))[..16]);
    public async Task<Result<object>> Report(Guid id, Guid? student, int page, int pageSize, Actor a, CancellationToken ct)
    {
        await Access(id, a, student != a.Id, ct); page = Math.Clamp(page, 1, 100000); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.ClassEnrollments.Where(e => e.ClassId == id && e.Status == ClassEnrollmentStatus.Active && (!student.HasValue || e.StudentId == student));
        var count = await query.CountAsync(ct);
        var students = await query.OrderBy(e => e.StudentId).Skip((page - 1) * pageSize).Take(pageSize).Select(e => new { e.StudentId, e.Student.FullName }).ToListAsync(ct);
        var ids = students.Select(s => s.StudentId).ToArray(); var topics = await Ordered(id, ct);
        var progress = await db.TopicProgress.AsNoTracking().Where(p => p.ClassId == id && ids.Contains(p.UserId)).ToListAsync(ct);
        var c = await db.Classes.SingleAsync(c => c.Id == id, ct);
        var scores = c.Mode == ClassMode.Managed ? await db.ScoreEntries.AsNoTracking().Where(s => s.ClassId == id && ids.Contains(s.UserId)).ToListAsync(ct) : [];
        return Ok(new { students, topics = topics.Select(t => new { t.Id, t.Title }), progress, scores, count, page, pageSize });
    }
    public async Task<Result<object>> Personal(string action, Actor a, CancellationToken ct)
    {
        if (action == "badges") return Ok(await db.UserBadges.AsNoTracking().Where(b => b.UserId == a.Id).ToListAsync(ct));
        if (action == "streak")
        {
            var days = await db.ActivityDays.Where(d => d.UserId == a.Id).Select(d => d.Date).ToListAsync(ct);
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(Now, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")));
            var streak = StreakCalculator.Calculate(days, today); return Ok(new { streak.Current, streak.Longest, streak.ActiveToday });
        }
        var recent = await db.TopicProgress.AsNoTracking().Where(p => p.UserId == a.Id && db.ClassEnrollments.Any(e => e.ClassId == p.ClassId && e.StudentId == a.Id && e.Status == ClassEnrollmentStatus.Active))
            .GroupBy(p => p.ClassId).Select(g => new { ClassId = g.Key, LastAccess = g.Max(p => p.LastAccessedAt) }).OrderByDescending(x => x.LastAccess).Take(5).ToListAsync(ct);
        var classIds = recent.Select(x => x.ClassId).ToArray();
        var rows = await (from t in db.Topics join c in db.Chapters on t.ChapterId equals c.Id
            join p in db.TopicProgress.Where(p => p.UserId == a.Id) on t.Id equals p.TopicId into ps from p in ps.DefaultIfEmpty()
            where classIds.Contains(t.ClassId) && t.Status == TopicStatus.Published && (t.PublishAt == null || t.PublishAt <= Now)
            orderby c.Order, t.Order, t.Id select new { t.ClassId, t.Id, t.Slug, t.Title, Status = p == null ? LearningStatus.NotStarted : p.Status, LastAccess = p == null ? (DateTime?)null : p.LastAccessedAt }).ToListAsync(ct);
        return Ok(recent.Select(c => new { c.ClassId, topic = rows.Where(t => t.ClassId == c.ClassId && t.Status == LearningStatus.InProgress).OrderByDescending(t => t.LastAccess).FirstOrDefault()
            ?? rows.FirstOrDefault(t => t.ClassId == c.ClassId && t.Status != LearningStatus.Completed), percent = rows.Count(t => t.ClassId == c.ClassId) == 0 ? 0 : rows.Count(t => t.ClassId == c.ClassId && t.Status == LearningStatus.Completed) * 100m / rows.Count(t => t.ClassId == c.ClassId) }));
    }
}
