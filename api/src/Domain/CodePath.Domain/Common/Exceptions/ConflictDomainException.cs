using CodePath.Shared.Kernel.Common;

namespace CodePath.Domain.Common.Exceptions;

public sealed class ConflictDomainException : DomainException
{
    public ConflictDomainException(string message) 
        : base(message, ErrorCodes.Conflict)
    {
    }
}
