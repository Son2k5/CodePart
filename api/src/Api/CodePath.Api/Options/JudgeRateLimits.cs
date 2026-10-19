using System.Threading.RateLimiting;
using CodePath.Application.Judge;

namespace CodePath.Api.Options;

public static class JudgeRateLimits
{
    public static SlidingWindowRateLimiterOptions Options(bool submit) => new()
    {
        PermitLimit = submit ? JudgeRules.SubmitPerMinute : JudgeRules.RunPerMinute,
        Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 60, QueueLimit = 0
    };
}
