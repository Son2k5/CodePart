using CodePath.Domain.Common.Exceptions;
using CodePath.Domain.Users.Entities;
using CodePath.Domain.Users.Enums;
using CodePath.Shared.Kernel.Enums;
using FluentAssertions;
using Xunit;

namespace CodePath.Architecture.Tests;

public sealed class UserAndEnrollmentTests
{
    [Fact]
    public void CreateStudent_ShouldNormalizeEmailAndSetStudentDefaults()
    {
        var now = DateTime.UtcNow;
        var student = User.CreateStudent(
            "Student Name",
            "  2026000101@HANU.EDU.VN  ",
            "password-hash",
            "2026000101",
            now);

        student.Email.Should().Be("2026000101@hanu.edu.vn");
        student.Role.Should().Be(UserRole.Student);
        student.Status.Should().Be(UserStatus.Active);
        student.StudentId.Should().Be("2026000101");
        student.EmailVerifiedAt.Should().BeNull();
    }

    [Fact]
    public void CreateTeacher_ShouldStartPendingWithoutStudentId()
    {
        var now = DateTime.UtcNow;
        var teacher = User.CreateTeacher(
            "Teacher Name",
            "teacher@hanu.edu.vn",
            "password-hash",
            now);

        teacher.Role.Should().Be(UserRole.Teacher);
        teacher.Status.Should().Be(UserStatus.Pending);
        teacher.StudentId.Should().BeNull();
    }

    [Fact]
    public void ApproveTeacher_WhenPending_ShouldActivateAccount()
    {
        var now = DateTime.UtcNow;
        var teacher = User.CreateTeacher("Teacher", "teacher-approve@hanu.edu.vn", "hash", now);

        teacher.ApproveTeacher(now);

        teacher.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public void RejectTeacher_ShouldBeTerminal()
    {
        var now = DateTime.UtcNow;
        var teacher = User.CreateTeacher("Teacher", "teacher-reject@hanu.edu.vn", "hash", now);
        teacher.RejectTeacher(now);

        Action approveRejectedTeacher = () => teacher.ApproveTeacher(now);

        approveRejectedTeacher.Should().Throw<DomainRuleViolationException>();
        teacher.Status.Should().Be(UserStatus.Rejected);
    }

    [Fact]
    public void Disable_WhenActive_ShouldDisableAccount()
    {
        var now = DateTime.UtcNow;
        var teacher = User.CreateTeacher("Teacher", "teacher-disable@hanu.edu.vn", "hash", now);
        teacher.ApproveTeacher(now);

        teacher.Disable(Guid.NewGuid(), now);

        teacher.Status.Should().Be(UserStatus.Disabled);
    }

    [Fact]
    public void Disable_WhenActorIsTarget_ShouldThrow()
    {
        var now = DateTime.UtcNow;
        var admin = User.CreateAdmin("Admin", "admin-self-disable@hanu.edu.vn", "hash", now);

        Action selfDisable = () => admin.Disable(admin.Id, now);

        selfDisable.Should().Throw<DomainRuleViolationException>();
        admin.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public void UpdateUnverifiedAccount_CannotChangeRole()
    {
        var now = DateTime.UtcNow;
        var teacher = User.CreateTeacher("Teacher", "teacher-role@hanu.edu.vn", "hash", now);

        Action changeRole = () => teacher.UpdateUnverifiedAccount(
            "Teacher",
            "new-hash",
            UserRole.Student,
            "2026000999",
            now);

        changeRole.Should().Throw<DomainRuleViolationException>();
        teacher.Role.Should().Be(UserRole.Teacher);
    }

    [Fact]
    public void CreateEnrollment_ShouldLeaveGradingOutcomeUnknown()
    {
        var now = DateTime.UtcNow;
        var enrollment = Enrollment.Create(Guid.NewGuid(), Guid.NewGuid(), now);

        enrollment.Status.Should().Be(EnrollmentStatus.Active);
        enrollment.MidtermScore.Should().BeNull();
        enrollment.FinalScore.Should().BeNull();
        enrollment.TotalScore.Should().BeNull();
        enrollment.IsPassed.Should().BeNull();
    }

    [Theory]
    [InlineData(8, 6, 6.8, true)]
    [InlineData(2, 3, 2.6, false)]
    public void UpdateScores_ShouldCalculateOutcome(
        double midterm,
        double final,
        double expectedTotal,
        bool expectedPassed)
    {
        var now = DateTime.UtcNow;
        var enrollment = Enrollment.Create(Guid.NewGuid(), Guid.NewGuid(), now);

        enrollment.UpdateScores((decimal)midterm, (decimal)final, now);

        enrollment.TotalScore.Should().Be((decimal)expectedTotal);
        enrollment.IsPassed.Should().Be(expectedPassed);
        enrollment.Status.Should().Be(
            expectedPassed ? EnrollmentStatus.Completed : EnrollmentStatus.Active);
    }

    [Fact]
    public void Drop_WhenEnrollmentIsCompleted_ShouldThrow()
    {
        var now = DateTime.UtcNow;
        var enrollment = Enrollment.Create(Guid.NewGuid(), Guid.NewGuid(), now);
        enrollment.UpdateScores(8m, 8m, now);

        Action act = () => enrollment.Drop(now);

        act.Should().Throw<DomainRuleViolationException>();
    }
}
