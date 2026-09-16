namespace CodePath.Shared.Kernel.Exceptions;

public sealed class ConflictException(string message) : AppException(message);
