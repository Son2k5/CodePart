using CodePath.Application.Users.Abstractions;
using CodePath.Domain.Users.Entities;
using CodePath.Shared.Kernel.Common;
using CodePath.Shared.Kernel.Enums;
using CodePath.Shared.Kernel.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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

        var existingUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
        if (existingUser != null)
        {
            if (existingUser.EmailVerifiedAt.HasValue)
            {
                throw new ConflictException("Email already exists in the system.");
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

        try
        {
            await _dbContext.Users.AddAsync(user, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result<Guid>.Success(user.Id);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new ConflictException("Email or student code already exists.");
        }
    }
}
