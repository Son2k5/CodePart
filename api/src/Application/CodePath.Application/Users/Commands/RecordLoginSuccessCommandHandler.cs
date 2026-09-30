using CodePath.Application.Users.Abstractions;
using CodePath.Shared.Kernel.Common;
using MediatR;

namespace CodePath.Application.Users.Commands;

internal sealed class RecordLoginSuccessCommandHandler : IRequestHandler<RecordLoginSuccessCommand, Result<bool>>
{
    private readonly IUsersDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public RecordLoginSuccessCommandHandler(IUsersDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<Result<bool>> Handle(RecordLoginSuccessCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null) return Result<bool>.Failure("User not found.");

        user.RecordLoginSuccess(_timeProvider.GetUtcNow().UtcDateTime);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
