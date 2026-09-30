using CodePath.Application.Users.Abstractions;
using CodePath.Shared.Kernel.Common;
using MediatR;

namespace CodePath.Application.Users.Commands;

internal sealed class VerifyUserEmailCommandHandler : IRequestHandler<VerifyUserEmailCommand, Result<bool>>
{
    private readonly IUsersDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public VerifyUserEmailCommandHandler(IUsersDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<Result<bool>> Handle(VerifyUserEmailCommand request, CancellationToken cancellationToken)
    {
        var email = EmailNormalizer.Normalize(request.Email);
        var user = await _dbContext.GetByEmailAsync(email, cancellationToken);
        if (user is null) return Result<bool>.Failure("User not found.");

        user.VerifyEmail(_timeProvider.GetUtcNow().UtcDateTime);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
