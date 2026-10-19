using System.Text;
using CodePath.Application.Learning;
using CodePath.Domain.Learning;
using CodePath.Domain.Users.Entities;
using CodePath.Domain.Common.Exceptions;
using CodePath.Infrastructure.Learning;
using Xunit;
namespace CodePath.Unit.Tests.Learning;
public sealed class LearningRulesTests
{
    [Fact] public void Coverage_merges_overlap_and_is_idempotent()
    {
        CoveredRange[] input = [new(30, 45), new(0, 20), new(15, 35), new(50, 60)];
        var merged = Coverage.Merge(input); Assert.Equal(new[] { new CoveredRange(0, 45), new CoveredRange(50, 60) }, merged);
        Assert.Equal(merged, Coverage.Merge(input.Concat(input))); Assert.Equal(55, Coverage.Percent(merged, 100));
    }
    [Fact] public void Seek_to_end_does_not_count_as_watched()
    {
        Assert.Empty(Coverage.Heartbeat([], 0, 1000, 1000, TimeSpan.FromSeconds(15)));
        Assert.Empty(Coverage.Heartbeat([], null, 1000, 1000, TimeSpan.FromSeconds(15)));
        Assert.Empty(Coverage.Heartbeat([], 30, 15, 1000, TimeSpan.FromSeconds(15)));
        Assert.Single(Coverage.Heartbeat([], 0, 32, 1000, TimeSpan.FromSeconds(15)));
        Assert.Empty(Coverage.Heartbeat([], 0, 33, 1000, TimeSpan.FromSeconds(15)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Coverage.Heartbeat([], 0, double.NaN, 100, TimeSpan.FromSeconds(15)));
    }
    [Theory]
    [InlineData(80, 80, 2, true)] [InlineData(79, 80, 2, false)] [InlineData(80, 79, 2, false)] [InlineData(80, 80, 1, false)]
    public void Completion_requires_all_configured_conditions(int video, int pdf, int passed, bool expected)
    { Assert.Equal(expected, CompletionEvaluator.Complete(new(80, 80, "all_required"), [video], [pdf], passed, 2, passed, 2, false)); }
    [Fact] public void Manual_completion_cannot_bypass_required_media_or_exercises()
    {
        Assert.True(CompletionEvaluator.CanMarkManually(new()));
        Assert.False(CompletionEvaluator.CanMarkManually(new(80)));
        Assert.False(CompletionEvaluator.CanMarkManually(new(RequireExercises: "percent:70")));
        Assert.False(CompletionEvaluator.Complete(new(80), [100], [], 0, 0, 0, 0, true));
        Assert.True(CompletionEvaluator.Complete(new(RequireExercises: "percent:70"), [], [], 7, 10, 0, 0, false));
        Assert.False(new CompletionRuleValidator().Validate(new CompletionRule(RequireExercises: "percent:101")).IsValid);
    }
    private static readonly DateTime Due = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
    [Theory]
    [InlineData(LateMode.None, 100)] [InlineData(LateMode.FixedPercent, 80)] [InlineData(LateMode.PercentPerDay, 90)]
    public void Score_policy_modes(LateMode mode, int expected)
    { Assert.Equal(expected, ScoreCalculator.Calculate(100, Due.AddHours(1), Due, null, new(mode, PercentPerDay: 10, FixedPercent: 20)).EffectivePoints); }
    [Fact] public void Grace_extension_hard_close_and_maximum_penalty()
    {
        var p = new LatePolicy(LateMode.PercentPerDay, 25, GracePeriodMinutes: 10, MaxPenaltyPercent: 50, HardCloseAfterDays: 4);
        Assert.Equal(100, ScoreCalculator.Calculate(100, Due.AddMinutes(10), Due, null, p).EffectivePoints);
        Assert.Equal(75, ScoreCalculator.Calculate(100, Due.AddMinutes(11), Due, null, p).EffectivePoints);
        Assert.Equal(50, ScoreCalculator.Calculate(100, Due.AddDays(3), Due, null, p).EffectivePoints);
        Assert.Equal(0, ScoreCalculator.Calculate(100, Due.AddDays(5), Due, null, p).EffectivePoints);
        Assert.Equal(100, ScoreCalculator.Calculate(100, Due.AddDays(5), Due, Due.AddDays(6), p).EffectivePoints);
        Assert.Equal(100, ScoreCalculator.Calculate(100, Due.AddDays(50), null, null, p).EffectivePoints);
    }
    [Fact] public void Tiered_penalty_and_best_submission_use_submission_time()
    {
        var p = new LatePolicy(LateMode.Tiered, Tiers: [new(0, 10), new(1, 50)]);
        Assert.Equal(90, ScoreCalculator.Calculate(100, Due.AddHours(1), Due, null, p).EffectivePoints);
        Assert.Equal(50, ScoreCalculator.Calculate(100, Due.AddDays(2), Due, null, p).EffectivePoints);
        var best = ScoreCalculator.Best([(80, Due), (100, Due.AddDays(2))], Due, null, p);
        Assert.Equal(80, best.EffectivePoints); Assert.Equal(0, best.PenaltyPercent);
    }
    [Fact] public void Publish_is_a_snapshot_and_increments_version()
    {
        var t = new Topic { DraftContent = "[{\"value\":1}]" }; t.Publish(Due);
        t.DraftContent = "[{\"value\":2}]"; Assert.Equal("[{\"value\":1}]", t.PublishedContent); Assert.Equal(1, t.PublishedVersion);
        t.Publish(Due.AddMinutes(1)); Assert.Equal(t.DraftContent, t.PublishedContent); Assert.Equal(2, t.PublishedVersion);
    }
    [Fact] public void Unlock_rules_check_publication_and_clock_boundaries()
    {
        var t = new Topic(); Assert.Equal("Draft", t.LockedReason(Due, true)); t.Publish(Due); t.PublishAt = Due.AddSeconds(1);
        Assert.Equal("Scheduled", t.LockedReason(Due, true)); Assert.Null(t.LockedReason(Due.AddSeconds(1), true));
        t.PublishAt = null; t.UnlockRule = UnlockRule.AfterDate; t.UnlockAt = Due;
        Assert.Null(t.LockedReason(Due, true)); Assert.Equal("AfterDate", t.LockedReason(Due.AddTicks(-1), true));
        t.UnlockRule = UnlockRule.AfterPreviousCompleted; Assert.Equal("PreviousIncomplete", t.LockedReason(Due, false)); Assert.Null(t.LockedReason(Due, true));
    }
    [Fact] public void Self_study_rejects_teachers()
    {
        var c = Class.CreateManaged("Learning", Guid.NewGuid(), Due); c.SetMode(ClassMode.SelfStudy, Due);
        Assert.Throws<DomainRuleViolationException>(c.EnsureTeacherAssignmentAllowed);
        c.SetMode(ClassMode.Managed, Due); c.Teachers.Add(ClassTeacher.Create(c.Id, Guid.NewGuid(), Guid.NewGuid(), Due));
        Assert.Throws<DomainRuleViolationException>(() => c.SetMode(ClassMode.SelfStudy, Due));
    }
    [Fact] public void Streak_handles_duplicates_gaps_and_yesterday()
    {
        var today = new DateOnly(2026, 10, 16);
        var days = Enumerable.Range(1, 7).Select(i => today.AddDays(-i)).ToArray();
        Assert.Equal((7, 7, false), StreakCalculator.Calculate(days.Concat(days), today));
        Assert.Equal((8, 8, true), StreakCalculator.Calculate(days.Append(today), today));
        Assert.Equal((0, 7, false), StreakCalculator.Calculate(days, today.AddDays(1)));
    }
    [Fact] public void Sanitizer_removes_active_html_and_preserves_code_fences()
    {
        const string fence = "```html\n<script>alert(1)</script>\n```\n";
        var result = ContentValidator.Markdown("<script>bad()</script><b onclick='bad()'>safe</b>\n" + fence);
        Assert.DoesNotContain("onclick", result); Assert.Contains(fence, result); Assert.DoesNotContain("bad()", result);
    }
    [Fact] public void Fake_mime_is_rejected()
    {
        Assert.True(LearningAssets.ValidMagic("application/pdf", Encoding.ASCII.GetBytes("%PDF-1.7")));
        Assert.False(LearningAssets.ValidMagic("image/png", Encoding.ASCII.GetBytes("%PDF-1.7")));
        Assert.False(LearningAssets.ValidMagic("image/svg+xml", Encoding.ASCII.GetBytes("<svg>")));
    }
}
