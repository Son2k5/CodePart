using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Users.Abstractions;
using CodePath.Domain.Auth.Entities;
using CodePath.Shared.Kernel.Common;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CodePath.Application.Auth.Commands;

public sealed record ForgotPasswordCommand(string Email, string? IpAddress = null)
    : IRequest<Result<string>>;

public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator() =>
        RuleFor(command => command.Email).NotEmpty().MaximumLength(254).EmailAddress();
}

internal sealed class ForgotPasswordCommandHandler(
    IUsersDbContext usersDbContext,
    IAuthDbContext authDbContext,
    IPasswordResetRateLimiter rateLimiter,
    IEmailSender emailSender,
    TimeProvider timeProvider,
    ILogger<ForgotPasswordCommandHandler> logger)
    : IRequestHandler<ForgotPasswordCommand, Result<string>>
{
    private const string NeutralResponse =
        "If the account exists, password reset instructions will be sent to the supplied email address.";
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan MinimumResponseTime = TimeSpan.FromMilliseconds(250);

    public async Task<Result<string>> Handle(
        ForgotPasswordCommand request,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var normalizedEmail = EmailNormalizer.Normalize(request.Email);

        try
        {
            var acquired = await rateLimiter.TryAcquireAsync(normalizedEmail);
            var user = acquired
                ? await usersDbContext.GetByEmailReadOnlyAsync(normalizedEmail, cancellationToken)
                : null;

            if (user is not null && user.EmailVerifiedAt.HasValue)
            {
                var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                var tokenHash = Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
                var utcNow = timeProvider.GetUtcNow().UtcDateTime;
                var token = PasswordResetToken.Create(
                    user.Id,
                    tokenHash,
                    utcNow.Add(TokenLifetime),
                    utcNow,
                    request.IpAddress);

                await authDbContext.ReplaceActivePasswordResetTokenAsync(
                    token, utcNow, cancellationToken);

                try
                {
                    await emailSender.SendPasswordResetEmailAsync(
                        user.Email, rawToken, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "Password reset email delivery failed for {Recipient}.",
                        normalizedEmail);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Password reset request processing failed for {Recipient}.",
                normalizedEmail);
        }

        var elapsed = Stopwatch.GetElapsedTime(startedAt);
        var remainingDelay = MinimumResponseTime - elapsed;
        if (remainingDelay > TimeSpan.Zero)
            await Task.Delay(remainingDelay, cancellationToken);

        return Result<string>.Success(NeutralResponse);
    }
}
