using CodePath.Application.Classes;
using CodePath.Domain.Common.Exceptions;
using CodePath.Domain.Users.Entities;
using CodePath.Shared.Kernel.Enums;
using FluentAssertions;
using Moq;
using Xunit;

namespace CodePath.Unit.Tests.Classes;

public class ClassManagementTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);
    private static Class Create(ClassAccessType access = ClassAccessType.Public, int? capacity = 1, DateTime? expires = null)
    {
        var c = Class.CreateManaged("Test class", Guid.NewGuid(), Now);
        c.Configure(c.Name, "", null, access, capacity, null, null, access == ClassAccessType.Public ? null : "ABCDEFGH", expires, Now);
        c.SetStatus(ClassStatus.Active, Now); return c;
    }
    private static void Error(Action action, string code) => action.Should().Throw<DomainRuleViolationException>().Which.ErrorCode.Should().Be(code);
    [Fact] public async Task Generator_retries_collision()
    {
        var generator = new SequenceGenerator(); var seen = new List<string>();
        var code = await generator.GenerateAsync((x, _) => { seen.Add(x); return Task.FromResult(x == "ABCDEFGH"); }, default);
        code.Should().Be("JKLMNPQR"); seen.Should().Equal("ABCDEFGH", "JKLMNPQR");
    }
    [Fact] public async Task Generator_uses_eight_unambiguous_characters()
    {
        var codes = new HashSet<string>(); var generator = new JoinCodeGenerator();
        for (var i = 0; i < 1000; i++)
        {
            var code = await generator.GenerateAsync((x, _) => Task.FromResult(codes.Contains(x)), default);
            code.Should().HaveLength(8); code.All(JoinCodeGenerator.Alphabet.Contains).Should().BeTrue(); codes.Add(code).Should().BeTrue();
        }
    }
    [Fact] public void Full_class_rejects_join() => Error(() => Create().EnsureCanJoin(false, 1, false, Now), "CLASS_FULL");
    [Fact] public void Duplicate_join_is_reported_before_capacity() => Error(() => Create().EnsureCanJoin(false, 1, true, Now), "ALREADY_ENROLLED");
    [Fact] public void Expiry_is_inclusive() => Error(() => Create(ClassAccessType.CodeRequired, expires: Now).EnsureCanJoin(true, 0, false, Now), "JOIN_CODE_EXPIRED");
    [Fact] public void Revoked_code_rejects_join()
    {
        var c = Create(ClassAccessType.CodeRequired); c.SetJoinCode(null, null, Now);
        Error(() => c.EnsureCanJoin(true, 0, false, Now), "INVALID_JOIN_CODE");
    }
    [Fact] public void Switching_access_clears_and_replaces_code()
    {
        var c = Create(ClassAccessType.CodeRequired, expires: Now.AddHours(1));
        c.Configure(c.Name, "", null, ClassAccessType.Public, null, null, null, null, null, Now);
        c.JoinCode.Should().BeNull(); c.JoinCodeExpiresAt.Should().BeNull();
        c.Configure(c.Name, "", null, ClassAccessType.CodeRequired, null, null, null, "JKLMNPQR", null, Now);
        c.JoinCode.Should().Be("JKLMNPQR");
    }
    [Theory] [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)]
    public void Occupied_class_requires_force_and_confirmation(bool force, bool confirm) => Error(() => Create().Delete(1, force, confirm, Now), "CLASS_HAS_STUDENTS");
    [Fact] public void Confirmed_force_soft_deletes()
    {
        var c = Create(ClassAccessType.CodeRequired); c.Delete(1, true, true, Now);
        c.IsDeleted.Should().BeTrue(); c.DeletedAt.Should().Be(Now); c.JoinCode.Should().BeNull();
    }
    [Theory] [InlineData(UserRole.Student)] [InlineData(UserRole.Admin)] [InlineData(null)]
    public void Non_teachers_cannot_be_assigned(UserRole? role) => Error(() => Class.EnsureTeacher(role), "TEACHER_ROLE_REQUIRED");
    [Fact] public void Teacherless_creation_is_allowed() => Create().Teachers.Should().BeEmpty();
    [Fact] public void Removed_enrollment_can_rejoin()
    {
        var e = ClassEnrollment.Create(Guid.NewGuid(), Guid.NewGuid(), ClassJoinMethod.Public, Now);
        e.Remove(); e.Rejoin(ClassJoinMethod.Code, Now.AddMinutes(1));
        e.Status.Should().Be(ClassEnrollmentStatus.Active); e.JoinMethod.Should().Be(ClassJoinMethod.Code);
    }
    [Fact] public async Task Join_handler_uses_injected_clock_for_expiry()
    {
        var db = new Mock<IClassRepository>(); var student = Guid.NewGuid(); var c = Create(ClassAccessType.CodeRequired, expires: Now.AddHours(1));
        db.Setup(x => x.TransactionAsync(It.IsAny<Func<CancellationToken, Task<CodePath.Shared.Kernel.Common.Result<Guid>>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task<CodePath.Shared.Kernel.Common.Result<Guid>>> action, CancellationToken ct) => action(ct));
        db.Setup(x => x.LockAsync(null, c.JoinCode, It.IsAny<CancellationToken>())).ReturnsAsync(c);
        var handler = new ClassCommandHandlers(db.Object, new FixedClock(Now.AddHours(1)), new JoinCodeGenerator());
        var act = () => handler.Handle(new JoinClassCommand(student, null, c.JoinCode), default);
        (await act.Should().ThrowAsync<DomainRuleViolationException>()).Which.ErrorCode.Should().Be("JOIN_CODE_EXPIRED");
        db.Verify(x => x.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
    private sealed class FixedClock(DateTime now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class SequenceGenerator : JoinCodeGenerator
    { private int count; protected override string Candidate() => count++ == 0 ? "ABCDEFGH" : "JKLMNPQR"; }
}
