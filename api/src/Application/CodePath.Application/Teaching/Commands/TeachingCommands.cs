using System.Security.Cryptography;
using CodePath.Application.Teaching.Abstractions;
using CodePath.Application.Teaching.Dtos;
using CodePath.Domain.Users.Entities;
using CodePath.Domain.Users.Enums;
using CodePath.Domain.Exercises.Entities;
using CodePath.Shared.Kernel.Common;
using CodePath.Shared.Kernel.Enums;
using CodePath.Shared.Kernel.Exceptions;
using FluentValidation;
using MediatR;

namespace CodePath.Application.Teaching.Commands;

public sealed record CreateCourseCommand(Guid TeacherId, CreateCourseInput Data) : IRequest<Result<Guid>>;
public sealed record SetCourseStatusCommand(Guid TeacherId, Guid CourseId, string Status) : IRequest<Result<Guid>>;
public sealed record AddCourseStudentCommand(Guid TeacherId, Guid CourseId, string Email) : IRequest<Result<Guid>>;
public sealed record RemoveCourseStudentCommand(Guid TeacherId, Guid CourseId, Guid StudentId) : IRequest<Result<Guid>>;
public sealed record JoinCourseCommand(Guid StudentId, string JoinCode) : IRequest<Result<Guid>>;
public sealed record CreateCourseExerciseCommand(Guid TeacherId, Guid CourseId, CreateExerciseInput Data) : IRequest<Result<Guid>>;
public sealed record GradeSubmissionCommand(Guid TeacherId, Guid CourseId, Guid SubmissionId, decimal Score, string? Feedback) : IRequest<Result<Guid>>;

public sealed class CreateCourseCommandValidator : AbstractValidator<CreateCourseCommand>
{
    public CreateCourseCommandValidator()
    {
        RuleFor(x => x.TeacherId).NotEmpty();
        RuleFor(x => x.Data).NotNull();
        When(x => x.Data != null, () => {
            RuleFor(x => x.Data.SubjectName).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Data.SectionCode).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Data.Semester).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Data.Language).Must(x => new[] { "python", "javascript", "java", "csharp", "cpp" }.Contains(x));
            RuleFor(x => x.Data.Capacity).InclusiveBetween(1, 1000);
        });
    }
}
public sealed class SetCourseStatusCommandValidator : AbstractValidator<SetCourseStatusCommand>
{
    public SetCourseStatusCommandValidator() => RuleFor(x => x.Status).Must(x => x is "Active" or "Closed");
}
public sealed class AddCourseStudentCommandValidator : AbstractValidator<AddCourseStudentCommand>
{
    public AddCourseStudentCommandValidator() => RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
}
public sealed class JoinCourseCommandValidator : AbstractValidator<JoinCourseCommand>
{
    public JoinCourseCommandValidator() => RuleFor(x => x.JoinCode).NotEmpty().MaximumLength(20);
}
public sealed class CreateCourseExerciseCommandValidator : AbstractValidator<CreateCourseExerciseCommand>
{
    public CreateCourseExerciseCommandValidator()
    {
        RuleFor(x => x.Data).NotNull();
        When(x => x.Data != null, () => {
            RuleFor(x => x.Data.Title).NotEmpty().MaximumLength(250);
            RuleFor(x => x.Data.Difficulty).Must(x => x is "Easy" or "Medium" or "Hard");
            RuleFor(x => x.Data.Description).NotEmpty().MaximumLength(10000);
            RuleFor(x => x.Data.Constraints).NotNull().MaximumLength(10000);
            RuleFor(x => x.Data.StarterCode).NotNull().MaximumLength(10000);
            RuleFor(x => x.Data.TimeLimitMs).InclusiveBetween(100, 15000);
            RuleFor(x => x.Data.MemoryLimitKb).InclusiveBetween(16384, 1048576);
            RuleFor(x => x.Data.TestCases).NotEmpty().Must(x => x != null && x.Length <= 50 && x.Any(t => t != null && t.IsSample))
                .WithMessage("Provide 1–50 tests, including at least one sample.");
            RuleForEach(x => x.Data.TestCases).NotNull().ChildRules(test => {
                test.RuleFor(x => x.Input).NotEmpty().MaximumLength(10000);
                test.RuleFor(x => x.ExpectedOutput).NotEmpty().MaximumLength(10000);
                test.RuleFor(x => x.Explanation).MaximumLength(2000);
            });
        });
    }
}
public sealed class GradeSubmissionCommandValidator : AbstractValidator<GradeSubmissionCommand>
{
    public GradeSubmissionCommandValidator()
    {
        RuleFor(x => x.Score).InclusiveBetween(0, 10).Must(x => decimal.Round(x, 2) == x);
        RuleFor(x => x.Feedback).MaximumLength(2000);
    }
}

