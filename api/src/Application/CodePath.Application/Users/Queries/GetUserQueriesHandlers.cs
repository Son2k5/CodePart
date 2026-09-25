using CodePath.Application.Users.Abstractions;
using CodePath.Application.Users.Dtos;
using CodePath.Application.Users.Queries;
using CodePath.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

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
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null) return Result<UserAuthDto>.Failure("User not found.");

        return Result<UserAuthDto>.Success(user.ToUserAuthDto());
    }
}

internal sealed class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, Result<UserAuthDto>>
{
    private readonly IUsersDbContext _dbContext;

    public GetUserByIdQueryHandler(IUsersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<UserAuthDto>> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user is null) return Result<UserAuthDto>.Failure("User not found.");

        return Result<UserAuthDto>.Success(user.ToUserAuthDto());
    }
}
