using CodePath.Shared.Kernel.Enums;

namespace CodePath.Application.Admin.Dtos;

public sealed record StudentFilter(int Page = 1, int PageSize = 20, string? Search = null,
    Guid? FacultyId = null, UserStatus? Status = null, string SortBy = "created_desc");

public sealed record StudentDto(Guid Id, string? StudentId, string FullName, string Email,
    string? Phone, string? Bio, Guid? FacultyId, string? FacultyName, string Status,
    int ClassesCount, int CoursesCount, DateTime? EmailVerifiedAt, DateTime CreatedAt, DateTime? LastLoginAt);

public sealed record StudentStatsDto(int TotalStudents, int ActiveStudents, int PendingApprovals,
    int DisabledStudents, int RejectedStudents);

public sealed record EnrolledClassDto(Guid Id, string Name, string AcademicYear);

public sealed record EnrolledCourseDto(Guid Id, string SubjectName, string SectionCode, string Semester, string? TeacherName);

public sealed record StudentDetailDto(StudentDto Profile, IReadOnlyList<EnrolledClassDto> EnrolledClasses,
    IReadOnlyList<EnrolledCourseDto> EnrolledCourses);

public sealed record StudentProfileInput(string FullName, string StudentId, Guid? FacultyId, string? Phone, string? Bio);

public sealed record CreateStudentInput(string FullName, string Email, string StudentId, Guid? FacultyId,
    string? Phone, string? Bio, string TemporaryPassword,
    [property: System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<UserStatus>))]
    UserStatus Status = UserStatus.Active);
