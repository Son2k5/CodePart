using CodePath.Application.Notifications.Contracts;
using CodePath.Domain.Notifications.Enums;
using FluentValidation;

namespace CodePath.Application.Notifications.Validation;

/// <summary>Hard size limits to bound payload/storage cost and blunt content-based attacks.</summary>
public static class NotificationLimits
{
    public const int MaxTitleLength = 200;
    public const int MaxBodyLength = 2000;
    public const int MaxPayloadSerializedLength = 4096;
    public const int MaxBulkRecipients = 500;
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;
}

public sealed class NotificationRequestValidator : AbstractValidator<NotificationRequest>
{
    public NotificationRequestValidator()
    {
        RuleFor(x => x.RecipientId).NotEmpty();
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(NotificationLimits.MaxTitleLength);
        RuleFor(x => x.Body).NotNull().MaximumLength(NotificationLimits.MaxBodyLength);
    }
}

public sealed class BulkNotificationRequestValidator : AbstractValidator<BulkNotificationRequest>
{
    public BulkNotificationRequestValidator()
    {
        RuleFor(x => x.RecipientIds)
            .NotEmpty()
            .Must(ids => ids.Count <= NotificationLimits.MaxBulkRecipients)
            .WithMessage($"A bulk send is limited to {NotificationLimits.MaxBulkRecipients} recipients.");
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(NotificationLimits.MaxTitleLength);
        RuleFor(x => x.Body).NotNull().MaximumLength(NotificationLimits.MaxBodyLength);
    }
}

public sealed class BroadcastRequestValidator : AbstractValidator<BroadcastRequest>
{
    public BroadcastRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(NotificationLimits.MaxTitleLength);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(NotificationLimits.MaxBodyLength);
        RuleFor(x => x.Scope).IsInEnum();
        RuleFor(x => x.Role).NotEmpty().When(x => x.Scope == BroadcastScope.Role)
            .WithMessage("A role is required for a role-scoped broadcast.");
        RuleFor(x => x.ClassId).NotEmpty().When(x => x.Scope == BroadcastScope.Class)
            .WithMessage("A class id is required for a class-scoped broadcast.");
    }
}

public sealed class NotificationPreferenceUpdateValidator : AbstractValidator<IReadOnlyList<NotificationPreferenceUpdateDto>>
{
    public NotificationPreferenceUpdateValidator()
    {
        RuleFor(x => x).NotEmpty();
        RuleForEach(x => x).ChildRules(preference =>
        {
            preference.RuleFor(x => x.Type).IsInEnum();
        });
    }
}
