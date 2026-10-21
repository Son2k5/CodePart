namespace CodePath.Shared.Kernel.Common;

public static class ErrorCodes
{
    public const string BadRequest = "BAD_REQUEST";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string Conflict = "CONFLICT";
    public const string ValidationError = "VALIDATION_ERROR";
    public const string AccountUnverified = "UNVERIFIED_EMAIL";
    public const string AccountPending = "ACCOUNT_PENDING";
    public const string AccountRejected = "ACCOUNT_REJECTED";
    public const string AccountDisabled = "ACCOUNT_DISABLED";
    public const string InfraError = "INFRASTRUCTURE_ERROR";
    public const string TooManyRequests = "TOO_MANY_REQUESTS";
    public const string RateLimitExceeded = "RATE_LIMIT_EXCEEDED";
    public const string InternalServerError = "INTERNAL_SERVER_ERROR";
    public const string ServiceUnavailable = "SERVICE_UNAVAILABLE";

    // Auth
    public const string AuthInvalidCredentials = "AUTH_INVALID_CREDENTIALS";
    public const string AuthSessionExpired = "AUTH_SESSION_EXPIRED";
    public const string AuthOtpInvalid = "AUTH_OTP_INVALID";
    public const string SessionIdleTimeout = "SESSION_IDLE_TIMEOUT";
    public const string SessionExpired = "SESSION_EXPIRED";

    // Classes & Learning
    public const string TopicLocked = "TOPIC_LOCKED";
    public const string NotEnrolled = "NOT_ENROLLED";
    public const string ClassNotFound = "CLASS_NOT_FOUND";
    public const string EnrollmentNotFound = "ENROLLMENT_NOT_FOUND";
    public const string ClassFull = "CLASS_FULL";
    public const string AlreadyEnrolled = "ALREADY_ENROLLED";
    public const string ClassHasStudents = "CLASS_HAS_STUDENTS";
    public const string ClassConcurrencyConflict = "CLASS_CONCURRENCY_CONFLICT";
    public const string InvalidJoinCode = "INVALID_JOIN_CODE";
    public const string ContentInvalid = "CONTENT_INVALID";
    public const string FileTooLarge = "FILE_TOO_LARGE";
    public const string AssetNotReady = "ASSET_NOT_READY";
    public const string FeatureNotEnabled = "FEATURE_NOT_ENABLED";

    // Judge
    public const string SubmissionInProgress = "SUBMISSION_IN_PROGRESS";
    public const string CompileError = "COMPILE_ERROR";
    public const string RuntimeError = "RUNTIME_ERROR";
    public const string TimeLimitExceeded = "TIME_LIMIT_EXCEEDED";
    public const string MemoryLimitExceeded = "MEMORY_LIMIT_EXCEEDED";

    // AI & RAG
    public const string AiUnavailable = "AI_UNAVAILABLE";
    public const string RagUnavailable = "RAG_UNAVAILABLE";
    public const string RagDisabled = "RAG_DISABLED";
    public const string ContextChanged = "CONTEXT_CHANGED";
}

