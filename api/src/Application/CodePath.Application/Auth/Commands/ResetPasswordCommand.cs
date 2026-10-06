using System.Security.Cryptography;
using System.Text;
using CodePath.Application.Auth.Abstractions;
using CodePath.Shared.Kernel.Common;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CodePath.Application.Auth.Commands;

public sealed record ResetPasswordCommand(
    string Token,
    string NewPassword,
    string? IpAddress = null) : IRequest<Result<string>>;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(command => command.Token)
            .NotEmpty()
            .Length(64)
            .Matches("^[A-Fa-f0-9]{64}$");
        RuleFor(command => command.NewPassword)
            .NotEmpty()
            .Must(PasswordPolicy.IsValid)
            .WithMessage("Password must be 12-128 characters and contain lowercase, uppercase, a number, and a special character.");
    }
}

internal sealed class ResetPasswordCommandHandler(
    IAuthDbContext authDbContext,
    IPasswordHasher passwordHasher,
    IEmailSender emailSender,
    TimeProvider timeProvider,
    ILogger<ResetPasswordCommandHandler> logger)
    : IRequestHandler<ResetPasswordCommand, Result<string>>
{
    public async Task<Result<string>> Handle(
        ResetPasswordCommand request,
        CancellationToken cancellationToken)
    {
        var tokenHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(request.Token)));
        var passwordHash = passwordHasher.HashPassword(request.NewPassword);
        var completion = await authDbContext.TryConsumePasswordResetTokenAsync(
            tokenHash,
            passwordHash,
            timeProvider.GetUtcNow().UtcDateTime,
            request.IpAddress,
            cancellationToken);

        if (completion is null)
        {
            return Result<string>.Failure(
                "The password reset token is invalid, expired, or has already been used.",
                ErrorCodes.BadRequest);
        }

        try
        {
            await emailSender.SendPasswordChangedEmailAsync(
                completion.Email, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Password change notification delivery failed for user {UserId}.",
                completion.UserId);
        }

        return Result<string>.Success("Password changed successfully. Please sign in again.");
    }
}
