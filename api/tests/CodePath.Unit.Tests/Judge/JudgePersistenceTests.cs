using System.Text.Json;
using CodePath.Application.Judge;
using CodePath.Domain.Judge;
using CodePath.Infrastructure.Judge;
using CodePath.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodePath.Unit.Tests.Judge;

public sealed class JudgeDatabaseFactAttribute : FactAttribute
{
    public JudgeDatabaseFactAttribute() { if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JUDGE_TEST_CONNECTION"))) Skip = "Set JUDGE_TEST_CONNECTION to a disposable MySQL 8 database."; }
}
public class JudgePersistenceTests
{
    private static ServiceProvider Provider(ICodeExecutor executor) => new ServiceCollection()
        .AddDbContext<JudgeDbContext>(o => JudgeDbContext.ConfigureMySql(o, Environment.GetEnvironmentVariable("JUDGE_TEST_CONNECTION")!))
        .AddSingleton(executor).BuildServiceProvider();
    private static Submission NewSubmission(Problem p, Guid user) => new() { UserId = user, ProblemId = p.Id, SourceCode = "test", Language = "python", Snapshot = JsonSerializer.Serialize(p), TotalCount = p.TestCases.Count,
        Status = Verdict.Running, LeaseToken = Guid.NewGuid(), LeaseUntil = DateTime.UtcNow.AddMinutes(5) };
    private static Problem Problem() => new() { Points = 80, TestCases = Enumerable.Range(1, 10).Select(i => new TestCase { Order = i, Input = "secret", ExpectedOutput = "42", IsSample = i == 1 }).ToList() };
    [JudgeDatabaseFact]
    public async Task Worker_retries_transient_failure_and_awards_only_once_across_resubmissions()
    {
        var runner = new FakeExecutor { Failures = 1 };
        await using var services = Provider(runner);
        using var scope = services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<JudgeDbContext>();
        await db.Database.MigrateAsync();
        var p = Problem(); var user = Guid.NewGuid(); var s = NewSubmission(p, user);
        db.Submissions.Add(s); await db.SaveChangesAsync();
        await JudgeWorker.Process(scope.ServiceProvider, s.Id, s.LeaseToken!.Value, CancellationToken.None);
        Assert.Equal(Verdict.Accepted, s.Status); Assert.Equal(10, s.Results.Count); Assert.Equal(11, runner.Calls); Assert.Equal(80, s.AwardedPoints);
        var second = NewSubmission(p, user); db.Submissions.Add(second); await db.SaveChangesAsync();
        var lease = second.LeaseToken!.Value;
        await JudgeWorker.Process(scope.ServiceProvider, second.Id, lease, CancellationToken.None);
        await JudgeWorker.Process(scope.ServiceProvider, second.Id, lease, CancellationToken.None);
        Assert.Equal(0, second.AwardedPoints); Assert.Equal(21, runner.Calls);
        Assert.Equal(1, await db.Awards.CountAsync(a => a.UserId == user && a.ProblemId == p.Id));
        Assert.All(second.Results.Where(r => !r.IsSample), r => { Assert.Null(r.Input); Assert.Null(r.ExpectedOutput); });
    }
    [JudgeDatabaseFact]
    public async Task Unique_constraint_rejects_duplicate_active_job_and_fencing_rejects_wrong_owner()
    {
        await using var services = Provider(new FakeExecutor());
        using var first = services.CreateScope(); using var second = services.CreateScope();
        var db = first.ServiceProvider.GetRequiredService<JudgeDbContext>(); await db.Database.MigrateAsync();
        var p = Problem(); var user = Guid.NewGuid(); var s = NewSubmission(p, user); db.Submissions.Add(s); await db.SaveChangesAsync();
        var other = second.ServiceProvider.GetRequiredService<JudgeDbContext>(); other.Submissions.Add(NewSubmission(p, user));
        await Assert.ThrowsAsync<DbUpdateException>(() => other.SaveChangesAsync());
        await JudgeWorker.Process(first.ServiceProvider, s.Id, Guid.NewGuid(), CancellationToken.None);
        Assert.Empty(s.Results);
    }
    [JudgeDatabaseFact]
    public async Task Worker_runs_every_test_after_wrong_answer_and_durably_records_failure_event()
    {
        var runner = new FakeExecutor { Output = "wrong" }; await using var services = Provider(runner);
        using var scope = services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<JudgeDbContext>(); await db.Database.MigrateAsync();
        var s = NewSubmission(Problem(), Guid.NewGuid()); db.Submissions.Add(s); await db.SaveChangesAsync();
        await JudgeWorker.Process(scope.ServiceProvider, s.Id, s.LeaseToken!.Value, CancellationToken.None);
        Assert.Equal(10, runner.Calls); Assert.Equal(10, s.Results.Count); Assert.Equal(Verdict.WrongAnswer, s.Status); Assert.Equal(0, s.Score);
        Assert.True(await db.FailureEvents.AnyAsync(e => e.SubmissionId == s.Id));
    }
    [JudgeDatabaseFact]
    public async Task Student_endpoints_hide_tests_and_reject_hidden_run_and_foreign_submission()
    {
        var runner = new FakeExecutor(); await using var services = Provider(runner);
        using var scope = services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<JudgeDbContext>(); await db.Database.MigrateAsync();
        // Public problems do not consult the existing identity DB; auth is enforced by the endpoints.
        using var accounts = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Options);
        var service = new JudgeService(db, new TestAccess(), runner, new JudgeQueue());
        var student = new Actor(Guid.NewGuid(), false, false);
        var p = Problem(); p.Status = ProblemStatus.Published;
        p.TestCases[1].Input = "HIDDEN_SENTINEL"; p.TestCases[1].ExpectedOutput = "HIDDEN_EXPECTED";
        db.Problems.Add(p); await db.SaveChangesAsync();
        var response = await service.GetProblem(p.Id, student, false, default);
        Assert.True(response.IsSuccess); Assert.DoesNotContain("HIDDEN", JsonSerializer.Serialize(response.Value));
        var forbidden = await service.Run(p.Id, new("python", "code", [p.TestCases[1].Id]), student, default);
        Assert.False(forbidden.IsSuccess); Assert.Equal(0, runner.Calls);
        var submission = NewSubmission(p, Guid.NewGuid()); db.Submissions.Add(submission); await db.SaveChangesAsync();
        Assert.Equal("NOT_FOUND", (await service.GetSubmission(submission.Id, student, false, default)).ErrorCode);
        Assert.Equal("NOT_FOUND", (await service.GetProblem(p.Id, new(Guid.NewGuid(), false, true), true, default)).ErrorCode);
    }
    [JudgeDatabaseFact]
    public async Task MySql_management_transactions_save_replace_and_publish_tests()
    {
        await using var services = Provider(new FakeExecutor());
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JudgeDbContext>(); await db.Database.MigrateAsync();
        using var accounts = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Options);
        var service = new JudgeService(db, new TestAccess(), new FakeExecutor(), new JudgeQueue());
        var admin = new Actor(Guid.NewGuid(), true, false);
        var created = await service.SaveProblem(null, new("MySQL management", "Sum the input", "Easy", 1000, 64, 100, TestAccess.CourseId,
            ComparisonMode.Lines, [new("python", "print(42)")]), admin, default);
        Assert.True(created.IsSuccess, created.Error);
        var id = JsonSerializer.SerializeToElement(created.Value).GetProperty("Id").GetGuid();
        var tests = Enumerable.Range(1, 10).Select(i => new TestWrite("", "42", i == 1, i, 1)).ToList();
        Assert.True((await service.SaveTests(id, tests, admin, default)).IsSuccess);
        Assert.True((await service.ChangeProblem(id, "publish", admin, default)).IsSuccess);
        Assert.Equal("CONFLICT", (await service.SaveTests(id, tests, admin, default)).ErrorCode);
        Assert.True((await service.ChangeProblem(id, "unpublish", admin, default)).IsSuccess);
        tests[0] = tests[0] with { ExpectedOutput = "43" };
        Assert.True((await service.SaveTests(id, tests, admin, default)).IsSuccess);
        db.ChangeTracker.Clear();
        var persisted = await db.Problems.Include(p => p.TestCases).SingleAsync(p => p.Id == id);
        Assert.Equal(10, persisted.TestCases.Count);
        Assert.Equal("43", persisted.TestCases.Single(t => t.Order == 1).ExpectedOutput);
    }
    [JudgeDatabaseFact]
    public async Task Class_scope_hides_other_classes_and_drafts_and_rejects_execution_without_membership()
    {
        var runner = new FakeExecutor(); await using var services = Provider(runner);
        using var scope = services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<JudgeDbContext>(); await db.Database.MigrateAsync();
        var access = new ClassAccess();
        var service = new JudgeService(db, access, runner, new JudgeQueue());
        var enrolled = new Actor(access.Student, false, false);
        var other = new Actor(Guid.NewGuid(), false, false);
        var teacher = new Actor(access.Teacher, false, true);
        var p = Problem(); p.ClassId = access.ClassId; p.Status = ProblemStatus.Published;
        // The assigned teacher can manage an admin-created problem in their class.
        p.CreatedBy = Guid.NewGuid();
        var draft = Problem(); draft.ClassId = access.ClassId;
        db.Problems.AddRange(p, draft); await db.SaveChangesAsync();
        Assert.True((await service.GetProblem(p.Id, enrolled, false, default)).IsSuccess);
        Assert.True((await service.GetProblem(p.Id, teacher, true, default)).IsSuccess);
        Assert.Equal("NOT_FOUND", (await service.GetProblem(draft.Id, enrolled, false, default)).ErrorCode);
        Assert.Equal("NOT_FOUND", (await service.GetProblem(p.Id, other, false, default)).ErrorCode);
        Assert.Equal("NOT_FOUND", (await service.Run(p.Id, new("python", "code"), other, default)).ErrorCode);
        Assert.Equal("NOT_FOUND", (await service.Submit(p.Id, new("python", "code"), other, default)).ErrorCode);
        Assert.Equal(0, runner.Calls);
        var list = await service.ListProblems(enrolled, false, 1, default, classId: access.ClassId);
        Assert.True(list.IsSuccess);
        Assert.Equal(1, JsonSerializer.SerializeToElement(list.Value).GetProperty("Total").GetInt32());
        var submitted = await service.Submit(p.Id, new("python", "code"), enrolled, default);
        Assert.True(submitted.IsSuccess);
        Assert.Equal(access.ClassId, (await db.Submissions.SingleAsync(s => s.ProblemId == p.Id)).ClassId);
        access.Enrolled = false;
        Assert.Equal("NOT_FOUND", (await service.GetProblem(p.Id, enrolled, false, default)).ErrorCode);
    }
    [JudgeDatabaseFact]
    public async Task Actual_class_access_queries_follow_teacher_assignment_enrollment_and_class_status()
    {
        var connection = new MySqlConnector.MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("JUDGE_TEST_CONNECTION")!)
        { GuidFormat = MySqlConnector.MySqlGuidFormat.Binary16, DateTimeKind = MySqlConnector.MySqlDateTimeKind.Utc }.ConnectionString;
        using var accounts = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(connection, new MySqlServerVersion(new Version(8, 4, 0))).Options);
        await accounts.Database.MigrateAsync();
        var now = DateTime.UtcNow; var suffix = Guid.NewGuid().ToString("N");
        var teacher = CodePath.Domain.Users.Entities.User.CreateTeacherByAdmin("Teacher", $"teacher-{suffix}@example.com", "test-hash", null, null, null, null, CodePath.Shared.Kernel.Enums.UserStatus.Active, now);
        var student = CodePath.Domain.Users.Entities.User.CreateStudent("Student", $"student-{suffix}@example.com", "test-hash", suffix, now);
        var c = CodePath.Domain.Users.Entities.Class.CreateManaged("Judge class " + suffix, teacher.Id, now);
        c.SetStatus(CodePath.Domain.Users.Entities.ClassStatus.Active, now);
        var assignment = CodePath.Domain.Users.Entities.ClassTeacher.Create(c.Id, teacher.Id, teacher.Id, now);
        var enrollment = CodePath.Domain.Users.Entities.ClassEnrollment.Create(c.Id, student.Id, CodePath.Domain.Users.Entities.ClassJoinMethod.AdminAdded, now);
        c.Teachers.Add(assignment); c.ClassEnrollments.Add(enrollment);
        accounts.Users.AddRange(teacher, student); accounts.Classes.Add(c); await accounts.SaveChangesAsync();
        var access = new JudgeAccess(accounts);
        Assert.True(await access.CanAccess(null, c.Id, new(teacher.Id, false, true), true, default));
        Assert.False(await access.CanAccess(null, c.Id, new(Guid.NewGuid(), false, true), true, default));
        Assert.True(await access.CanAccess(null, c.Id, new(student.Id, false, false), false, default));
        Assert.False(await access.CanAccess(null, Guid.NewGuid(), new(Guid.NewGuid(), true, false), true, default));
        accounts.ClassTeachers.Remove(assignment); await accounts.SaveChangesAsync();
        Assert.False(await access.CanAccess(null, c.Id, new(teacher.Id, false, true), true, default));
        enrollment.Remove(); await accounts.SaveChangesAsync();
        Assert.False(await access.CanAccess(null, c.Id, new(student.Id, false, false), false, default));
        enrollment.Rejoin(CodePath.Domain.Users.Entities.ClassJoinMethod.AdminAdded, now);
        c.SetStatus(CodePath.Domain.Users.Entities.ClassStatus.Archived, now); await accounts.SaveChangesAsync();
        Assert.False(await access.CanAccess(null, c.Id, new(student.Id, false, false), false, default));
    }
    private sealed class ClassAccess : IJudgeAccess
    {
        public Guid Student = Guid.NewGuid(), Teacher = Guid.NewGuid(), ClassId = Guid.NewGuid();
        public bool Enrolled = true;
        private bool Allowed(Actor a, bool manage) => manage ? a.IsAdmin || a.Id == Teacher && a.IsTeacher : Enrolled && a.Id == Student;
        public Task<IReadOnlyList<JudgeScope>> Scopes(Actor a, bool manage, CancellationToken ct) => Task.FromResult<IReadOnlyList<JudgeScope>>(Allowed(a, manage) ? [new(ClassId, "Class A", "class")] : []);
        public Task<bool> CanAccess(Guid? course, Guid? @class, Actor a, bool manage, CancellationToken ct) => Task.FromResult(course == null && @class == ClassId && Allowed(a, manage));
    }
    private sealed class TestAccess : IJudgeAccess
    {
        public static readonly Guid CourseId = Guid.NewGuid();
        public Task<IReadOnlyList<JudgeScope>> Scopes(Actor actor, bool manage, CancellationToken ct) => Task.FromResult<IReadOnlyList<JudgeScope>>([new(CourseId, "Test course", "course")]);
        public Task<bool> CanAccess(Guid? courseId, Guid? classId, Actor actor, bool manage, CancellationToken ct) => Task.FromResult(classId == null && (courseId == null || courseId == CourseId));
    }
    private sealed class FakeExecutor : ICodeExecutor
    {
        public int Calls; public int Failures; public string Output = "42";
        public bool Supports(string language) => true;
        public Task<ExecutionResult> Execute(string language, string sourceCode, string stdin, Limits limits, CancellationToken ct)
        { Calls++; return Task.FromResult(new ExecutionResult(Calls <= Failures ? Verdict.InternalError : Verdict.Accepted, Output, null, 10, 1024)); }
    }
}
