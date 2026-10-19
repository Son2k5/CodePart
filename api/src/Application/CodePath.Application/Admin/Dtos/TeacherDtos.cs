using CodePath.Shared.Kernel.Enums;

namespace CodePath.Application.Admin.Dtos;

public sealed record TeacherFilter(int Page = 1, int PageSize = 20, string? Search = null,
    Guid? FacultyId = null, UserStatus? Status = null, string SortBy = "created_desc");
public sealed record OffsetPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);
public sealed record TeacherDto(Guid Id, string? TeacherId, string FullName, string Email,
    string? Phone, string? Bio, Guid? FacultyId, string? FacultyName, string Status,
    int ClassesCount, int CoursesCount, DateTime? EmailVerifiedAt, DateTime CreatedAt, DateTime? LastLoginAt);
public sealed record TeacherStatsDto(int TotalTeachers, int ActiveTeachers, int PendingApprovals,
    int DisabledTeachers, int RejectedTeachers);
public sealed record ManagedClassDto(Guid Id, string Name, string AcademicYear, int StudentCount);
public sealed record TeachingCourseDto(Guid Id, string SubjectName, string SectionCode, string Semester,
    int Capacity, int EnrolledCount);
public sealed record TeacherDetailDto(TeacherDto Profile, IReadOnlyList<ManagedClassDto> ManagedClasses,
    IReadOnlyList<TeachingCourseDto> TeachingCourses);
public sealed record FacultyDto(Guid Id, string Name, string Code, int TeacherCount);
public sealed record TeacherProfileInput(string FullName, string? TeacherId, Guid? FacultyId, string? Phone, string? Bio);
public sealed record CreateTeacherInput(string FullName, string Email, string? TeacherId, Guid? FacultyId,
    string? Phone, string? Bio, string TemporaryPassword,
    [property: System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<UserStatus>))]
    UserStatus Status = UserStatus.Active);
