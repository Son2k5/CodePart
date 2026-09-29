using CodePath.Application.Users.Abstractions;
using CodePath.Application.Users.Dtos;
using CodePath.Shared.Kernel.Common;
using MediatR;

namespace CodePath.Application.Users.Queries;

internal sealed class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, Result<UserAuthDto>>
{
    private readonly IUsersDbContext _dbContext;

    public GetUserByIdQueryHandler(IUsersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<UserAuthDto>> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.GetByIdReadOnlyAsync(request.UserId, cancellationToken);

        if (user is null) return Result<UserAuthDto>.Failure("User not found.");

        return Result<UserAuthDto>.Success(user.ToUserAuthDto());
    }
}
