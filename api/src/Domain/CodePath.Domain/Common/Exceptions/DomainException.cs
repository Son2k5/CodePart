using CodePath.Shared.Kernel.Common;
using CodePath.Shared.Kernel.Exceptions;

namespace CodePath.Domain.Common.Exceptions;

public abstract class DomainException : AppException
{
    protected DomainException(string message, string errorCode = ErrorCodes.BadRequest) : base(message, errorCode)
    {
    }
}
