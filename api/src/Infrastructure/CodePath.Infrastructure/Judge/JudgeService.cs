using System.Text.Json;
using CodePath.Application.Judge;
using CodePath.Domain.Judge;
using CodePath.Shared.Kernel.Common;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace CodePath.Infrastructure.Judge;

public sealed class JudgeService(JudgeDbContext db, IJudgeAccess access, ICodeExecutor executor, JudgeQueue queue, StackExchange.Redis.IConnectionMultiplexer? redis = null,
    CodePath.Application.Learning.ILearningService? learning = null, TimeProvider? clock = null) : IJudgeService
{
    private const int MaxImportBatch = 50;
    private static Result<object> Fail(string message, string? code = "VALIDATION_ERROR") => Result<object>.Failure(message, code);
    private static Result<Problem> FailProblem(string message, string? code = "VALIDATION_ERROR") => Result<Problem>.Failure(message, code);
    private async Task<Problem?> Find(Guid id, Actor a, bool manage, CancellationToken ct)
    {
        var p = await db.Problems.Include(x => x.TestCases).Include(x => x.StarterCodes).FirstOrDefaultAsync(x => x.Id == id, ct);
        return p != null && (manage ? a.IsAdmin || a.IsTeacher && (p.ClassId.HasValue || p.CreatedBy == a.Id) : p.Status == ProblemStatus.Published)
            && (await access.CanAccess(p.ClassId, a, manage, ct) ||
                !manage && learning != null && await learning.CanReadLinkedProblem(id, a, ct)) ? p : null;
    }
    public async Task<Result<object>> GetProblem(Guid id, Actor a, bool manage, CancellationToken ct)
    {
        var p = await Find(id, a, manage, ct);
        if (p == null) return Fail("Problem not found.", "NOT_FOUND");
        return Result<object>.Success(new { p.Id, p.Title, p.Description, p.Category, p.Difficulty, p.TimeLimitMs, p.MemoryLimitMb, p.Points,
            p.ClassId, Status = p.Status.ToString(), p.ComparisonMode, StarterCodes = p.StarterCodes.Select(x => new StarterDto(x.Language, x.Template)),
            TestCases = p.TestCases.Where(t => manage || t.IsSample).OrderBy(t => t.Order).Select(t => new { t.Id, t.Input, t.ExpectedOutput, t.IsSample, t.Order, t.Weight }) });
    }
    public async Task<Result<object>> ListProblems(Actor a, bool manage, int page, CancellationToken ct, Guid? classId = null, bool library = false, string? search = null, string? difficulty = null, string? category = null, string? language = null)
    {
        if (library && classId.HasValue) return Fail("Library cannot be combined with a class.");
        if (classId.HasValue && !await access.CanAccess(classId, a, manage, ct)) return Fail("Class not found.", "NOT_FOUND");
        var scopes = library ? Array.Empty<JudgeScope>() : await access.Scopes(a, manage, ct);
        var classes = scopes.Where(s => s.Kind == "class").Select(s => s.Id).ToArray();
        var q = db.Problems.AsNoTracking().Where(p =>
            (p.ClassId.HasValue && classes.Contains(p.ClassId.Value)
                || p.ClassId == null && (!manage || a.IsAdmin || a.IsTeacher && p.CreatedBy == a.Id))
            && (manage || p.Status == ProblemStatus.Published));
        if (library) q = q.Where(p => p.ClassId == null);
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); q = q.Where(p => p.Title.Contains(term)); }
        if (!string.IsNullOrWhiteSpace(difficulty)) q = q.Where(p => p.Difficulty == difficulty);
        if (!string.IsNullOrWhiteSpace(category)) q = q.Where(p => p.Category == category);
        if (!string.IsNullOrWhiteSpace(language)) q = q.Where(p => p.StarterCodes.Any(s => s.Language == language));
        if (classId.HasValue) q = q.Where(p => p.ClassId == classId);
        // Aggregate over the filtered library, before pagination. Each solved problem counts once.
        var acceptedIds = db.Submissions.Where(s => s.UserId == a.Id && s.Status == Verdict.Accepted).Select(s => s.ProblemId);
        var totals = await q.GroupBy(p => p.Difficulty).Select(g => new { Difficulty = g.Key, Total = g.Count() }).ToListAsync(ct);
        var solved = await q.Where(p => acceptedIds.Contains(p.Id)).GroupBy(p => p.Difficulty)
            .Select(g => new { Difficulty = g.Key, Solved = g.Count() }).ToListAsync(ct);
        var progress = totals.Select(t => new { t.Difficulty, t.Total, Solved = solved.FirstOrDefault(s => s.Difficulty == t.Difficulty)?.Solved ?? 0 }).ToArray();
        return Result<object>.Success(new { Items = await q.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id).Skip((Math.Clamp(page, 1, 100000) - 1) * 20).Take(20)
            .Select(p => new { p.Id, p.Title, p.Category, p.Difficulty, p.Points, Status = p.Status.ToString(), p.ClassId }).ToListAsync(ct), Total = totals.Sum(t => t.Total), Progress = progress });
    }
    public async Task<Result<object>> SaveProblem(Guid? id, ProblemWrite r, Actor a, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var result = await SaveProblemCore(id, r, a, ct);
        if (!result.IsSuccess) return Fail(result.Error!, result.ErrorCode);
        await tx.CommitAsync(ct);
        return Result<object>.Success(new { result.Value!.Id });
    }
    // Runs inside the caller's transaction so bulk imports can stage several problems atomically.
    private async Task<Result<Problem>> SaveProblemCore(Guid? id, ProblemWrite r, Actor a, CancellationToken ct)
    {
        var validation = new ProblemWriteValidator().Validate(r);
        if (!validation.IsValid || r.StarterCodes.Any(s => !executor.Supports(s.Language))) return FailProblem("Invalid problem fields or language.");
        if (!await access.CanAccess(r.ClassId, a, true, ct)) return FailProblem("Class not found.", "NOT_FOUND");
        if (id.HasValue) await Lock(id.Value, ct);
        var p = id.HasValue ? await Find(id.Value, a, true, ct) : new Problem { CreatedBy = a.Id };
        if (p == null) return FailProblem("Problem not found.", "NOT_FOUND");
        if (id.HasValue && p.ClassId != r.ClassId) return FailProblem("Create a new problem to post to another class.", "CONFLICT");
        if (p.Status == ProblemStatus.Published) return FailProblem("Unpublish before editing.", "CONFLICT");
        if (!id.HasValue) db.Problems.Add(p);
        p.Title = r.Title.Trim();
        // Raw HTML is encoded at the boundary. Frontend additionally skips HTML and filters URLs.
        p.Description = r.Description.Replace("<", "&lt;").Replace(">", "&gt;");
        p.Category = r.Category; p.Difficulty = r.Difficulty; p.TimeLimitMs = r.TimeLimitMs; p.MemoryLimitMb = r.MemoryLimitMb;
        p.Points = r.Points; p.ClassId = r.ClassId; p.ComparisonMode = r.ComparisonMode;
        foreach (var old in p.StarterCodes.Where(s => !r.StarterCodes.Any(n => n.Language == s.Language)).ToArray()) { p.StarterCodes.Remove(old); db.Remove(old); }
        foreach (var starter in r.StarterCodes)
        {
            var entry = p.StarterCodes.FirstOrDefault(s => s.Language == starter.Language);
            if (entry == null) p.StarterCodes.Add(new() { ProblemId = p.Id, Language = starter.Language, Template = starter.Template });
            else entry.Template = starter.Template;
        }
        await db.SaveChangesAsync(ct);
        return Result<Problem>.Success(p);
    }
    public async Task<Result<object>> ImportProblems(List<ProblemImportItem> items, Actor a, CancellationToken ct)
    {
        if (items == null || items.Count == 0) return Fail("No problems supplied.");
        if (items.Count > MaxImportBatch) return Fail($"Bulk import is limited to {MaxImportBatch} problems per request.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var created = new List<object>();
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item?.Problem == null) return Fail($"Item #{i + 1}: missing problem data.");
            var saved = await SaveProblemCore(null, item.Problem, a, ct);
            if (!saved.IsSuccess) return Fail($"Item #{i + 1}: {saved.Error}", saved.ErrorCode);
            var problem = saved.Value!;
            if (item.Tests is { Count: > 0 })
            {
                var tests = await SaveTestsCore(problem.Id, item.Tests, a, ct);
                if (!tests.IsSuccess) return Fail($"Item #{i + 1}: {tests.Error}", tests.ErrorCode);
            }
            if (item.Publish)
            {
                var publish = await ChangeProblemCore(problem.Id, "publish", a, ct);
                if (!publish.IsSuccess) return Fail($"Item #{i + 1}: {publish.Error}", publish.ErrorCode);
            }
            created.Add(new { Index = i, ProblemId = problem.Id, problem.Title, Tests = item.Tests?.Count ?? 0, Published = item.Publish });
        }
        await tx.CommitAsync(ct);
        return Result<object>.Success(new { Imported = created.Count, Items = created });
    }
    public async Task<Result<object>> ChangeProblem(Guid id, string action, Actor a, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var result = await ChangeProblemCore(id, action, a, ct);
        if (!result.IsSuccess) return Fail(result.Error!, result.ErrorCode);
        await tx.CommitAsync(ct);
        return result;
    }
    private async Task<Result<object>> ChangeProblemCore(Guid id, string action, Actor a, CancellationToken ct)
    {
        await Lock(id, ct);
        var p = await Find(id, a, true, ct);
        if (p == null) return Fail("Problem not found.", "NOT_FOUND");
        if (action == "publish" && !p.CanPublish) return Fail("Publishing requires 10–15 tests and 1–2 samples.");
        if (action == "delete") p.IsDeleted = true;
        else p.Status = action == "publish" ? ProblemStatus.Published : ProblemStatus.Draft;
        await db.SaveChangesAsync(ct); return Result<object>.Success(new { p.Id });
    }
    public async Task<Result<object>> SaveTests(Guid id, List<TestWrite> tests, Actor a, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var result = await SaveTestsCore(id, tests, a, ct);
        if (!result.IsSuccess) return Fail(result.Error!, result.ErrorCode);
        await tx.CommitAsync(ct);
        return result;
    }
    private async Task<Result<object>> SaveTestsCore(Guid id, List<TestWrite> tests, Actor a, CancellationToken ct)
    {
        await Lock(id, ct);
        var p = await Find(id, a, true, ct);
        if (p == null) return Fail("Problem not found.", "NOT_FOUND");
        if (p.Status == ProblemStatus.Published) return Fail("Unpublish before editing tests.", "CONFLICT");
        if (tests.Count > 15 || tests.Any(t => t == null || !new TestWriteValidator().Validate(t).IsValid)
            || tests.Select(t => t.Order).Distinct().Count() != tests.Count || tests.Count(t => t.IsSample) > 2) return Fail("Invalid tests, duplicate order, or too many samples.");
        db.RemoveRange(p.TestCases); p.TestCases.Clear(); await db.SaveChangesAsync(ct);
        p.TestCases = tests.Select(t => new TestCase { ProblemId = id, Input = t.Input, ExpectedOutput = t.ExpectedOutput, IsSample = t.IsSample, Order = t.Order, Weight = t.Weight }).ToList();
        // Test IDs are assigned in the domain; explicitly mark replacements as new rows.
        db.AddRange(p.TestCases);
        await db.SaveChangesAsync(ct);
        return Result<object>.Success(new { Count = tests.Count });
    }
    private Task<int> Lock(Guid id, CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync($"SELECT `Id` FROM `judge_problems` WHERE `Id` = {id} FOR UPDATE", ct);
    public async Task<Result<object>> Run(Guid id, CodeRequest r, Actor a, CancellationToken ct)
    {
        if (!new CodeRequestValidator().Validate(r).IsValid || !executor.Supports(r.Language)) return Fail("Invalid source, input or language.");
        var p = await Find(id, a, false, ct);
        if (p == null) return Fail("Problem not found.", "NOT_FOUND");
        var tests = p.TestCases.Where(t => t.IsSample && (r.TestCaseIds == null || r.TestCaseIds.Contains(t.Id))).OrderBy(t => t.Order).Take(2).ToArray();
        if (r.TestCaseIds != null && tests.Length != r.TestCaseIds.Length) return Fail("Only sample tests can be run.");
        var results = new List<TestDto>();
        if (r.CustomInput != null)
        {
            var result = await executor.Execute(r.Language, r.SourceCode, r.CustomInput, new(p.TimeLimitMs, p.MemoryLimitMb), ct);
            results.Add(new(null, 1, true, result.Verdict.ToString(), result.RuntimeMs, result.MemoryKb, r.CustomInput, null, result.Output, JudgeSanitizer.Sanitize(result.Error)));
        }
        else foreach (var t in tests)
        {
            var result = await executor.Execute(r.Language, r.SourceCode, t.Input, new(p.TimeLimitMs, p.MemoryLimitMb), ct);
            var verdict = result.Verdict == Verdict.Accepted && !JudgeRules.Equal(t.ExpectedOutput, result.Output ?? "", p.ComparisonMode) ? Verdict.WrongAnswer : result.Verdict;
            results.Add(new(t.Id, t.Order, true, verdict.ToString(), result.RuntimeMs, result.MemoryKb, t.Input, t.ExpectedOutput, result.Output, JudgeSanitizer.Sanitize(result.Error)));
        }
        if (redis != null)
        {
            try
            {
                var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(r.SourceCode)));
                await redis.GetDatabase().StringSetAsync($"ai:run:{a.Id}:{id}:{r.Language}:{hash}",
                    JsonSerializer.Serialize(new CodePath.Infrastructure.Ai.AiRunSnapshot(DateTime.UtcNow, results)), TimeSpan.FromHours(1));
            }
            catch (StackExchange.Redis.RedisException) { /* A cache outage must not break running code. */ }
        }
        return Result<object>.Success(new { Results = results });
    }
    public async Task<Result<object>> Submit(Guid id, CodeRequest r, Actor a, CancellationToken ct)
    {
        if (!new CodeRequestValidator().Validate(r).IsValid || r.CustomInput != null || r.TestCaseIds != null || !executor.Supports(r.Language)) return Fail("Invalid submission.");
        var p = await Find(id, a, false, ct);
        if (p == null) return Fail("Problem not found.", "NOT_FOUND");
        Guid? contextClass = p.ClassId;
        if (r.TopicId.HasValue)
        {
            if (learning == null) return Fail("Learning is unavailable.", "FEATURE_NOT_ENABLED");
            var context = await learning.SubmissionContext(r.TopicId.Value, id, a, ct);
            if (!context.IsSuccess) return Fail(context.Error!, context.ErrorCode!);
            contextClass = context.Value;
        }
        var s = new Submission { ProblemId = id, UserId = a.Id, ClassId = contextClass, TopicId = r.TopicId,
            CreatedAt = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime, Language = r.Language, SourceCode = r.SourceCode, TotalCount = p.TestCases.Count, Snapshot = JsonSerializer.Serialize(p) };
        db.Submissions.Add(s);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { ErrorCode: MySqlErrorCode.DuplicateKeyEntry }) { return Fail("A submission is already running.", "SUBMISSION_IN_PROGRESS"); }
        queue.Wake(s.Id);
        return Result<object>.Success(new { SubmissionId = s.Id });
    }
    public async Task<Result<SubmissionDto>> GetSubmission(Guid id, Actor a, bool manage, CancellationToken ct)
    {
        var s = await db.Submissions.AsNoTracking().Include(x => x.Results).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s == null || (manage ? !a.IsAdmin && (!a.IsTeacher || await Find(s.ProblemId, a, true, ct) == null) : s.UserId != a.Id))
            return Result<SubmissionDto>.Failure("Submission not found.", "NOT_FOUND");
        return Result<SubmissionDto>.Success(JudgeRules.ToDto(s, manage));
    }
    public async Task<Result<Page<SubmissionDto>>> History(Actor a, bool manage, Guid? problem, Guid? user, Verdict? verdict, int page, int size, CancellationToken ct, Guid? classId = null)
    {
        page = Math.Clamp(page, 1, 100000); size = Math.Clamp(size, 1, 50);
        var q = db.Submissions.AsNoTracking().AsQueryable();
        if (!manage) q = q.Where(s => s.UserId == a.Id);
        else if (!a.IsAdmin)
        {
            var scopes = await access.Scopes(a, true, ct);
            var classes = scopes.Where(s => s.Kind == "class").Select(s => s.Id).ToArray();
            var ids = db.Problems.Where(p => p.ClassId.HasValue && classes.Contains(p.ClassId.Value)
                || p.ClassId == null && p.CreatedBy == a.Id).Select(p => p.Id);
            q = q.Where(s => ids.Contains(s.ProblemId));
        }
        if (problem.HasValue) q = q.Where(s => s.ProblemId == problem);
        if (classId.HasValue) q = q.Where(s => s.ClassId == classId);
        if (manage && user.HasValue) q = q.Where(s => s.UserId == user);
        if (verdict.HasValue) q = q.Where(s => s.Status == verdict);
        var count = await q.CountAsync(ct);
        var items = await q.OrderByDescending(s => s.CreatedAt).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return Result<Page<SubmissionDto>>.Success(new(items.Select(s => JudgeRules.ToDto(s)).ToArray(), count, page, size));
    }
}
