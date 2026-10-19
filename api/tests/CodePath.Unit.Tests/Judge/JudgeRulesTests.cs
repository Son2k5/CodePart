using System.Text;
using System.Text.Json;
using CodePath.Application.Judge;
using CodePath.Domain.Judge;
using Xunit;

namespace CodePath.Unit.Tests.Judge;

public class JudgeRulesTests
{
    [Theory]
    [InlineData("1  \r\n2\n", "1\n2", true)]
    [InlineData(" 1\n2", "1\n2", false)]
    [InlineData("1\n\n2", "1\n2", false)]
    [InlineData("", "\n", true)]
    public void Lines_preserve_leading_and_interior_whitespace(string expected, string actual, bool equal) => Assert.Equal(equal, JudgeRules.Equal(expected, actual, ComparisonMode.Lines));
    [Fact] public void Tokens_ignore_whitespace_but_not_order()
    {
        Assert.True(JudgeRules.Equal("1\t 2\n3", "1 2 3", ComparisonMode.Tokens));
        Assert.False(JudgeRules.Equal("1 2", "2 1", ComparisonMode.Tokens));
    }
    [Fact] public void Aggregate_returns_first_failure_and_rejects_empty_results()
    {
        Assert.Equal(Verdict.WrongAnswer, JudgeRules.Aggregate([Verdict.Accepted, Verdict.WrongAnswer, Verdict.TimeLimitExceeded]));
        Assert.Equal(Verdict.Accepted, JudgeRules.Aggregate([Verdict.Accepted, Verdict.Accepted]));
        Assert.Equal(Verdict.InternalError, JudgeRules.Aggregate([]));
    }
    [Fact] public void Partial_score_uses_weights_not_pass_count()
    {
        var a = new TestCase { Weight = 1 }; var b = new TestCase { Weight = 3 };
        var p = new Problem { Points = 100, TestCases = [a, b] };
        Assert.Equal(25m, JudgeRules.Score(p, [new() { TestCaseId = a.Id, Verdict = Verdict.Accepted }, new() { TestCaseId = b.Id, Verdict = Verdict.WrongAnswer }]));
    }
    [Fact] public void Hidden_projection_never_exposes_sensitive_fields_even_if_storage_has_them()
    {
        var r = new SubmissionResult { TestCaseId = Guid.NewGuid(), Input = "SECRET_INPUT", ExpectedOutput = "SECRET_EXPECTED", ActualOutput = "SECRET_ACTUAL", ErrorMessage = "SECRET_ERROR", IsSample = false };
        var json = JsonSerializer.Serialize(JudgeRules.StudentResult(r));
        Assert.DoesNotContain("SECRET", json);
        Assert.DoesNotContain(r.TestCaseId.ToString(), json);
        r.IsSample = true;
        Assert.Contains("SECRET_INPUT", JsonSerializer.Serialize(JudgeRules.StudentResult(r)));
    }
    [Fact] public void Validation_counts_utf8_bytes_and_accepts_empty_custom_input()
    {
        var v = new CodeRequestValidator();
        Assert.False(v.Validate(new CodeRequest("python", new string('界', 30000))).IsValid);
        Assert.True(v.Validate(new CodeRequest("python", "print(1)", CustomInput: "")).IsValid);
        Assert.False(v.Validate(new CodeRequest("python", "print(1)", [Guid.NewGuid()], "")).IsValid);
        Assert.Equal(90000, Encoding.UTF8.GetByteCount(new string('界', 30000)));
    }
    [Fact] public void Publish_requires_ten_to_fifteen_and_one_to_two_samples()
    {
        var p = new Problem { TestCases = Enumerable.Range(1, 10).Select(i => new TestCase { Order = i, IsSample = i == 1 }).ToList() };
        Assert.True(p.CanPublish); p.TestCases[1].IsSample = true; Assert.True(p.CanPublish);
        p.TestCases[2].IsSample = true; Assert.False(p.CanPublish);
        p.TestCases.RemoveAt(0); Assert.False(p.CanPublish);
    }
    [Fact] public void New_problem_must_belong_to_exactly_one_class_or_course()
    {
        var validator = new ProblemWriteValidator();
        var request = new ProblemWrite("Sum", "Description", "Easy", 1000, 64, 100, null, ComparisonMode.Lines, [new("python", "print(1)")]);
        Assert.False(validator.Validate(request).IsValid);
        Assert.True(validator.Validate(request with { ClassId = Guid.NewGuid() }).IsValid);
        Assert.True(validator.Validate(request with { CourseId = Guid.NewGuid() }).IsValid);
        Assert.False(validator.Validate(request with { ClassId = Guid.NewGuid(), CourseId = Guid.NewGuid() }).IsValid);
    }
}
