using System.Security.Claims;
using CodePath.Api.Middleware;
using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Auth.Services;
using CodePath.Domain.Auth.Entities;
using CodePath.Infrastructure.Auth.Options;
using CodePath.Infrastructure.Auth.Services.Tokens;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CodePath.Unit.Tests.Auth;

public sealed class SessionActivityMiddlewareTests
{
    [Theory]
    [InlineData(false, 10, 200, true)]
    [InlineData(true, 10, 200, false)]
    [InlineData(true, 61, 401, false)]
    public async Task Middleware_ShouldCheckSession_EvenWhenTrackingSkipped(bool skip, int idleMinutes, int status, bool touch)
    {
        var now = DateTime.UtcNow;
        var token = RefreshToken.Create(Guid.NewGuid(), "hash", now.AddDays(7), now.AddDays(7), now.AddMinutes(-idleMinutes));
        var db = new Mock<IAuthDbContext>();
        db.Setup(x => x.GetSessionAsync(token.SessionId, It.IsAny<CancellationToken>())).ReturnsAsync(token);
        var service = new SessionActivityService(db.Object, new AuthSessionPolicy(Options.Create(new AuthSessionOptions())), TimeProvider.System);
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim("sid", token.SessionId.ToString()), new Claim("sub", token.UserId.ToString()) }, "test"));
        context.Response.Body = new MemoryStream();
        if (skip) context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new SkipActivityTrackingAttribute()), "poll"));
        var called = false;
        var middleware = new SessionActivityMiddleware(_ => { called = true; return Task.CompletedTask; });
        await middleware.InvokeAsync(context, service);
        context.Response.StatusCode.Should().Be(status);
        called.Should().Be(status == 200);
        db.Verify(x => x.TouchSessionAsync(token.SessionId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), touch ? Times.Once : Times.Never);
        if (status == 401)
        {
            context.Response.Body.Position = 0;
            (await new StreamReader(context.Response.Body).ReadToEndAsync()).Should().Contain("SESSION_IDLE_TIMEOUT");
        }
    }
}
