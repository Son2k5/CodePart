using System.Security.Claims;
using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Users.Abstractions;
using CodePath.Shared.Kernel.Enums;
using Microsoft.AspNetCore.Authorization;

namespace CodePath.Api.Authorization;

public sealed class ActiveUserRequirement(UserRole? requiredRole = null) : IAuthorizationRequirement
{
    public UserRole? RequiredRole { get; } = requiredRole;
}

/// <summary>
/// Web authorization belongs to the API composition layer. Infrastructure only
/// supplies the cache and persistence implementations consumed here.
/// </summary>
public sealed class ActiveUserAuthorizationHandler : AuthorizationHandler<ActiveUserRequirement>
{
    private readonly IUsersDbContext _usersDbContext;
    private readonly IUserStatusCache _statusCache;

    public ActiveUserAuthorizationHandler(
        IUsersDbContext usersDbContext,
        IUserStatusCache statusCache)
    {
        _usersDbContext = usersDbContext;
        _statusCache = statusCache;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ActiveUserRequirement requirement)
    {
        var subject = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(subject, out var userId))
            return;

        var status = await _statusCache.TryGetStatusAsync(userId);
        if (!status.HasValue)
        {
            var user = await _usersDbContext.GetByIdReadOnlyAsync(userId);
            if (user is null)
                return;

            status = user.Status;
            await _statusCache.SetStatusAsync(userId, status.Value);
        }

        if (status != UserStatus.Active)
            return;

        if (requirement.RequiredRole.HasValue)
        {
            var roleClaim = context.User.FindFirst(ClaimTypes.Role)?.Value;
            if (!string.Equals(roleClaim, requirement.RequiredRole.Value.ToString(), StringComparison.Ordinal))
                return;
        }

        context.Succeed(requirement);
    }
}
