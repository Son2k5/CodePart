using CodePath.Shared.Kernel.Enums;
using Microsoft.AspNetCore.Authorization;

namespace CodePath.Api.Authorization;

public sealed class ActiveUserRequirement(UserRole? requiredRole = null) : IAuthorizationRequirement
{
    public UserRole? RequiredRole { get; } = requiredRole;
}

/// <summary>
/// Authentication already verifies the user's current status and security
/// version against the database. Policies only need to enforce authentication
/// and the optional role, avoiding a second database query per request.
/// </summary>
public sealed class ActiveUserAuthorizationHandler : AuthorizationHandler<ActiveUserRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ActiveUserRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return Task.CompletedTask;

        if (requirement.RequiredRole.HasValue)
        {
            var roleClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            if (!string.Equals(roleClaim, requirement.RequiredRole.Value.ToString(), StringComparison.Ordinal))
                return Task.CompletedTask;
        }

        context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
