using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Users.Abstractions;
using CodePath.Shared.Kernel.Common;
using MediatR;

namespace CodePath.Application.Users.Commands;

internal sealed class UpdateUserStatusCommandHandler : IRequestHandler<UpdateUserStatusCommand, Result<bool>>
{
    private readonly IUsersDbContext _dbContext;
    private readonly IUserStatusCache _statusCache;

    public UpdateUserStatusCommandHandler(IUsersDbContext dbContext, IUserStatusCache statusCache)
    {
        _dbContext = dbContext;
        _statusCache = statusCache;
    }

    public async Task<Result<bool>> Handle(UpdateUserStatusCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null) return Result<bool>.Failure("User not found.");

        user.UpdateStatus(request.NewStatus);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _statusCache.InvalidateAsync(request.UserId, cancellationToken);

        return Result<bool>.Success(true);
    }
}
