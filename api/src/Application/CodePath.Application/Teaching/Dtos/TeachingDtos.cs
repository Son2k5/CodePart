namespace CodePath.Application.Teaching.Dtos;

public sealed record CourseDto(Guid Id, string SubjectName, string SectionCode, string Language, string Semester,
    string? JoinCode, int Capacity, int EnrolledCount, string Status, int ExerciseCount);
public sealed record StudentDto(Guid Id, string FullName, string Email, string? StudentId, string Status);
public sealed record ClassDto(Guid Id, string Name, string AcademicYear, string FacultyName, int StudentCount);
public sealed record CourseExerciseDto(Guid Id, string Slug, string Title, string Difficulty, DateTime CreatedAt);
public sealed record SubmissionDto(Guid Id, Guid ExerciseId, string ExerciseTitle, Guid StudentId, string StudentName,
    string Language, string Status, int PassedTests, int TotalTests, DateTime CreatedAt,
    decimal? TeacherScore, string? TeacherFeedback, DateTime? GradedAt);
public sealed record SubmissionPage(IReadOnlyList<SubmissionDto> Items, int Page, int PageSize, int TotalCount, int TotalPages);
public sealed record TestResultDto(int Order, string Status, string Input, string ExpectedOutput,
    string? ActualOutput, string? ErrorMessage, int? RuntimeMs, int? MemoryKb);
public sealed record SubmissionDetailDto(SubmissionDto Summary, string SourceCode, IReadOnlyList<TestResultDto> TestResults);
public sealed record CreateCourseInput(string SubjectName, string SectionCode, string Language, string Semester, int Capacity = 60);
public sealed record TestInput(string Input, string ExpectedOutput, bool IsSample, string? Explanation);
public sealed record CreateExerciseInput(string Title, string Difficulty, string Description, string Constraints,
    string StarterCode, int TimeLimitMs, int MemoryLimitKb, TestInput[] TestCases);
