namespace CodePath.Shared.Kernel.Exceptions;

public sealed class NotFoundException(string message) : AppException(message);
