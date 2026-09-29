namespace CodePath.Shared.Kernel.Common;

public sealed class Result<T> : IValidationResult
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }
    public string? ErrorCode { get; }
    public IReadOnlyList<ValidationError> ValidationErrors { get; }

    private Result(bool isSuccess, T? value, string? error, string? errorCode = null, IEnumerable<ValidationError>? validationErrors = null)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
        ErrorCode = errorCode;
        ValidationErrors = validationErrors?.ToList() ?? [];
    }

    public static Result<T> Success(T value) => new(true, value, null, null);

    public static Result<T> Failure(string error, string? errorCode = ErrorCodes.BadRequest)
        => new(false, default, error, errorCode ?? ErrorCodes.BadRequest);

    public static Result<T> ValidationFailure(IEnumerable<ValidationError> validationErrors)
    {
        var errorsList = validationErrors.ToList();
        var message = errorsList.Count > 0
            ? string.Join("; ", errorsList.Select(e => e.ErrorMessage))
            : "Dữ liệu không hợp lệ.";
        return new(false, default, message, ErrorCodes.ValidationError, errorsList);
    }
}
