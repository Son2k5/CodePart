using CodePath.Shared.Kernel.Common;

namespace CodePath.Domain.Common.Exceptions;

public sealed class DomainValidationException : DomainException
{
    public string? PropertyName { get; }

    public DomainValidationException(string message, string? propertyName = null, string errorCode = ErrorCodes.ValidationError) 
        : base(message, errorCode)
    {
        PropertyName = propertyName;
    }
}
