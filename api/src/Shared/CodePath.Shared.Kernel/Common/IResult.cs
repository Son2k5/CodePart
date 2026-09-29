namespace CodePath.Shared.Kernel.Common;

public interface IResult
{
    bool IsSuccess { get; }
    string? Error { get; }
    string? ErrorCode { get; }
}
