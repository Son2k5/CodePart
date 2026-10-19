using System.Threading.RateLimiting;
using CodePath.Api.Options;
using Xunit;

namespace CodePath.Unit.Tests.Judge;
public class JudgeRateLimitTests
{
    [Theory]
    [InlineData(false, 10)]
    [InlineData(true, 5)]
    public void User_budget_rejects_next_request_without_queueing(bool submit, int permits)
    {
        using var limiter = new SlidingWindowRateLimiter(JudgeRateLimits.Options(submit));
        for (var i = 0; i < permits; i++) { using var lease = limiter.AttemptAcquire(); Assert.True(lease.IsAcquired); }
        using var rejected = limiter.AttemptAcquire(); Assert.False(rejected.IsAcquired);
        using var anotherUser = new SlidingWindowRateLimiter(JudgeRateLimits.Options(submit));
        using var independent = anotherUser.AttemptAcquire(); Assert.True(independent.IsAcquired);
    }
}
