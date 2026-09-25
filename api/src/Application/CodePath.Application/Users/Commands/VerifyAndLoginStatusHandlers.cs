using CodePath.Application.Users.Abstractions;
using CodePath.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace CodePath.Application.Users.Commands;

internal sealed class VerifyUserEmailCommandHandler : IRequestHandler<VerifyUserEmailCommand, Result<bool>>
{
    private readonly IUsersDbContext _dbContext;

    public VerifyUserEmailCommandHandler(IUsersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<bool>> Handle(VerifyUserEmailCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user is null) return Result<bool>.Failure("User not found.");

        user.VerifyEmail();
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

internal sealed class RecordLoginSuccessCommandHandler : IRequestHandler<RecordLoginSuccessCommand, Result<bool>>
{
    private readonly IUsersDbContext _dbContext;

    public RecordLoginSuccessCommandHandler(IUsersDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<bool>> Handle(RecordLoginSuccessCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        if (user is null) return Result<bool>.Failure("User not found.");

        user.RecordLoginSuccess();
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

internal sealed class UpdateUserStatusCommandHandler : IRequestHandler<UpdateUserStatusCommand, Result<bool>>
{
    private readonly IUsersDbContext _dbContext;
    private readonly IDatabase _redis;

    public UpdateUserStatusCommandHandler(IUsersDbContext dbContext, IConnectionMultiplexer redis)
    {
        _dbContext = dbContext;
        _redis = redis.GetDatabase();
    }

    public async Task<Result<bool>> Handle(UpdateUserStatusCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        if (user is null) return Result<bool>.Failure("User not found.");

        user.UpdateStatus(request.NewStatus);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _redis.KeyDeleteAsync($"user:status:{request.UserId}");

        return Result<bool>.Success(true);
    }
}
