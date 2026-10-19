using CodePath.Application.Teaching.Commands;
using CodePath.Application.Teaching.Dtos;
using CodePath.Domain.Common.Exceptions;
using CodePath.Domain.Exercises.Entities;
using CodePath.Domain.Exercises.Enums;
using CodePath.Domain.Users.Entities;
using CodePath.Domain.Users.Enums;
using FluentAssertions;
using Xunit;

namespace CodePath.Unit.Tests.Teaching;

public sealed class TeachingDomainTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);
    [Fact]
    public void PublishedExercise_CannotBeMovedToAnotherCourse()
    {
        var e = Exercise.Create("slug", "Title", "Easy", "Description", "", "", 1000, 65536, Now);
        var course = Guid.NewGuid();
        e.AssignToCourse(course, Now);
        e.AddTestCase("1", "1", true, 1, null, Now);
        e.Publish(Now);
        var move = () => e.AssignToCourse(Guid.NewGuid(), Now);
        move.Should().Throw<DomainRuleViolationException>();
        e.CourseId.Should().Be(course);
    }
    [Theory]
    [InlineData(-1)]
    [InlineData(10.01)]
    [InlineData(1.001)]
    public void Grade_RejectsOutOfRangeOrExcessPrecision(decimal score)
    {
        var s = Submission.Create(Guid.NewGuid(), Guid.NewGuid(), "python", "code", Now);
        s.Complete(SubmissionStatus.WrongAnswer, [], 1, 1, 1, null, Now);
        var grade = () => s.Grade(score, null, Guid.NewGuid(), Now);
        grade.Should().Throw<DomainValidationException>();
    }
    [Fact]
    public void Grade_DoesNotOverwriteAutomaticVerdictOrTestResults()
    {
        var s = Submission.Create(Guid.NewGuid(), Guid.NewGuid(), "python", "code", Now);
        var gradePending = () => s.Grade(5, null, Guid.NewGuid(), Now);
        gradePending.Should().Throw<DomainRuleViolationException>();
        s.Complete(SubmissionStatus.WrongAnswer, [], 1, 1, 1, "error", Now);
        var grader = Guid.NewGuid();
        s.Grade(5.5m, " partial credit ", grader, Now);
        s.Status.Should().Be(SubmissionStatus.WrongAnswer);
        s.TotalTests.Should().Be(1);
        s.GradedBy.Should().Be(grader);
        s.TeacherFeedback.Should().Be("partial credit");
    }
    [Fact]
    public void Rejoin_OnlyAllowsDroppedEnrollment()
    {
        var enrollment = Enrollment.Create(Guid.NewGuid(), Guid.NewGuid(), Now);
        var rejoin = () => enrollment.Rejoin(Now);
        rejoin.Should().Throw<DomainRuleViolationException>();
        enrollment.Drop(Now);
        enrollment.Rejoin(Now);
        enrollment.Status.Should().Be(EnrollmentStatus.Active);
    }
    [Fact]
    public void ExerciseValidation_RequiresSampleAndBoundsJudgeResources()
    {
        var data = new CreateExerciseInput("Title", "Easy", "Description", "", "", 1000, 65536,
            [new TestInput("1", "1", true, null)]);
        var validator = new CreateCourseExerciseCommandValidator();
        validator.Validate(new CreateCourseExerciseCommand(Guid.NewGuid(), Guid.NewGuid(), data)).IsValid.Should().BeTrue();
        foreach (var invalid in new[] { data with { TestCases = [] }, data with { TimeLimitMs = 15001 },
            data with { TestCases = [new TestInput("secret", "secret", false, null)] } })
            validator.Validate(new CreateCourseExerciseCommand(Guid.NewGuid(), Guid.NewGuid(), invalid)).IsValid.Should().BeFalse();
    }
}
