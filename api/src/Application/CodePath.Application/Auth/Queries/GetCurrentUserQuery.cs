using CodePath.Application.Users.Queries;
using CodePath.Shared.Kernel.Common;
using MediatR;

namespace CodePath.Application.Auth.Queries;

public sealed record UserProfileResponse(Guid Id, string Email, string FullName, string Role, string Status);

public sealed record GetCurrentUserQuery(Guid UserId) : IRequest<Result<UserProfileResponse>>;

internal sealed class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, Result<UserProfileResponse>>
{
    private readonly ISender _sender;
    public GetCurrentUserQueryHandler(ISender sender) => _sender = sender;

    public async Task<Result<UserProfileResponse>> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var userResult = await _sender.Send(new GetUserByIdQuery(request.UserId), cancellationToken);
        if (!userResult.IsSuccess || userResult.Value is null)
        {
            return Result<UserProfileResponse>.Failure("Không tìm thấy thông tin người dùng.");
        }

        var user = userResult.Value;
        return Result<UserProfileResponse>.Success(new UserProfileResponse(
            user.Id, user.Email, user.FullName, user.Role.ToString(), user.Status.ToString()));
    }
}
