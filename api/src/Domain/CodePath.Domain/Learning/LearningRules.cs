namespace CodePath.Domain.Learning;

public sealed record CoveredRange(double Start, double End);
public static class Coverage
{
    public static IReadOnlyList<CoveredRange> Merge(IEnumerable<CoveredRange> ranges)
    {
        var result = new List<CoveredRange>();
        foreach (var r in ranges.Where(r => double.IsFinite(r.Start) && double.IsFinite(r.End) && r.Start >= 0 && r.End > r.Start).OrderBy(r => r.Start))
            if (result.Count == 0 || result[^1].End < r.Start) result.Add(r);
            else result[^1] = new(result[^1].Start, Math.Max(result[^1].End, r.End));
        return result;
    }
    public static IReadOnlyList<CoveredRange> Heartbeat(IEnumerable<CoveredRange> ranges, double? previous, double position, double duration, TimeSpan elapsed)
    {
        if (!double.IsFinite(position) || position < 0 || position > duration) throw new ArgumentOutOfRangeException(nameof(position));
        return Merge(previous.HasValue && elapsed.TotalSeconds >= 0 && position > previous && position - previous <= elapsed.TotalSeconds * 2 + 2
            ? ranges.Append(new(previous.Value, position)) : ranges);
    }
    public static decimal Percent(IEnumerable<CoveredRange> ranges, double duration) => duration <= 0 ? 0 :
        (decimal)Math.Clamp(Merge(ranges).Sum(x => x.End - x.Start) * 100 / duration, 0, 100);
}
public static class CompletionEvaluator
{
    public static bool CanMarkManually(CompletionRule r) => r.AllowManualMarkRead && r.RequireVideoPercent == null && r.RequireDocumentPercent == null && r.RequireExercises == "none";
    public static bool Complete(CompletionRule r, IEnumerable<decimal> videos, IEnumerable<decimal> documents,
        int passed, int total, int requiredPassed, int requiredTotal, bool manual)
    {
        if (manual) return CanMarkManually(r);
        if (r.RequireVideoPercent == null && r.RequireDocumentPercent == null && r.RequireExercises == "none") return false;
        if (r.RequireVideoPercent is { } v && videos.Any(x => x < v)) return false;
        if (r.RequireDocumentPercent is { } d && documents.Any(x => x < d)) return false;
        return r.RequireExercises switch {
            "none" => true, "all_required" => requiredPassed == requiredTotal,
            _ => r.RequireExercises.StartsWith("percent:", StringComparison.Ordinal) && decimal.TryParse(r.RequireExercises[8..], System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var p)
                && (total == 0 || passed * 100m / total >= p)
        };
    }
}
public enum LateMode { None, PercentPerDay, FixedPercent, Tiered }
public sealed record LateTier(decimal AfterDays, decimal Percent);
public sealed record LatePolicy(LateMode Mode = LateMode.None, decimal PercentPerDay = 0, decimal FixedPercent = 0,
    LateTier[]? Tiers = null, decimal MaxPenaltyPercent = 100, int GracePeriodMinutes = 0, decimal? HardCloseAfterDays = null);
public sealed record ScoreCategory(string Name, decimal Weight, Guid[] TopicIds);
public sealed record ScoreConfiguration(LatePolicy LatePolicy, ScoreCategory[] Categories);
public sealed record ScoreResult(decimal BasePoints, decimal PenaltyPercent, decimal LateByMinutes, decimal EffectivePoints);
public static class ScoreCalculator
{
    public static ScoreResult Calculate(decimal points, DateTime submittedAt, DateTime? dueAt, DateTime? extension, LatePolicy policy)
    {
        var deadline = extension.HasValue && (!dueAt.HasValue || extension > dueAt) ? extension : dueAt;
        var late = deadline.HasValue ? Math.Max(0, (decimal)(submittedAt - deadline.Value).TotalMinutes) : 0;
        var days = Math.Max(0, late - policy.GracePeriodMinutes) / 1440;
        var penalty = days <= 0 ? 0 : policy.Mode switch {
            LateMode.PercentPerDay => decimal.Ceiling(days) * policy.PercentPerDay,
            LateMode.FixedPercent => policy.FixedPercent,
            LateMode.Tiered => (policy.Tiers ?? []).Where(t => days > t.AfterDays).OrderByDescending(t => t.AfterDays).FirstOrDefault()?.Percent ?? 0,
            _ => 0
        };
        penalty = Math.Clamp(penalty, 0, Math.Clamp(policy.MaxPenaltyPercent, 0, 100));
        if (policy.HardCloseAfterDays is { } close && late / 1440 > close) penalty = 100;
        return new(points, penalty, late, decimal.Round(Math.Max(0, points * (1 - penalty / 100)), 2));
    }
    public static ScoreResult Best(IEnumerable<(decimal Points, DateTime SubmittedAt)> submissions, DateTime? due, DateTime? extension, LatePolicy policy) =>
        submissions.Select(s => Calculate(s.Points, s.SubmittedAt, due, extension, policy)).OrderByDescending(s => s.EffectivePoints).FirstOrDefault() ?? new(0, 0, 0, 0);
}
public static class StreakCalculator
{
    public static (int Current, int Longest, bool ActiveToday) Calculate(IEnumerable<DateOnly> activity, DateOnly today)
    {
        var days = activity.Where(d => d <= today).Distinct().Order().ToArray();
        var longest = 0; var run = 0; DateOnly? previous = null;
        foreach (var day in days) { run = previous == day.AddDays(-1) ? run + 1 : 1; longest = Math.Max(longest, run); previous = day; }
        return (previous == today || previous == today.AddDays(-1) ? run : 0, longest, previous == today);
    }
}
