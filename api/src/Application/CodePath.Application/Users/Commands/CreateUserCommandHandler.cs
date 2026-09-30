using CodePath.Application.Users.Abstractions;
using CodePath.Domain.Users.Entities;
using CodePath.Shared.Kernel.Common;
using CodePath.Shared.Kernel.Enums;
using CodePath.Shared.Kernel.Exceptions;
using MediatR;

namespace CodePath.Application.Users.Commands;

internal sealed class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Result<Guid>>
{
    private readonly IUsersDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public CreateUserCommandHandler(IUsersDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<Result<Guid>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = EmailNormalizer.Normalize(request.Email);
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        var existingUser = await _dbContext.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (existingUser != null)
        {
            if (existingUser.EmailVerifiedAt.HasValue)
            {
                return Result<Guid>.Failure("Email đã tồn tại trong hệ thống.", ErrorCodes.Conflict);
            }

            existingUser.UpdateUnverifiedAccount(request.FullName, request.PasswordHash, request.Role, request.StudentId, utcNow);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result<Guid>.Success(existingUser.Id);
        }

        User user = request.Role switch
        {
            UserRole.Student => User.CreateStudent(request.FullName, normalizedEmail, request.PasswordHash, request.StudentId!, utcNow),
            UserRole.Teacher => User.CreateTeacher(request.FullName, normalizedEmail, request.PasswordHash, utcNow),
            UserRole.Admin => throw new ArgumentOutOfRangeException(
                nameof(request.Role),
                "Admin accounts cannot be created through the general user creation command."),
            _ => throw new ArgumentOutOfRangeException(nameof(request.Role))
        };

        // UniqueViolation (email/studentId) được Infrastructure/UsersDbContext.SaveChangesAsync
        // chuyển thành ConflictException → Application bắt và chuyển sang Result.Failure
        try
        {
            await _dbContext.AddAsync(user, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result<Guid>.Success(user.Id);
        }
        catch (ConflictException ex)
        {
            return Result<Guid>.Failure(ex.Message, ErrorCodes.Conflict);
        }
    }
}
