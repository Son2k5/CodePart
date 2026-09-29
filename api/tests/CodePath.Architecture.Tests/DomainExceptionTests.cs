using CodePath.Domain.Common.Exceptions;
using CodePath.Domain.Users.Entities;
using FluentAssertions;
using Xunit;

namespace CodePath.Architecture.Tests;

public class DomainExceptionTests
{
    [Fact]
    public void CreateStudent_WithEmptyFullName_ShouldThrowDomainValidationException()
    {
        // Act
        var act = () => User.CreateStudent(
            fullName: "",
            email: "student@hanu.edu.vn",
            passwordHash: "hash123",
            studentId: "2001040001");

        // Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*Họ và tên không được để trống*");
    }

    [Fact]
    public void AssignToClass_WhenUserIsTeacher_ShouldThrowDomainRuleViolationException()
    {
        // Arrange
        var teacher = User.CreateTeacher(
            fullName: "Teacher Name",
            email: "teacher@hanu.edu.vn",
            passwordHash: "hash123");

        // Act
        var act = () => teacher.AssignToClass(Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainRuleViolationException>()
            .WithMessage("*Only students can be assigned to a class*");
    }

    [Fact]
    public void ActivateCourse_WhenAlreadyActive_ShouldThrowDomainRuleViolationException()
    {
        // Arrange
        var course = Course.Create(
            subjectName: "C# Programming",
            sectionCode: "CS101",
            language: "Vietnamese",
            semester: "Fall 2026",
            joinCode: "CODE12",
            teacherId: Guid.NewGuid());

        course.Activate();

        // Act
        var act = () => course.Activate();

        // Assert
        act.Should().Throw<DomainRuleViolationException>()
            .WithMessage("*Course is already active*");
    }
}
