using CodePath.Application.Users.Abstractions;
using CodePath.Application.Users.Dtos;
using CodePath.Shared.Kernel.Common;
using MediatR;

namespace CodePath.Application.Users.Queries;

internal sealed class GetUserByEmailQueryHandler : IRequestHandler<GetUserByEmailQuery, Result<UserAuthDto>>
{
    private readonly IUsersDbContext _dbContext;

    public GetUserByEmailQueryHandler(IUsersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<UserAuthDto>> Handle(GetUserByEmailQuery request, CancellationToken cancellationToken)
    {
        var email = EmailNormalizer.Normalize(request.Email);
        var user = await _dbContext.GetByEmailReadOnlyAsync(email, cancellationToken);

        if (user is null) return Result<UserAuthDto>.Failure("User not found.");

        return Result<UserAuthDto>.Success(user.ToUserAuthDto());
    }
}
