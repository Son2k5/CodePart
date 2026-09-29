using CodePath.Application.Users.Abstractions;
using CodePath.Shared.Kernel.Common;
using MediatR;

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
        var user = await _dbContext.GetByEmailAsync(email, cancellationToken);
        if (user is null) return Result<bool>.Failure("User not found.");

        user.VerifyEmail();
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
