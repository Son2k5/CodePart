namespace CodePath.Shared.Kernel.Exceptions;

public sealed class UnauthorizedAppException(string message) : AppException(message);
