namespace CodePath.Shared.Kernel.Common;

public interface IValidationResult
{
    IReadOnlyList<ValidationError> ValidationErrors { get; }
}
