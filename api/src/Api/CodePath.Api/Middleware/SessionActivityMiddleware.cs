using CodePath.Api.Endpoints;
using CodePath.Application.Auth.Services;

namespace CodePath.Api.Middleware;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class SkipActivityTrackingAttribute : Attribute;

// Auth lifecycle endpoints perform their own session validation and must remain
// reachable with an expired/revoked access session (especially idempotent logout).
[AttributeUsage(AttributeTargets.Method)]
public sealed class SessionLifecycleAttribute : Attribute;

public sealed class SessionActivityMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, SessionActivityService sessions)
    {
        var endpoint = context.GetEndpoint();
        if (context.User.Identity?.IsAuthenticated != true
            || endpoint?.Metadata.GetMetadata<SessionLifecycleAttribute>() is not null)
        {
            await next(context);
            return;
        }
        var error = SessionActivityService.IdleCode;
        if (context.User.GetSessionId() is { } sessionId
            && context.User.GetUserId() is { } userId)
        {
            error = await sessions.ValidateAsync(sessionId, userId,
                endpoint?.Metadata.GetMetadata<SkipActivityTrackingAttribute>() is null,
                context.RequestAborted);
        }
        if (error is not null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { code = error }, context.RequestAborted);
            return;
        }
        await next(context);
    }
}
