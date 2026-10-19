using System.Text.Json;
namespace CodePath.Application.Ai;

public sealed record AssistInput(Guid ProblemId, string Language, string SourceCode, string Type = "Hint", int Level = 1, string? Question = null);
public sealed record AiResponse(string Stage, double StageConfidence, string ErrorCategory, string Message,
    string[] Steps, int[] SuspectedLines, string? GuidingQuestion, int Level, string GuardStatus,
    string ModelVersion, string PromptVersion, int TokensIn, int TokensOut,
    CodePath.Application.Learning.RagReference[]? References = null);
public interface IAiAssistantClient
{
    Task<AiResponse> Assist(JsonElement context, CancellationToken ct);
}
public sealed record AssistResult(Guid InteractionId, AiResponse Hint, int Remaining, int UnlockedLevel, string? Code = null);
public interface IAiService
{
    Task<AssistResult> Assist(Guid studentId, AssistInput input, CancellationToken ct);
    Task<bool> Feedback(Guid studentId, Guid interactionId, bool helpful, CancellationToken ct);
}
public sealed class AiPolicyException(string code, int status = 400) : Exception(code)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}
public static class AiPolicy
{
    public const int DailyQuota = 30;
    public static bool CanUnlock(int requested, int previous, int failuresSinceHint, double minutesSinceHint)
        => requested is >= 1 and <= 3 && (requested == 1 || requested <= previous ||
            requested == previous + 1 && (failuresSinceHint >= 2 || minutesSinceHint >= 3));
    public static bool HasQuota(int consumed) => consumed < DailyQuota;
}
