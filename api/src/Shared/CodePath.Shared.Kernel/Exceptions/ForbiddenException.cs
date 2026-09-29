namespace CodePath.Shared.Kernel.Exceptions;

public class ForbiddenException : AppException
{
    public ForbiddenException(string message, string errorCode = "FORBIDDEN") : base(message, errorCode)
    {
    }

    public ForbiddenException(string errorCode, string message, bool _) : base(message, errorCode)
    {
    }
}
