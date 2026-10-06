using CodePath.Infrastructure.Auth.Options;
using CodePath.Infrastructure.Auth.Services.Passwords;
using CodePath.Infrastructure.Auth.Services.Tokens;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodePath.Unit.Tests.Auth;

public sealed class PasswordAndSessionPolicyTests
{
    [Fact]
    public void BCryptHasher_ShouldDetectHashCreatedWithOlderWorkFactor()
    {
        var oldHasher = new BCryptPasswordHasher(Options.Create(new PasswordHashingOptions
        {
            WorkFactor = 10
        }));
        var currentHasher = new BCryptPasswordHasher(Options.Create(new PasswordHashingOptions
        {
            WorkFactor = 11
        }));
        var hash = oldHasher.HashPassword("ExamplePassword1!");

        currentHasher.VerifyPassword("ExamplePassword1!", hash).Should().BeTrue();
        currentHasher.NeedsRehash(hash).Should().BeTrue();
    }

    [Fact]
    public void AuthSessionPolicy_ShouldUseConfiguredRefreshLifetimes()
    {
        var policy = new AuthSessionPolicy(Options.Create(new AuthSessionOptions
        {
            RefreshTokenLifetimeDays = 10,
            RefreshTokenAbsoluteLifetimeDays = 30
        }));

        policy.RefreshTokenLifetime.Should().Be(TimeSpan.FromDays(10));
        policy.RefreshTokenAbsoluteLifetime.Should().Be(TimeSpan.FromDays(30));
    }
}
