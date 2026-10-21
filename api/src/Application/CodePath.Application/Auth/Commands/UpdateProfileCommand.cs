using CodePath.Application.Auth.Queries;
using CodePath.Application.Users.Abstractions;
using CodePath.Shared.Kernel.Common;
using FluentValidation;
using MediatR;

namespace CodePath.Application.Auth.Commands;

public sealed record UpdateProfileRequest(string FullName);
public sealed record UpdateProfileCommand(Guid UserId, string FullName) : IRequest<Result<UserProfileResponse>>;

public sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
    }
}

internal sealed class UpdateProfileCommandHandler(IUsersDbContext users, TimeProvider time)
    : IRequestHandler<UpdateProfileCommand, Result<UserProfileResponse>>
{
    public async Task<Result<UserProfileResponse>> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return Result<UserProfileResponse>.Failure("Không tìm thấy tài khoản.", ErrorCodes.NotFound);

        user.UpdateFullName(request.FullName, time.GetUtcNow().UtcDateTime);
        await users.SaveChangesAsync(cancellationToken);
        return Result<UserProfileResponse>.Success(new(user.Id, user.Email, user.FullName, user.Role.ToString(), user.Status.ToString()));
    }
}
