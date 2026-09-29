namespace CodePath.Shared.Kernel.Common;

public sealed record ValidationError(string PropertyName, string ErrorMessage);
