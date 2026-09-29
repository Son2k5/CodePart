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

    public CreateUserCommandHandler(IUsersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<Guid>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var existingUser = await _dbContext.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (existingUser != null)
        {
            if (existingUser.EmailVerifiedAt.HasValue)
            {
                return Result<Guid>.Failure("Email đã tồn tại trong hệ thống.", ErrorCodes.Conflict);
            }

            existingUser.UpdateUnverifiedAccount(request.FullName, request.PasswordHash, request.Role, request.StudentId);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result<Guid>.Success(existingUser.Id);
        }

        User user = request.Role switch
        {
            UserRole.Student => User.CreateStudent(request.FullName, normalizedEmail, request.PasswordHash, request.StudentId!),
            UserRole.Teacher => User.CreateTeacher(request.FullName, normalizedEmail, request.PasswordHash),
            UserRole.Admin => User.CreateAdmin(request.FullName, normalizedEmail, request.PasswordHash),
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
