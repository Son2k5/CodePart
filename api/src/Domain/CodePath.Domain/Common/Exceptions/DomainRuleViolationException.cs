using CodePath.Shared.Kernel.Common;

namespace CodePath.Domain.Common.Exceptions;

public sealed class DomainRuleViolationException : DomainException
{
    public DomainRuleViolationException(string message, string errorCode = ErrorCodes.BadRequest) 
        : base(message, errorCode)
    {
    }
}
