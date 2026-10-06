using CodePath.Application.Users.Abstractions;
using CodePath.Shared.Kernel.Common;
using MediatR;

namespace CodePath.Application.Auth.Queries;

public sealed record UserProfileResponse(Guid Id, string Email, string FullName, string Role, string Status);

public sealed record GetCurrentUserQuery(Guid UserId) : IRequest<Result<UserProfileResponse>>;

internal sealed class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, Result<UserProfileResponse>>
{
    private readonly IUsersDbContext _usersDbContext;
    public GetCurrentUserQueryHandler(IUsersDbContext usersDbContext) =>
        _usersDbContext = usersDbContext;

    public async Task<Result<UserProfileResponse>> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _usersDbContext.GetByIdReadOnlyAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return Result<UserProfileResponse>.Failure("Không tìm thấy thông tin người dùng.");
        }

        return Result<UserProfileResponse>.Success(new UserProfileResponse(
            user.Id, user.Email, user.FullName, user.Role.ToString(), user.Status.ToString()));
    }
}
