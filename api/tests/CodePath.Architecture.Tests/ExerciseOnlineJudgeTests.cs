using CodePath.Domain.Common.Exceptions;
using CodePath.Domain.Exercises.Entities;
using FluentAssertions;
using Xunit;

namespace CodePath.Architecture.Tests;

public sealed class ExerciseOnlineJudgeTests
{
    private static readonly DateTime UtcNow = new(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Publish_WithoutTestCases_ShouldBeRejected()
    {
        var exercise = CreateExercise();

        var action = () => exercise.Publish(UtcNow);

        action.Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void PublishedExercise_ShouldKeepSampleAndHiddenTests()
    {
        var exercise = CreateExercise();
        exercise.AddTestCase("1\n", "1\n", true, 1, "sample", UtcNow);
        exercise.AddTestCase("2\n", "2\n", false, 2, null, UtcNow);

        exercise.Publish(UtcNow);

        exercise.IsPublished.Should().BeTrue();
        exercise.TestCases.Should().HaveCount(2);
        exercise.TestCases.Count(test => test.IsSample).Should().Be(1);
    }

    private static Exercise CreateExercise() => Exercise.Create(
        "echo",
        "Echo",
        "Easy",
        "Echo the input.",
        "No constraints.",
        "print(input())",
        1_000,
        64_000,
        UtcNow);
}
