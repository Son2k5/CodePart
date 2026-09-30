namespace CodePath.Domain.Exercises.Enums;

public enum SubmissionStatus
{
    Pending = 0,
    Accepted = 1,
    WrongAnswer = 2,
    CompilationError = 3,
    RuntimeError = 4,
    TimeLimitExceeded = 5,
    SystemError = 6
}
