using CodePath.Application.Teaching.Dtos;
using CodePath.Domain.Users.Entities;
using CodePath.Domain.Exercises.Entities;

namespace CodePath.Application.Teaching.Abstractions;

public interface ITeachingDbContext
{
    Task<T> TransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct);
    Task<IReadOnlyList<CourseDto>> CoursesAsync(Guid userId, bool teacher, CancellationToken ct);
    Task<Course?> OwnedCourseAsync(Guid id, Guid teacherId, CancellationToken ct);
    Task<Course?> CourseByCodeAsync(string code, CancellationToken ct);
    Task<bool> EnrolledAsync(Guid courseId, Guid studentId, CancellationToken ct);
    Task<int> EnrollmentCountAsync(Guid courseId, CancellationToken ct);
    Task<Enrollment?> EnrollmentAsync(Guid courseId, Guid studentId, CancellationToken ct);
    Task<User?> StudentByEmailAsync(string email, CancellationToken ct);
    Task<IReadOnlyList<StudentDto>> StudentsAsync(Guid courseId, CancellationToken ct);
    Task<IReadOnlyList<CourseExerciseDto>> ExercisesAsync(Guid courseId, CancellationToken ct);
    Task<IReadOnlyList<ClassDto>> ClassesAsync(Guid teacherId, CancellationToken ct);
    Task<IReadOnlyList<StudentDto>?> ClassStudentsAsync(Guid classId, Guid teacherId, CancellationToken ct);
    Task<SubmissionPage> SubmissionsAsync(Guid courseId, int page, int pageSize, CancellationToken ct);
    Task<SubmissionDetailDto?> SubmissionDetailAsync(Guid courseId, Guid submissionId, CancellationToken ct);
    Task<Submission?> SubmissionAsync(Guid courseId, Guid submissionId, CancellationToken ct);
    Task AddCourseAsync(Course course, CancellationToken ct);
    Task AddEnrollmentAsync(Enrollment enrollment, CancellationToken ct);
    Task AddExerciseAsync(Exercise exercise, CancellationToken ct);
    Task<int> SaveChangesAsync(CancellationToken ct);
}
