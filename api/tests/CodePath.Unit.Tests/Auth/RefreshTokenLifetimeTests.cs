using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Auth.Commands;
using CodePath.Application.Auth.Services;
using CodePath.Application.Users.Abstractions;
using CodePath.Domain.Auth.Entities;
using CodePath.Domain.Users.Entities;
using CodePath.Infrastructure.Auth.Options;
using CodePath.Infrastructure.Auth.Services.Tokens;
using CodePath.Shared.Kernel.Common;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CodePath.Unit.Tests.Auth;

public sealed class RefreshTokenLifetimeTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
    private readonly Mock<IAuthDbContext> db = new();
    private readonly Mock<IJwtTokenService> jwt = new();
    private readonly Mock<IUsersDbContext> users = new();
    private readonly IAuthSessionPolicy policy = new AuthSessionPolicy(Options.Create(new AuthSessionOptions()));

    private Task<Result<LoginResponse>> Refresh(RefreshToken token)
    {
        db.Setup(x => x.GetRefreshTokenAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(token);
        jwt.Setup(x => x.HashRefreshToken("raw")).Returns("hash");
        var type = typeof(RefreshTokenCommand).Assembly.GetType("CodePath.Application.Auth.Commands.RefreshTokenCommandHandler")!;
        var handler = (IRequestHandler<RefreshTokenCommand, Result<LoginResponse>>)Activator.CreateInstance(
            type, db.Object, jwt.Object, users.Object, policy, new FixedTimeProvider(Now))!;
        return handler.Handle(new RefreshTokenCommand("raw"), CancellationToken.None);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(61)]
    [InlineData(5760)]
    public async Task IdleRefresh_ShouldRevokeSession_WithoutIssuingTokens(int minutes)
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "hash", Now.AddDays(1), Now.AddDays(1), Now.AddMinutes(-minutes));
        var result = await Refresh(token);
        result.ErrorCode.Should().Be(SessionActivityService.IdleCode);
        db.Verify(x => x.RevokeSessionAsync(token.SessionId, Now, "IDLE_TIMEOUT", It.IsAny<CancellationToken>()), Times.Once);
        users.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Refresh_ShouldPreserveActivityAndSessionAndAbsoluteDeadline()
    {
        var user = User.CreateAdmin("Admin", "admin@example.com", "hash", Now);
        var token = RefreshToken.Create(user.Id, "hash", Now.AddDays(6), Now.AddDays(6), Now.AddMinutes(-30));
        users.Setup(x => x.GetByIdReadOnlyAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        jwt.Setup(x => x.GenerateTokens(user.Id, user.Email, user.Role, user.Status, user.SecurityVersion, token.SessionId))
            .Returns(new GeneratedTokens("access", "replacement", 900));
        jwt.Setup(x => x.HashRefreshToken("replacement")).Returns("replacement-hash");
        RefreshToken? replacement = null;
        db.Setup(x => x.TryRotateRefreshTokenAsync("hash", It.IsAny<RefreshToken>(), Now, null, It.IsAny<CancellationToken>()))
            .Callback<string, RefreshToken, DateTime, string?, CancellationToken>((_, t, _, _, _) => replacement = t)
            .ReturnsAsync(true);
        var result = await Refresh(token);
        result.IsSuccess.Should().BeTrue();
        replacement!.LastActiveAt.Should().Be(token.LastActiveAt);
        replacement.SessionId.Should().Be(token.SessionId);
        replacement.AbsoluteExpiresAt.Should().Be(token.AbsoluteExpiresAt);
        replacement.ExpiresAt.Should().Be(token.AbsoluteExpiresAt);
        db.Verify(x => x.TouchSessionAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AbsoluteSevenDays_ShouldExpireEvenWithRecentActivityAndRotation()
    {
        var parent = RefreshToken.Create(Guid.NewGuid(), "hash", Now, Now, Now.AddDays(-7));
        parent.PreserveActivity(Now.AddMinutes(-1));
        var rotated = RefreshToken.CreateReplacement(parent, "hash", Now.AddMinutes(-10), TimeSpan.FromDays(7));
        var result = await Refresh(rotated);
        result.ErrorCode.Should().Be(SessionActivityService.ExpiredCode);
        db.Verify(x => x.RevokeSessionAsync(parent.SessionId, Now, "SESSION_EXPIRED", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(4, true, false)]
    [InlineData(5, true, true)]
    [InlineData(10, false, false)]
    public async Task Activity_ShouldRespectThrottleAndSkipMetadata(int ageMinutes, bool track, bool expectedWrite)
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "hash", Now.AddDays(7), Now.AddDays(7), Now.AddMinutes(-ageMinutes));
        db.Setup(x => x.GetSessionAsync(token.SessionId, It.IsAny<CancellationToken>())).ReturnsAsync(token);
        var service = new SessionActivityService(db.Object, policy, new FixedTimeProvider(Now));
        (await service.ValidateAsync(token.SessionId, token.UserId, track, CancellationToken.None)).Should().BeNull();
        db.Verify(x => x.TouchSessionAsync(token.SessionId, Now, Now.AddMinutes(-60), Now.AddMinutes(-5), It.IsAny<CancellationToken>()), expectedWrite ? Times.Once : Times.Never);
    }

    [Fact]
    public async Task SkippedActivity_StillRejectsIdleSession()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "hash", Now.AddDays(7), Now.AddDays(7), Now.AddHours(-2));
        db.Setup(x => x.GetSessionAsync(token.SessionId, It.IsAny<CancellationToken>())).ReturnsAsync(token);
        var service = new SessionActivityService(db.Object, policy, new FixedTimeProvider(Now));
        (await service.ValidateAsync(token.SessionId, token.UserId, false, CancellationToken.None)).Should().Be(SessionActivityService.IdleCode);
        db.Verify(x => x.TouchSessionAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}
