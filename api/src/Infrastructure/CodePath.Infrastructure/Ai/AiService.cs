using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodePath.Application.Ai;
using CodePath.Application.Judge;
using CodePath.Application.Learning;
using CodePath.Domain.Ai;
using CodePath.Domain.Judge;
using CodePath.Infrastructure.Judge;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace CodePath.Infrastructure.Ai;

public sealed record AiRunSnapshot(DateTime CreatedAt, List<TestDto> Results);

public sealed class AiService(JudgeDbContext db, IJudgeAccess access, IAiAssistantClient client,
    IConnectionMultiplexer redis, IConfiguration config, IRagService rag, ILearningService learning) : IAiService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task<AssistResult> Assist(Guid studentId, AssistInput input, CancellationToken ct)
    {
        if (input.SourceCode == null || Encoding.UTF8.GetByteCount(input.SourceCode) > 65536 || input.Question?.Length > 2000 ||
            input.Level is < 1 or > 3 || input.Type is not ("Hint" or "AutoHint" or "TraceTestCase" or "ExplainError" or "Ask"))
            throw new AiPolicyException("INVALID_CONTEXT");
        var p = await db.Problems.AsNoTracking().Include(x => x.StarterCodes).SingleOrDefaultAsync(x => x.Id == input.ProblemId, ct);
        if (p == null || p.Status != ProblemStatus.Published ||
            !((p.ClassId.HasValue || p.CourseId.HasValue) && await access.CanAccess(p.CourseId, p.ClassId, new(studentId, false, false), false, ct)
                || await learning.CanReadLinkedProblem(p.Id, new(studentId, false, false), ct)))
            throw new AiPolicyException("FORBIDDEN", 403);
        var starter = p.StarterCodes.SingleOrDefault(x => x.Language == input.Language);
        if (starter == null) throw new AiPolicyException("INVALID_CONTEXT");
        var cache = redis.GetDatabase();
        var lockKey = $"ai:lock:{studentId}";
        var token = Guid.NewGuid().ToString();
        if (!await cache.StringSetAsync(lockKey, token, TimeSpan.FromSeconds(60), When.NotExists)) throw new AiPolicyException("RATE_LIMIT", 429);
        try
        {
            var now = DateTime.UtcNow;
            var previous = await db.Set<AiInteraction>().AsNoTracking().Where(x => x.StudentId == studentId && x.ProblemId == p.Id && x.Language == input.Language)
                .OrderByDescending(x => x.CreatedAt).Take(10).ToListAsync(ct);
            var submissions = await db.Submissions.AsNoTracking().Where(x => x.UserId == studentId && x.ProblemId == p.Id && x.Language == input.Language && x.FinishedAt != null)
                .OrderByDescending(x => x.CreatedAt).Take(50).Include(x => x.Results).ToListAsync(ct);
            var lastHint = previous.FirstOrDefault();
            var maxLevel = await db.Set<AiInteraction>().Where(x => x.StudentId == studentId && x.ProblemId == p.Id && x.Language == input.Language && x.GuardStatus != "fallback").Select(x => (int?)x.Level).MaxAsync(ct) ?? 0;
            var failures = submissions.TakeWhile(x => x.Status != Verdict.Accepted).ToArray();
            var sinceHint = failures.Count(x => x.CreatedAt > (lastHint?.CreatedAt ?? DateTime.MinValue));
            var minutes = lastHint == null ? 0 : (now - lastHint.CreatedAt).TotalMinutes;
            if (!AiPolicy.CanUnlock(input.Level, maxLevel, sinceHint, minutes)) throw new AiPolicyException("LEVEL_LOCKED", 409);
            // One atomic script enforces both limits across API replicas; rejected requests consume neither.
            var daily = $"ai:quota:{studentId}:{now:yyyyMMdd}";
            var rate = $"ai:rate:{studentId}:{now:yyyyMMddHHmm}";
            var count = (int)await cache.ScriptEvaluateAsync("""
                local d=tonumber(redis.call('GET',KEYS[1]) or '0')
                local r=tonumber(redis.call('GET',KEYS[2]) or '0')
                if d>=tonumber(ARGV[1]) then return -1 end
                if r>=5 then return -2 end
                redis.call('INCR',KEYS[1]); redis.call('EXPIRE',KEYS[1],172800)
                redis.call('INCR',KEYS[2]); redis.call('EXPIRE',KEYS[2],120)
                return d+1
                """, [daily, rate], [AiPolicy.DailyQuota]);
            if (count < 0) throw new AiPolicyException(count == -1 ? "QUOTA" : "RATE_LIMIT", 429);
            // A previous verdict applies only to the exact source/language currently being discussed.
            var s = submissions.FirstOrDefault(x => x.SourceCode == input.SourceCode);
            var codeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input.SourceCode)));
            var runJson = await cache.StringGetAsync($"ai:run:{studentId}:{p.Id}:{input.Language}:{codeHash}");
            var run = runJson.HasValue ? JsonSerializer.Deserialize<AiRunSnapshot>(runJson.ToString()) : null;
            if (run != null && s != null && run.CreatedAt <= s.CreatedAt) run = null;
            var similarityHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(System.Text.RegularExpressions.Regex.Replace(input.SourceCode, @"\s+", ""))));
            var repeated = previous.TakeWhile(x => x.CodeHash == similarityHash).ToArray();
            var stuck = repeated.Length == 0 ? 0 : (now - repeated.Last().CreatedAt).TotalMinutes;
            var studentRef = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(config["Ai:AnonymizationKey"]!), studentId.ToByteArray()));
            var context = JsonSerializer.SerializeToElement(new {
                requestId = Guid.NewGuid(), studentRef,
                problem = new { title = p.Title, statement = JudgeRules.Clip(p.Description, 24000), constraints = $"{p.TimeLimitMs} ms; {p.MemoryLimitMb} MB", language = input.Language, starterCode = starter.Template },
                sourceCode = input.SourceCode,
                runState = new { lastAction = s == null ? "none" : "submit", passed = s?.PassedCount ?? 0, total = s?.TotalCount ?? 0,
                    runtimeMs = s?.MaxRuntimeMs ?? 0, memoryKb = s?.MaxMemoryKb ?? 0,
                    // Hidden errors/stdout may echo secret input, so never transmit them.
                    compileError = s?.Status == Verdict.CompileError ? JudgeRules.Clip(s.CompileOutput, 4000) : null,
                    runtimeError = JudgeRules.Clip(s?.Results.FirstOrDefault(r => r.IsSample && r.Verdict == Verdict.RuntimeError)?.ErrorMessage, 4000),
                    verdicts = s?.Results.Select(r => new { testCaseId = r.IsSample ? r.TestCaseId.ToString() : "hidden", isSample = r.IsSample,
                        verdict = r.Verdict.ToString(), input = r.IsSample ? JudgeRules.Clip(r.Input, 4000) : null, expected = r.IsSample ? JudgeRules.Clip(r.ExpectedOutput, 4000) : null, actual = r.IsSample ? JudgeRules.Clip(r.ActualOutput, 4000) : null }).ToArray() },
                history = new { previousHints = previous.Take(3).Select(x => new { level = x.Level, text = JsonSerializer.Deserialize<AiResponse>(x.Response, Json)!.Message }),
                    submitCount = submissions.Count, minutesStuck = stuck, consecutiveFailures = failures.Length, repeatedCodeCount = repeated.Length },
                request = new { type = input.Type, level = input.Level, question = input.Question }
            }, Json);
            // Normalize null collection to [] for the strict Python schema.
            var node = System.Text.Json.Nodes.JsonNode.Parse(context.GetRawText())!;
            node["runState"]!["verdicts"] ??= new System.Text.Json.Nodes.JsonArray();
            if (run != null)
                node["runState"] = JsonSerializer.SerializeToNode(new {
                    lastAction = "run", passed = run.Results.Count(r => r.Verdict == "Accepted"), total = run.Results.Count,
                    runtimeMs = run.Results.Select(r => r.RuntimeMs).DefaultIfEmpty().Max(), memoryKb = run.Results.Select(r => r.MemoryKb ?? 0).DefaultIfEmpty().Max(),
                    compileError = run.Results.FirstOrDefault(r => r.Verdict == "CompileError")?.ErrorMessage,
                    runtimeError = run.Results.FirstOrDefault(r => r.Verdict == "RuntimeError")?.ErrorMessage,
                    verdicts = run.Results.Select(r => new { testCaseId = r.TestCaseId?.ToString() ?? "custom", isSample = true, verdict = r.Verdict, input = JudgeRules.Clip(r.Input, 4000), expected = JudgeRules.Clip(r.ExpectedOutput, 4000), actual = JudgeRules.Clip(r.ActualOutput, 4000) })
                }, Json);
            var theory = await rag.ForProblem(p.Id, $"{p.Title}\n{input.Language}\n{s?.Status}\n{input.Question}", new(studentId, false, false), ct);
            node["theory"] = JsonSerializer.SerializeToNode(theory, Json);
            // Leave room for the original prompt contract even for large student submissions.
            while (theory.Length > 0 && Encoding.UTF8.GetByteCount(node.ToJsonString()) > 95000)
            { theory = theory[..^1]; node["theory"] = JsonSerializer.SerializeToNode(theory, Json); }
            var hint = await client.Assist(JsonSerializer.SerializeToElement(node), ct);
            // Even a cached hint is withheld if its supporting material changed during generation.
            if (!await rag.StillAllowed(theory, new(studentId, false, false), ct)) throw new AiPolicyException("CONTEXT_CHANGED", 409);
            hint = hint with { References = hint.GuardStatus == "fallback" ? [] : theory.Select(Learning.RagService.Reference).ToArray() };
            var interaction = new AiInteraction { StudentId = studentId, ProblemId = p.Id, Language = input.Language, Level = input.Level,
                Stage = hint.Stage, GuardStatus = hint.GuardStatus, ModelVersion = hint.ModelVersion, Response = JsonSerializer.Serialize(hint, Json), CodeHash = similarityHash };
            db.Add(interaction); await db.SaveChangesAsync(ct);
            return new(interaction.Id, hint, AiPolicy.DailyQuota - count, Math.Max(maxLevel, hint.GuardStatus == "fallback" ? 1 : input.Level),
                hint.GuardStatus == "fallback" ? hint.ModelVersion == "rule-based" && hint.PromptVersion == "fallback-v1" ? "AI_UNAVAILABLE" : "GUARD_FALLBACK" : null);
        }
        finally
        {
            try { await cache.ScriptEvaluateAsync("if redis.call('GET',KEYS[1])==ARGV[1] then return redis.call('DEL',KEYS[1]) else return 0 end", [lockKey], [token]); }
            catch (RedisException) { /* Lock expires; keep an already persisted hint available. */ }
        }
    }
    public async Task<bool> Feedback(Guid studentId, Guid interactionId, bool helpful, CancellationToken ct)
        => await db.Set<AiInteraction>().Where(x => x.Id == interactionId && x.StudentId == studentId)
            .ExecuteUpdateAsync(x => x.SetProperty(i => i.Helpful, helpful), ct) == 1;
}
