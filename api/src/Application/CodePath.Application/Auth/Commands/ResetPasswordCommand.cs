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
    // Stateless Data Protection tokens are variable-length protected payloads; only presence is checkable here.
    public ResetPasswordCommandValidator() =>
        RuleFor(command => command.NewPassword)
            .NotEmpty()
            .Must(PasswordPolicy.IsValid)
            .WithMessage("Password must be 12-128 characters and contain lowercase, uppercase, a number, and a special character.");
}

internal sealed class ResetPasswordCommandHandler(
    IAuthDbContext authDbContext,
    IPasswordHasher passwordHasher,
    IPasswordResetTokenService tokenService,
    IEmailSender emailSender,
    TimeProvider timeProvider,
    ILogger<ResetPasswordCommandHandler> logger)
    : IRequestHandler<ResetPasswordCommand, Result<string>>
{
    private const string InvalidTokenMessage =
        "The password reset link is invalid or has expired. Please request a new one.";

    public async Task<Result<string>> Handle(
        ResetPasswordCommand request,
        CancellationToken cancellationToken)
    {
        if (!tokenService.TryValidate(request.Token, out var userId, out var fingerprint))
            return Result<string>.Failure(InvalidTokenMessage, ErrorCodes.BadRequest);

        var passwordHash = passwordHasher.HashPassword(request.NewPassword);
        var completion = await authDbContext.TryResetPasswordAsync(
            userId,
            fingerprint,
            passwordHash,
            timeProvider.GetUtcNow().UtcDateTime,
            request.IpAddress,
            cancellationToken);

        if (completion is null)
        {
            // Fingerprint mismatch: the password changed since the link was issued, or the user no longer exists.
            return Result<string>.Failure(InvalidTokenMessage, ErrorCodes.BadRequest);
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
