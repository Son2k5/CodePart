using System.Data;
using CodePath.Application.Teaching.Abstractions;
using CodePath.Application.Teaching.Dtos;
using CodePath.Application.Exercises.Abstractions;
using CodePath.Domain.Exercises.Entities;
using CodePath.Domain.Users.Entities;
using CodePath.Domain.Users.Enums;
using CodePath.Shared.Kernel.Enums;
using Microsoft.EntityFrameworkCore;

namespace CodePath.Infrastructure.Persistence;

public partial class AppDbContext : ITeachingDbContext
{
    Task<T> ITeachingDbContext.TransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct) =>
        ExecuteWithRetryingTransactionAsync(operation, IsolationLevel.Serializable, ct);
    async Task<IReadOnlyList<CourseDto>> ITeachingDbContext.CoursesAsync(Guid userId, bool teacher, CancellationToken ct) =>
        await Courses.AsNoTracking().Where(c => teacher ? c.TeacherId == userId :
            c.Status != CourseStatus.Draft && c.Enrollments.Any(e => e.StudentId == userId && e.Status == EnrollmentStatus.Active))
        .OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Id)
        .Select(c => new CourseDto(c.Id, c.SubjectName, c.SectionCode, c.Language, c.Semester,
            teacher ? c.JoinCode : null, c.MaxCapacity, c.Enrollments.Count(e => e.Status == EnrollmentStatus.Active),
            c.Status.ToString(), Exercises.Count(e => e.CourseId == c.Id && e.IsPublished))).ToListAsync(ct);
    Task<Course?> ITeachingDbContext.OwnedCourseAsync(Guid id, Guid teacher, CancellationToken ct) =>
        Courses.SingleOrDefaultAsync(c => c.Id == id && c.TeacherId == teacher, ct);
    Task<Course?> ITeachingDbContext.CourseByCodeAsync(string code, CancellationToken ct) =>
        Courses.SingleOrDefaultAsync(c => c.JoinCode == code, ct);
    Task<bool> ITeachingDbContext.EnrolledAsync(Guid course, Guid student, CancellationToken ct) =>
        Enrollments.AnyAsync(e => e.CourseId == course && e.StudentId == student && e.Status == EnrollmentStatus.Active
            && e.Course.Status != CourseStatus.Draft, ct);
    Task<int> ITeachingDbContext.EnrollmentCountAsync(Guid course, CancellationToken ct) =>
        Enrollments.CountAsync(e => e.CourseId == course && e.Status == EnrollmentStatus.Active, ct);
    Task<Enrollment?> ITeachingDbContext.EnrollmentAsync(Guid course, Guid student, CancellationToken ct) =>
        Enrollments.SingleOrDefaultAsync(e => e.CourseId == course && e.StudentId == student, ct);
    Task<User?> ITeachingDbContext.StudentByEmailAsync(string email, CancellationToken ct) =>
        Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == email && u.Role == UserRole.Student, ct);
    async Task<IReadOnlyList<StudentDto>> ITeachingDbContext.StudentsAsync(Guid course, CancellationToken ct) =>
        await Enrollments.AsNoTracking().Where(e => e.CourseId == course && e.Status != EnrollmentStatus.Dropped)
            .OrderBy(e => e.Student.FullName).ThenBy(e => e.StudentId)
            .Select(e => new StudentDto(e.StudentId, e.Student.FullName, e.Student.Email, e.Student.StudentId, e.Status.ToString())).ToListAsync(ct);
    async Task<IReadOnlyList<CourseExerciseDto>> ITeachingDbContext.ExercisesAsync(Guid course, CancellationToken ct) =>
        await Exercises.AsNoTracking().Where(e => e.CourseId == course && e.IsPublished).OrderByDescending(e => e.CreatedAt)
            .Select(e => new CourseExerciseDto(e.Id, e.Slug, e.Title, e.Difficulty, e.CreatedAt)).ToListAsync(ct);
    async Task<IReadOnlyList<ClassDto>> ITeachingDbContext.ClassesAsync(Guid teacher, CancellationToken ct) =>
        await Classes.AsNoTracking().Where(c => c.Teachers.Any(t => t.TeacherId == teacher)).OrderBy(c => c.Name)
            .Select(c => new ClassDto(c.Id, c.Name, c.AcademicYear, c.FacultyId == null ? "" : c.Faculty.Name,
                c.ClassEnrollments.Count(e => e.Status == ClassEnrollmentStatus.Active))).ToListAsync(ct);
    async Task<IReadOnlyList<StudentDto>?> ITeachingDbContext.ClassStudentsAsync(Guid id, Guid teacher, CancellationToken ct)
    {
        if (!await Classes.AnyAsync(c => c.Id == id && c.Teachers.Any(t => t.TeacherId == teacher), ct)) return null;
        return await Users.AsNoTracking().Where(u => ClassEnrollments.Any(e => e.ClassId == id && e.StudentId == u.Id && e.Status == ClassEnrollmentStatus.Active)).OrderBy(u => u.FullName)
            .Select(u => new StudentDto(u.Id, u.FullName, u.Email, u.StudentId, u.Status.ToString())).ToListAsync(ct);
    }
    private IQueryable<SubmissionDto> SubmissionProjection(IQueryable<Submission> query) =>
        query.Select(s => new SubmissionDto(s.Id, s.ExerciseId, s.Exercise.Title, s.StudentId,
            Users.Where(u => u.Id == s.StudentId).Select(u => u.FullName).First(), s.Language,
            s.Status.ToString(), s.PassedTests, s.TotalTests, s.CreatedAt, s.TeacherScore, s.TeacherFeedback, s.GradedAt));
    async Task<SubmissionPage> ITeachingDbContext.SubmissionsAsync(Guid course, int page, int size, CancellationToken ct)
    {
        var count = await Submissions.CountAsync(s => s.Exercise.CourseId == course, ct);
        var items = await SubmissionProjection(Submissions.AsNoTracking().Where(s => s.Exercise.CourseId == course)
            .OrderByDescending(s => s.CreatedAt).ThenBy(s => s.Id).Skip((page - 1) * size).Take(size)).ToListAsync(ct);
        return new(items, page, size, count, (int)Math.Ceiling(count / (double)size));
    }
    async Task<SubmissionDetailDto?> ITeachingDbContext.SubmissionDetailAsync(Guid course, Guid id, CancellationToken ct)
    {
        var summary = await SubmissionProjection(Submissions.AsNoTracking().Where(s => s.Exercise.CourseId == course && s.Id == id)).SingleOrDefaultAsync(ct);
        if (summary is null) return null;
        var code = await Submissions.Where(s => s.Id == id).Select(s => s.SourceCode).SingleAsync(ct);
        var tests = await SubmissionTestResults.AsNoTracking().Where(t => t.SubmissionId == id).OrderBy(t => t.TestCase.Order)
            .Select(t => new TestResultDto(t.TestCase.Order, t.Status.ToString(), t.TestCase.Input, t.TestCase.ExpectedOutput,
                t.ActualOutput, t.ErrorMessage, t.RuntimeMs, t.MemoryKb)).ToListAsync(ct);
        return new(summary, code, tests);
    }
    Task<Submission?> ITeachingDbContext.SubmissionAsync(Guid course, Guid id, CancellationToken ct) =>
        Submissions.SingleOrDefaultAsync(s => s.Id == id && s.Exercise.CourseId == course, ct);
    Task ITeachingDbContext.AddCourseAsync(Course c, CancellationToken ct) => Courses.AddAsync(c, ct).AsTask();
    Task ITeachingDbContext.AddEnrollmentAsync(Enrollment e, CancellationToken ct) => Enrollments.AddAsync(e, ct).AsTask();
    Task ITeachingDbContext.AddExerciseAsync(Exercise e, CancellationToken ct) => Exercises.AddAsync(e, ct).AsTask();

    Task<bool> IExerciseDbContext.CanAccessCourseExerciseAsync(Guid course, Guid? user, bool execute, CancellationToken ct) =>
        Courses.AnyAsync(c => c.Id == course && (execute ? c.Status == CourseStatus.Active : c.Status != CourseStatus.Draft)
            && user.HasValue && (c.TeacherId == user.Value || c.Enrollments.Any(e => e.StudentId == user.Value && e.Status == EnrollmentStatus.Active)), ct);
}
