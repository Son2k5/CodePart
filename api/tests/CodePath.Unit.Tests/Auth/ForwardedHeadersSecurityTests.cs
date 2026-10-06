using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodePath.Unit.Tests.Auth;

public sealed class ForwardedHeadersSecurityTests
{
    [Fact]
    public async Task UntrustedClientCannotSpoofXForwardedFor()
    {
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor,
            ForwardLimit = 1
        };
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
        options.KnownProxies.Add(IPAddress.Parse("10.0.0.10"));

        var middleware = new ForwardedHeadersMiddleware(
            _ => Task.CompletedTask,
            NullLoggerFactory.Instance,
            Options.Create(options));
        var context = new DefaultHttpContext();
        var actualClientIp = IPAddress.Parse("198.51.100.20");
        context.Connection.RemoteIpAddress = actualClientIp;
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.99";

        await middleware.Invoke(context);

        context.Connection.RemoteIpAddress.Should().Be(actualClientIp);
    }
}
