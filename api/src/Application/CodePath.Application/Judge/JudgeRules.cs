using CodePath.Domain.Judge;

namespace CodePath.Application.Judge;

public static class JudgeRules
{
    public const int SourceBytes = 65536;
    public const int IoBytes = 1048576;
    public const int RunPerMinute = 10;
    public const int SubmitPerMinute = 5;
    public static string? Clip(string? text, int max = 16384) => text?.Length > max ? text[..max] : text;
    public static bool Equal(string expected, string actual, ComparisonMode mode)
    {
        if (mode == ComparisonMode.Tokens)
            return expected.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .SequenceEqual(actual.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        static string Normalize(string text) => string.Join('\n', text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Select(l => l.TrimEnd())).TrimEnd('\n');
        return Normalize(expected) == Normalize(actual);
    }
    public static Verdict Aggregate(IEnumerable<Verdict> verdicts)
    {
        var values = verdicts.ToArray();
        return values.Length == 0 ? Verdict.InternalError : values.FirstOrDefault(v => v != Verdict.Accepted, Verdict.Accepted);
    }
    public static decimal Score(Problem problem, IEnumerable<SubmissionResult> results)
    {
        var passed = results.Where(r => r.Verdict == Verdict.Accepted).Select(r => r.TestCaseId).ToHashSet();
        var weight = problem.TestCases.Sum(t => t.Weight);
        return weight == 0 ? 0 : decimal.Round(problem.Points * (decimal)problem.TestCases.Where(t => passed.Contains(t.Id)).Sum(t => t.Weight) / weight, 2);
    }
    public static TestDto StudentResult(SubmissionResult r) => r.IsSample
        ? new(r.TestCaseId, r.Index, true, r.Verdict.ToString(), r.RuntimeMs, r.MemoryKb, r.Input, r.ExpectedOutput, r.ActualOutput, r.ErrorMessage)
        : new(null, r.Index, false, r.Verdict.ToString(), r.RuntimeMs);
    public static SubmissionDto ToDto(Submission s, bool source = false) => new(s.Id, s.ProblemId, s.Language,
        s.Status.ToString(), s.PassedCount, s.TotalCount, s.Score, s.AwardedPoints, s.MaxRuntimeMs, s.MaxMemoryKb,
        s.CompileOutput, s.CreatedAt, s.FinishedAt, s.Results.OrderBy(r => r.Index).Select(StudentResult).ToArray(), source ? s.SourceCode : null);
}