internal sealed class TeachingCommandHandlers(ITeachingDbContext db, TimeProvider clock) :
    IRequestHandler<CreateCourseCommand, Result<Guid>>,
    IRequestHandler<SetCourseStatusCommand, Result<Guid>>,
    IRequestHandler<AddCourseStudentCommand, Result<Guid>>,
    IRequestHandler<RemoveCourseStudentCommand, Result<Guid>>,
    IRequestHandler<JoinCourseCommand, Result<Guid>>,
    IRequestHandler<CreateCourseExerciseCommand, Result<Guid>>,
    IRequestHandler<GradeSubmissionCommand, Result<Guid>>
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private async Task<Course> Own(Guid id, Guid teacher, CancellationToken ct) =>
        await db.OwnedCourseAsync(id, teacher, ct) ?? throw new NotFoundException("Course not found.");

    public async Task<Result<Guid>> Handle(CreateCourseCommand r, CancellationToken ct)
    {
        var course = Course.Create(r.Data.SubjectName, r.Data.SectionCode, r.Data.Language, r.Data.Semester,
            Convert.ToHexString(RandomNumberGenerator.GetBytes(8)), r.TeacherId, Now, r.Data.Capacity);
        await db.AddCourseAsync(course, ct);
        await db.SaveChangesAsync(ct);
        return Result<Guid>.Success(course.Id);
    }
    public Task<Result<Guid>> Handle(SetCourseStatusCommand r, CancellationToken ct) => db.TransactionAsync(async token => {
        var course = await Own(r.CourseId, r.TeacherId, token);
        if (r.Status == "Active" && course.Status != CourseStatus.Active) course.Activate(Now);
        if (r.Status == "Closed") course.Close(Now);
        await db.SaveChangesAsync(token);
        return Result<Guid>.Success(course.Id);
    }, ct);
    public Task<Result<Guid>> Handle(AddCourseStudentCommand r, CancellationToken ct) => db.TransactionAsync(async token => {
        var course = await Own(r.CourseId, r.TeacherId, token);
        var student = await db.StudentByEmailAsync(EmailNormalizer.Normalize(r.Email), token);
        if (student is null || student.Status != UserStatus.Active || student.EmailVerifiedAt is null)
            throw new NotFoundException("An active, verified student with this email was not found.");
        return await Enroll(course, student.Id, token);
    }, ct);
    public Task<Result<Guid>> Handle(JoinCourseCommand r, CancellationToken ct) => db.TransactionAsync(async token => {
        var course = await db.CourseByCodeAsync(r.JoinCode.Trim().ToUpperInvariant(), token)
            ?? throw new NotFoundException("Course not found.");
        return await Enroll(course, r.StudentId, token);
    }, ct);
    private async Task<Result<Guid>> Enroll(Course course, Guid student, CancellationToken ct)
    {
        if (course.Status != CourseStatus.Active) throw new ConflictException("Course is not open for enrollment.");
        var existing = await db.EnrollmentAsync(course.Id, student, ct);
        if (existing is not null && existing.Status != EnrollmentStatus.Dropped)
            throw new ConflictException("Student is already enrolled.");
        if (await db.EnrollmentCountAsync(course.Id, ct) >= course.MaxCapacity)
            throw new ConflictException("Course capacity has been reached.");
        if (existing is null) await db.AddEnrollmentAsync(Enrollment.Create(course.Id, student, Now), ct);
        else existing.Rejoin(Now);
        await db.SaveChangesAsync(ct);
        return Result<Guid>.Success(course.Id);
    }
    public Task<Result<Guid>> Handle(RemoveCourseStudentCommand r, CancellationToken ct) => db.TransactionAsync(async token => {
        await Own(r.CourseId, r.TeacherId, token);
        var enrollment = await db.EnrollmentAsync(r.CourseId, r.StudentId, token)
            ?? throw new NotFoundException("Enrollment not found.");
        enrollment.Drop(Now);
        await db.SaveChangesAsync(token);
        return Result<Guid>.Success(r.StudentId);
    }, ct);
    public Task<Result<Guid>> Handle(CreateCourseExerciseCommand r, CancellationToken ct) => db.TransactionAsync(async token => {
        var course = await Own(r.CourseId, r.TeacherId, token);
        if (course.Status != CourseStatus.Active) throw new ConflictException("Activate the course before assigning exercises.");
        var d = r.Data;
        var exercise = Exercise.Create("course-" + Guid.NewGuid().ToString("N"), d.Title, d.Difficulty,
            d.Description, d.Constraints, d.StarterCode, d.TimeLimitMs, d.MemoryLimitKb, Now);
        exercise.AssignToCourse(course.Id, Now);
        for (var i = 0; i < d.TestCases.Length; i++)
        {
            var test = d.TestCases[i];
            exercise.AddTestCase(test.Input, test.ExpectedOutput, test.IsSample, i + 1, test.Explanation, Now);
        }
        exercise.Publish(Now);
        await db.AddExerciseAsync(exercise, token);
        await db.SaveChangesAsync(token);
        return Result<Guid>.Success(exercise.Id);
    }, ct);
    public Task<Result<Guid>> Handle(GradeSubmissionCommand r, CancellationToken ct) => db.TransactionAsync(async token => {
        await Own(r.CourseId, r.TeacherId, token);
        var submission = await db.SubmissionAsync(r.CourseId, r.SubmissionId, token)
            ?? throw new NotFoundException("Submission not found.");
        submission.Grade(r.Score, r.Feedback, r.TeacherId, Now);
        await db.SaveChangesAsync(token);
        return Result<Guid>.Success(submission.Id);
    }, ct);
}
