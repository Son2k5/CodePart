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

public sealed class TeacherManagementTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AdminCreation_VerifiesEmailAndNormalizesProfile()
    {
        var faculty = Guid.NewGuid();
        var teacher = User.CreateTeacherByAdmin(" Teacher ", "T@hanu.edu.vn", "hashed", " gv-1 ", faculty,
            " 0901234567 ", " bio ", UserStatus.Active, Now);
        teacher.Status.Should().Be(UserStatus.Active);
        teacher.EmailVerifiedAt.Should().Be(Now);
        teacher.TeacherId.Should().Be("GV-1");
        teacher.FacultyId.Should().Be(faculty);
        teacher.PhoneNumber.Should().Be("0901234567");
        teacher.Bio.Should().Be("bio");
    }

    [Fact]
    public void Enable_OnlyAllowsDisabledAndInvalidatesOldTokenVersions()
    {
        var teacher = User.CreateTeacher("Teacher", "t@hanu.edu.vn", "hash", Now);
        var enable = () => teacher.Enable(Now);
        enable.Should().Throw<DomainRuleViolationException>();
        teacher.ApproveTeacher(Now);
        enable.Should().Throw<DomainRuleViolationException>();
        teacher.Disable(Guid.NewGuid(), Now);
        var previousVersion = teacher.SecurityVersion;
        teacher.Enable(Now);
        teacher.Status.Should().Be(UserStatus.Active);
        teacher.SecurityVersion.Should().Be(previousVersion + 1);
    }

    [Fact]
    public void TeacherProfile_RejectsStudentsAndOversizedFieldsAndCanClearOptionalFields()
    {
        var student = User.CreateStudent("Student", "s@hanu.edu.vn", "hash", "1", Now);
        var updateStudent = () => student.UpdateTeacherProfile("Name", null, null, null, null, Now);
        updateStudent.Should().Throw<DomainRuleViolationException>();
        var teacher = User.CreateTeacher("Teacher", "t@hanu.edu.vn", "hash", Now);
        var oversized = () => teacher.UpdateTeacherProfile("Name", new string('x', 51), null, null, null, Now);
        oversized.Should().Throw<DomainValidationException>();
        teacher.UpdateTeacherProfile("Name", " ", null, " ", " ", Now);
        teacher.TeacherId.Should().BeNull();
        teacher.PhoneNumber.Should().BeNull();
        teacher.Bio.Should().BeNull();
    }

    [Theory]
    [InlineData(0, 20, "name_asc")]
    [InlineData(1, 101, "name_asc")]
    [InlineData(int.MaxValue, 100, "created_desc")]
    [InlineData(1, 20, "email_asc")]
    public void InvalidPaginationOrSort_IsRejected(int page, int size, string sort)
    {
        new TeacherFilterValidator().Validate(new TeacherFilter(page, size, SortBy: sort)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Create_ValidatesSchoolEmailPasswordAndInitialStatus()
    {
        var valid = new CreateTeacherInput("Name", "teacher@hanu.edu.vn", "GV-1", null, "+84901234567", null, "SecurePassword1!");
        var validator = new CreateTeacherCommandValidator();
        validator.Validate(new CreateTeacherCommand(Guid.NewGuid(), valid, null)).IsValid.Should().BeTrue();
        foreach (var invalid in new[] { valid with { Email = "teacher@hanu.edu.vn.evil" },
            valid with { TemporaryPassword = "weak" }, valid with { Status = UserStatus.Disabled },
            valid with { Phone = "not a phone" } })
            validator.Validate(new CreateTeacherCommand(Guid.NewGuid(), invalid, null)).IsValid.Should().BeFalse();
        var json = JsonSerializer.Serialize(valid);
        json.Should().Contain("\"Status\":\"Active\"");
        JsonSerializer.Deserialize<CreateTeacherInput>(json)!.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public void Bulk_RejectsDuplicateAndEmptySelections()
    {
        var validator = new BulkTeachersCommandValidator();
        var id = Guid.NewGuid();
        validator.Validate(new BulkTeachersCommand(Guid.NewGuid(), [id, id], true, null)).IsValid.Should().BeFalse();
        validator.Validate(new BulkTeachersCommand(Guid.NewGuid(), [], true, null)).IsValid.Should().BeFalse();
    }
}
