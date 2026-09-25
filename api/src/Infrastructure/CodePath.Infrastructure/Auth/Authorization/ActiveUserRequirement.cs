using System.Security.Claims;
using CodePath.Application.Users.Queries;
using CodePath.Shared.Kernel.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using StackExchange.Redis;

namespace CodePath.Infrastructure.Auth.Authorization;

public class ActiveUserRequirement : IAuthorizationRequirement
{
    public UserRole? RequiredRole { get; }
    public ActiveUserRequirement(UserRole? requiredRole = null) => RequiredRole = requiredRole;
}

public class ActiveUserAuthorizationHandler : AuthorizationHandler<ActiveUserRequirement>
{
    private readonly ISender _sender;
    private readonly IDatabase _redis;

    public ActiveUserAuthorizationHandler(ISender sender, IConnectionMultiplexer redis)
    {
        _sender = sender;
        _redis = redis.GetDatabase();
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ActiveUserRequirement requirement)
    {
        var sub = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
               ?? context.User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(sub, out var userId)) return;

        var cacheKey = $"user:status:{userId}";
        var cachedStatus = await _redis.StringGetAsync(cacheKey);

        UserStatus currentStatus;
        if (cachedStatus.HasValue && Enum.TryParse<UserStatus>(cachedStatus.ToString(), out var parsedStatus))
        {
            currentStatus = parsedStatus;
        }
        else
        {
            var userResult = await _sender.Send(new GetUserByIdQuery(userId));
            if (!userResult.IsSuccess || userResult.Value is null) return;

            currentStatus = userResult.Value.Status;
            await _redis.StringSetAsync(cacheKey, currentStatus.ToString(), TimeSpan.FromMinutes(5));
        }

        if (currentStatus != UserStatus.Active) return;

        if (requirement.RequiredRole.HasValue)
        {
            var roleClaim = context.User.FindFirst(ClaimTypes.Role)?.Value;
            if (roleClaim != requirement.RequiredRole.Value.ToString()) return;
        }

        context.Succeed(requirement);
    }
}
