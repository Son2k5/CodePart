namespace CodePath.Shared.Kernel.Exceptions;

public class ForbiddenException : AppException
{
    public string ErrorCode { get; }

    public ForbiddenException(string errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }
}
