using CodePath.Shared.Kernel.Enums;

namespace CodePath.Application.Auth.Abstractions;

public interface IUserStatusCache
{
    Task<UserStatus?> TryGetStatusAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SetStatusAsync(Guid userId, UserStatus status, TimeSpan? ttl = null, CancellationToken cancellationToken = default);
    Task InvalidateAsync(Guid userId, CancellationToken cancellationToken = default);
}
