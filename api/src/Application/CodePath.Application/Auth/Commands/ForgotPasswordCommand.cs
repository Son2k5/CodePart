using System.Diagnostics;
using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Auth.Events;
using CodePath.Application.Notifications.Abstractions;
using CodePath.Application.Users.Abstractions;
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
    IPasswordResetTokenService tokenService,
    IEventBus events,
    ILogger<ForgotPasswordCommandHandler> logger)
    : IRequestHandler<ForgotPasswordCommand, Result<string>>
{
    private const string NeutralResponse =
        "If the account exists, password reset instructions will be sent to the supplied email address.";
    private static readonly TimeSpan MinimumResponseTime = TimeSpan.FromMilliseconds(250);

    public async Task<Result<string>> Handle(
        ForgotPasswordCommand request,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var normalizedEmail = EmailNormalizer.Normalize(request.Email);

        try
        {
            var acquired = await rateLimiter.TryAcquireAsync(normalizedEmail, request.IpAddress);
            var user = acquired
                ? await usersDbContext.GetByEmailReadOnlyAsync(normalizedEmail, cancellationToken)
                : null;

            if (user is not null && user.EmailVerifiedAt.HasValue)
            {
                // Stateless token (15-minute TTL, Data Protection): nothing to persist here.
                var token = tokenService.Create(user.Id, user.PasswordHash);

                // Reset email goes through the durable outbox: no synchronous SMTP on the
                // request path; the worker retries with backoff and dead-letters on failure.
                await events.PublishAsync(
                    new PasswordResetEmailNotification(user.Id, token), cancellationToken);
                await authDbContext.SaveChangesAsync(cancellationToken);
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
