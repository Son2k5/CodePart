namespace CodePath.Shared.Kernel.Exceptions;

public abstract class AppException : Exception
{
    public string ErrorCode { get; }

    protected AppException(string message, string errorCode = "BAD_REQUEST") : base(message)
    {
        ErrorCode = errorCode;
    }
}
