using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Users.Queries;
using CodePath.Shared.Kernel.Enums;
using CodePath.Shared.Web.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace CodePath.Infrastructure.Auth.Authorization;

/// <summary>
/// Handler duy nhất cho <see cref="ActiveUserRequirement"/> (định nghĩa ở Shared.Web).
/// Api chỉ phụ thuộc Shared.Web, không phụ thuộc Infrastructure type.
/// </summary>
public sealed class ActiveUserAuthorizationHandler : AuthorizationHandler<ActiveUserRequirement>
{
    private readonly ISender _sender;
    private readonly IUserStatusCache _statusCache;

    public ActiveUserAuthorizationHandler(ISender sender, IUserStatusCache statusCache)
    {
        _sender = sender;
        _statusCache = statusCache;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ActiveUserRequirement requirement)
    {
        var sub = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
               ?? context.User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(sub, out var userId)) return;

        var cachedStatus = await _statusCache.TryGetStatusAsync(userId);

        UserStatus currentStatus;
        if (cachedStatus.HasValue)
        {
            currentStatus = cachedStatus.Value;
        }
        else
        {
            var userResult = await _sender.Send(new GetUserByIdQuery(userId));
            if (!userResult.IsSuccess || userResult.Value is null) return;

            currentStatus = userResult.Value.Status;
            await _statusCache.SetStatusAsync(userId, currentStatus);
        }

        if (currentStatus != UserStatus.Active) return;

        if (requirement.RequiredRole.HasValue)
        {
            var roleClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            if (roleClaim != requirement.RequiredRole.Value.ToString()) return;
        }

        context.Succeed(requirement);
    }
}
