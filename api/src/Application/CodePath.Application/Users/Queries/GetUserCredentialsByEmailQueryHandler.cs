using CodePath.Application.Users.Abstractions;
using CodePath.Application.Users.Dtos;
using CodePath.Shared.Kernel.Common;
using MediatR;

namespace CodePath.Application.Users.Queries;

internal sealed class GetUserCredentialsByEmailQueryHandler : IRequestHandler<GetUserCredentialsByEmailQuery, Result<UserCredentialsDto>>
{
    private readonly IUsersDbContext _dbContext;

    public GetUserCredentialsByEmailQueryHandler(IUsersDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<UserCredentialsDto>> Handle(GetUserCredentialsByEmailQuery request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.GetByEmailReadOnlyAsync(email, cancellationToken);

        if (user is null) return Result<UserCredentialsDto>.Failure("User not found.");

        return Result<UserCredentialsDto>.Success(user.ToUserCredentialsDto());
    }
}
