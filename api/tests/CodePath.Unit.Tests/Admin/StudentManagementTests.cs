using System.Text.Json;
using CodePath.Application.Admin.Commands;
using CodePath.Application.Admin.Dtos;
using CodePath.Application.Admin.Queries;
using CodePath.Domain.Common.Exceptions;
using CodePath.Domain.Users.Entities;
using CodePath.Shared.Kernel.Enums;
using FluentAssertions;
using Xunit;

namespace CodePath.Unit.Tests.Admin;

public sealed class StudentManagementTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AdminCreation_VerifiesEmailAndNormalizesProfile()
    {
        var faculty = Guid.NewGuid();
        var student = User.CreateStudentByAdmin(" Student ", "s@hanu.edu.vn", "hashed", " 2101040001 ", faculty,
            " 0901234567 ", " bio ", UserStatus.Active, Now);
        student.Status.Should().Be(UserStatus.Active);
        student.EmailVerifiedAt.Should().Be(Now);
        student.StudentId.Should().Be("2101040001");
        student.FacultyId.Should().Be(faculty);
        student.PhoneNumber.Should().Be("0901234567");
        student.Bio.Should().Be("bio");
    }

    [Fact]
    public void ApproveStudent_WhenPending_ShouldActivateAccount()
    {
        var student = User.CreateStudentByAdmin("Student", "s@hanu.edu.vn", "hash", "2101040002", null,
            null, null, UserStatus.Pending, Now);
        student.Status.Should().Be(UserStatus.Pending);

        student.ApproveStudent(Now);
        student.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public void RejectStudent_ShouldBeTerminal()
    {
        var student = User.CreateStudentByAdmin("Student", "s@hanu.edu.vn", "hash", "2101040003", null,
            null, null, UserStatus.Pending, Now);
        student.RejectStudent(Now);

        Action approveRejected = () => student.ApproveStudent(Now);
        approveRejected.Should().Throw<DomainRuleViolationException>();
        student.Status.Should().Be(UserStatus.Rejected);
    }

    [Fact]
    public void StudentProfile_RejectsTeachersAndOversizedFields()
    {
        var teacher = User.CreateTeacher("Teacher", "t@hanu.edu.vn", "hash", Now);
        var updateTeacherAsStudent = () => teacher.UpdateStudentProfile("Name", "2101040004", null, null, null, Now);
        updateTeacherAsStudent.Should().Throw<DomainRuleViolationException>();

        var student = User.CreateStudent("Student", "s@hanu.edu.vn", "hash", "2101040005", Now);
        var oversized = () => student.UpdateStudentProfile("Name", new string('x', 51), null, null, null, Now);
        oversized.Should().Throw<DomainValidationException>();
    }

    [Theory]
    [InlineData(0, 20, "name_asc")]
    [InlineData(1, 101, "name_asc")]
    [InlineData(int.MaxValue, 100, "created_desc")]
    [InlineData(1, 20, "email_asc")]
    public void InvalidPaginationOrSort_IsRejected(int page, int size, string sort)
    {
        new StudentFilterValidator().Validate(new StudentFilter(page, size, SortBy: sort)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Create_ValidatesSchoolEmailPasswordAndInitialStatus()
    {
        var valid = new CreateStudentInput("Name", "student@hanu.edu.vn", "2101040001", null, "+84901234567", null, "SecurePassword1!");
        var validator = new CreateStudentCommandValidator();
        validator.Validate(new CreateStudentCommand(Guid.NewGuid(), valid, null)).IsValid.Should().BeTrue();

        foreach (var invalid in new[] {
            valid with { Email = "student@hanu.edu.vn.evil" },
            valid with { TemporaryPassword = "weak" },
            valid with { Status = UserStatus.Disabled },
            valid with { Phone = "not a phone" },
            valid with { StudentId = "" }
        })
        {
            validator.Validate(new CreateStudentCommand(Guid.NewGuid(), invalid, null)).IsValid.Should().BeFalse();
        }

        var json = JsonSerializer.Serialize(valid);
        json.Should().Contain("\"Status\":\"Active\"");
        JsonSerializer.Deserialize<CreateStudentInput>(json)!.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public void Bulk_RejectsDuplicateAndEmptySelections()
    {
        var validator = new BulkStudentsCommandValidator();
        var id = Guid.NewGuid();
        validator.Validate(new BulkStudentsCommand(Guid.NewGuid(), [id, id], true, null)).IsValid.Should().BeFalse();
        validator.Validate(new BulkStudentsCommand(Guid.NewGuid(), [], true, null)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void DeleteCommand_ValidatesActorAndTargetId()
    {
        var validator = new DeleteStudentCommandValidator();
        validator.Validate(new DeleteStudentCommand(Guid.Empty, Guid.NewGuid(), null)).IsValid.Should().BeFalse();
        validator.Validate(new DeleteStudentCommand(Guid.NewGuid(), Guid.Empty, null)).IsValid.Should().BeFalse();
        validator.Validate(new DeleteStudentCommand(Guid.NewGuid(), Guid.NewGuid(), "127.0.0.1")).IsValid.Should().BeTrue();
    }

    [Fact]
    public void BulkDeleteCommand_ValidatesIds()
    {
        var validator = new BulkDeleteStudentsCommandValidator();
        var id = Guid.NewGuid();
        validator.Validate(new BulkDeleteStudentsCommand(Guid.Empty, [id], null)).IsValid.Should().BeFalse();
        validator.Validate(new BulkDeleteStudentsCommand(Guid.NewGuid(), [], null)).IsValid.Should().BeFalse();
        validator.Validate(new BulkDeleteStudentsCommand(Guid.NewGuid(), [id, id], null)).IsValid.Should().BeFalse();
        validator.Validate(new BulkDeleteStudentsCommand(Guid.NewGuid(), [id, Guid.NewGuid()], null)).IsValid.Should().BeTrue();
    }
}
