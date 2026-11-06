using System.Text.Json;
using CodePath.Application.Notifications.Abstractions;
using CodePath.Application.Notifications.Contracts;
using CodePath.Application.Notifications.Events;
using CodePath.Application.Notifications.Validation;
using CodePath.Domain.Auditing.Entities;
using CodePath.Domain.Auditing.Enums;
using CodePath.Domain.Notifications.Entities;
using CodePath.Domain.Notifications.Enums;
using CodePath.Shared.Kernel.Common;
using FluentValidation;
using FluentValidation.Results;
using Ganss.Xss;
using Microsoft.Extensions.Logging;

namespace CodePath.Infrastructure.Notifications;

/// <summary>
/// Phase 1 implementation: validation, sanitization, preference gating, dedup/idempotency
/// and in-app persistence. Realtime and email channels are dispatched in later phases.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    private readonly INotificationDbContext _db;
    private readonly IValidator<NotificationRequest> _requestValidator;
    private readonly IValidator<BulkNotificationRequest> _bulkValidator;
    private readonly IValidator<BroadcastRequest> _broadcastValidator;
    private readonly IValidator<IReadOnlyList<NotificationPreferenceUpdateDto>> _preferenceValidator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<NotificationService> _logger;
    private readonly IRealtimeNotifier _realtime;
    private readonly IUnreadCountCache _unreadCache;
    private readonly IEventBus _events;

    public NotificationService(
        INotificationDbContext db,
        IValidator<NotificationRequest> requestValidator,
        IValidator<BulkNotificationRequest> bulkValidator,
        IValidator<BroadcastRequest> broadcastValidator,
        IValidator<IReadOnlyList<NotificationPreferenceUpdateDto>> preferenceValidator,
        TimeProvider timeProvider,
        ILogger<NotificationService> logger,
        IRealtimeNotifier realtime,
        IUnreadCountCache unreadCache,
        IEventBus events)
    {
        _db = db;
        _requestValidator = requestValidator;
        _bulkValidator = bulkValidator;
        _broadcastValidator = broadcastValidator;
        _preferenceValidator = preferenceValidator;
        _timeProvider = timeProvider;
        _logger = logger;
        _realtime = realtime;
        _unreadCache = unreadCache;
        _events = events;
    }

    public async Task<Result<NotificationDto?>> SendAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _requestValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Result<NotificationDto?>.ValidationFailure(ToValidationErrors(validation));

        var payloadJson = SerializePayload(request.Payload);
        if (payloadJson is not null && payloadJson.Length > NotificationLimits.MaxPayloadSerializedLength)
            return Result<NotificationDto?>.Failure("Notification payload exceeds the maximum size.", ErrorCodes.ValidationError);

        var dedupKey = BuildDedupKey(request.Type, request.RecipientId, request.EntityId);

        if (dedupKey is not null && await _db.DedupExistsAsync(request.RecipientId, dedupKey, cancellationToken))
        {
            // Idempotent no-op: a matching notification already exists.
            return Result<NotificationDto?>.Success(null);
        }

        var inAppEnabled = await IsInAppEnabledAsync(request.RecipientId, request.Type, cancellationToken);
        var emailEnabled = await IsEmailEnabledAsync(request.RecipientId, request.Type, request.AllowEmail, cancellationToken);
        if (!inAppEnabled && !emailEnabled)
            return Result<NotificationDto?>.Success(null);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var title = Sanitize(request.Title);
        var body = Sanitize(request.Body);

        Notification? notification = null;
        if (inAppEnabled)
        {
            notification = Notification.Create(
                request.RecipientId,
                request.Type,
                title,
                body,
                payloadJson,
                dedupKey,
                now);
            await _db.AddNotificationAsync(notification, cancellationToken);
        }

        // Stage the email onto the durable outbox so it commits atomically with the in-app row
        // and inherits the worker's retry/backoff/dead-letter. Skipped when in-app is disabled
        // and only an email is warranted (e.g. AccountVerifiedOtp).
        if (emailEnabled)
            await _events.PublishAsync(new EmailDispatchNotification(request.RecipientId, request.Type, title, body), cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        if (notification is null)
            return Result<NotificationDto?>.Success(null);

        var dto = ToDto(notification);
        await DispatchAsync(request.RecipientId, dto, cancellationToken);
        return Result<NotificationDto?>.Success(dto);
    }

    public async Task<Result<int>> SendBulkAsync(BulkNotificationRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _bulkValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Result<int>.ValidationFailure(ToValidationErrors(validation));

        var payloadJson = SerializePayload(request.Payload);
        if (payloadJson is not null && payloadJson.Length > NotificationLimits.MaxPayloadSerializedLength)
            return Result<int>.Failure("Notification payload exceeds the maximum size.", ErrorCodes.ValidationError);

        var title = Sanitize(request.Title);
        var body = Sanitize(request.Body);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var pending = new List<Notification>();
        var emailRecipients = new List<Guid>();
        foreach (var recipientId in request.RecipientIds.Distinct())
        {
            var dedupKey = BuildDedupKey(request.Type, recipientId, request.EntityId);
            if (dedupKey is not null && await _db.DedupExistsAsync(recipientId, dedupKey, cancellationToken))
                continue;

            var inAppEnabled = await IsInAppEnabledAsync(recipientId, request.Type, cancellationToken);
            var emailEnabled = await IsEmailEnabledAsync(recipientId, request.Type, request.AllowEmail, cancellationToken);
            if (!inAppEnabled && !emailEnabled)
                continue;

            if (inAppEnabled)
                pending.Add(Notification.Create(recipientId, request.Type, title, body, payloadJson, dedupKey, now));
            if (emailEnabled)
                emailRecipients.Add(recipientId);
        }

        foreach (var recipientId in emailRecipients)
            await _events.PublishAsync(new EmailDispatchNotification(recipientId, request.Type, title, body), cancellationToken);

        if (pending.Count > 0 || emailRecipients.Count > 0)
        {
            if (pending.Count > 0)
                await _db.AddNotificationsAsync(pending, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            foreach (var n in pending)
                await DispatchAsync(n.RecipientId, ToDto(n), cancellationToken);
        }

        return Result<int>.Success(pending.Count);
    }

    public async Task<Result<int>> BroadcastAsync(BroadcastRequest request, Guid actorId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var validation = await _broadcastValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Result<int>.ValidationFailure(ToValidationErrors(validation));

        IReadOnlyList<Guid> recipients = request.Scope switch
        {
            BroadcastScope.Role when request.Role.HasValue => await _db.GetUserIdsByRoleAsync(request.Role.Value, cancellationToken),
            BroadcastScope.Class when request.ClassId.HasValue => await _db.GetClassStudentIdsAsync(request.ClassId.Value, cancellationToken),
            _ => await _db.GetAllActiveUserIdsAsync(cancellationToken)
        };

        var title = Sanitize(request.Title);
        var body = Sanitize(request.Body);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var payloadJson = SerializePayload(new Dictionary<string, object?> { ["actorId"] = actorId });

        var pending = recipients
            .Distinct()
            .Select(recipientId => Notification.Create(recipientId, NotificationType.SystemBroadcast, title, body, payloadJson, dedupKey: null, now))
            .ToList();

        // System broadcasts are always persisted in-app; the email copy still honours each
        // recipient's EmailEnabled preference (default on) so users can opt out of mail.
        var emailRecipients = new List<Guid>();
        foreach (var recipientId in pending.Select(n => n.RecipientId).Distinct())
        {
            if (await IsEmailEnabledAsync(recipientId, NotificationType.SystemBroadcast, allowEmail: true, cancellationToken))
                emailRecipients.Add(recipientId);
        }

        foreach (var recipientId in emailRecipients)
            await _events.PublishAsync(new EmailDispatchNotification(recipientId, NotificationType.SystemBroadcast, title, body), cancellationToken);

        // Record the broadcast for audit; committed with the notification rows in the same save.
        // AuditLog requires a non-empty target, so the actor is recorded as the target for broadcasts.
        await _db.AddAuditLogAsync(AuditLog.Create(
            actorId, AuditAction.NotificationBroadcast, actorId, ipAddress,
            JsonSerializer.Serialize(new { scope = request.Scope.ToString(), role = request.Role?.ToString(), classId = request.ClassId, recipientCount = pending.Count }, PayloadOptions),
            now), cancellationToken);

        if (pending.Count > 0)
            await _db.AddNotificationsAsync(pending, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        if (pending.Count > 0)
        {
            foreach (var n in pending)
                await DispatchAsync(n.RecipientId, ToDto(n), cancellationToken);
        }

        _logger.LogInformation("Admin {ActorId} broadcast a notification to {Count} recipients (scope {Scope}).",
            actorId, pending.Count, request.Scope);

        return Result<int>.Success(pending.Count);
    }

    public async Task<CursorPage<NotificationDto>> ListAsync(
        Guid recipientId,
        bool? isRead,
        NotificationType? type,
        string? cursor,
        int take,
        CancellationToken cancellationToken = default)
    {
        var pageSize = take <= 0 ? NotificationLimits.DefaultPageSize : Math.Min(take, NotificationLimits.MaxPageSize);

        DateTime? cursorCreatedAt = null;
        Guid? cursorId = null;
        if (!string.IsNullOrWhiteSpace(cursor) && NotificationCursor.TryDecode(cursor, out var decoded))
        {
            cursorCreatedAt = decoded.Timestamp;
            cursorId = decoded.Id;
        }

        // Fetch one extra row to detect whether a further page exists.
        var rows = await _db.QueryAsync(recipientId, isRead, type, cursorCreatedAt, cursorId, pageSize + 1, cancellationToken);
        var hasMore = rows.Count > pageSize;
        var items = (hasMore ? rows.Take(pageSize) : rows).Select(ToDto).ToList();

        string? nextCursor = hasMore && items.Count > 0
            ? NotificationCursor.Encode(items[^1].CreatedAt, items[^1].Id)
            : null;

        return new CursorPage<NotificationDto>(items, nextCursor, hasMore);
    }

    public async Task<int> GetUnreadCountAsync(Guid recipientId, CancellationToken cancellationToken = default)
    {
        var cached = await _unreadCache.GetAsync(recipientId, cancellationToken);
        if (cached.HasValue)
            return cached.Value;

        var count = await _db.CountUnreadAsync(recipientId, cancellationToken);
        await _unreadCache.SetAsync(recipientId, count, cancellationToken);
        return count;
    }

    public async Task<Result<bool>> MarkAsReadAsync(Guid recipientId, Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _db.GetForRecipientAsync(notificationId, recipientId, cancellationToken);
        if (notification is null)
            return Result<bool>.Failure("Notification not found.", ErrorCodes.NotFound);

        if (notification.MarkRead(_timeProvider.GetUtcNow().UtcDateTime))
        {
            await _db.SaveChangesAsync(cancellationToken);
            await _unreadCache.InvalidateAsync(recipientId, cancellationToken);
        }

        return Result<bool>.Success(true);
    }

    public async Task<Result<int>> MarkAllAsReadAsync(Guid recipientId, CancellationToken cancellationToken = default)
    {
        var affected = await _db.MarkAllReadAsync(recipientId, _timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        if (affected > 0)
            await _unreadCache.InvalidateAsync(recipientId, cancellationToken);
        return Result<int>.Success(affected);
    }

    public async Task<Result<bool>> DeleteAsync(Guid recipientId, Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _db.GetForRecipientAsync(notificationId, recipientId, cancellationToken);
        if (notification is null)
            return Result<bool>.Failure("Notification not found.", ErrorCodes.NotFound);

        var wasUnread = !notification.IsRead;
        await _db.RemoveAsync(notification, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        if (wasUnread)
            await _unreadCache.InvalidateAsync(recipientId, cancellationToken);
        return Result<bool>.Success(true);
    }

    public async Task<IReadOnlyList<NotificationPreferenceDto>> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var preferences = await _db.GetPreferencesAsync(userId, cancellationToken);
        return preferences
            .Select(p => new NotificationPreferenceDto(p.Type, p.InAppEnabled, p.EmailEnabled))
            .ToList();
    }

    public async Task<Result<bool>> UpdatePreferencesAsync(
        Guid userId,
        IReadOnlyList<NotificationPreferenceUpdateDto> preferences,
        CancellationToken cancellationToken = default)
    {
        var validation = await _preferenceValidator.ValidateAsync(preferences, cancellationToken);
        if (!validation.IsValid)
            return Result<bool>.ValidationFailure(ToValidationErrors(validation));

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        foreach (var update in preferences)
        {
            var existing = await _db.GetPreferenceAsync(userId, update.Type, cancellationToken);
            if (existing is not null)
                existing.Update(update.InAppEnabled, update.EmailEnabled, now);
            else
                await _db.AddPreferenceAsync(NotificationPreference.Create(userId, update.Type, update.InAppEnabled, update.EmailEnabled, now), cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Best-effort realtime fan-out for a freshly persisted notification: push over the
    /// realtime channel and bump the unread cache. Never throws into the delivery path.
    /// </summary>
    private async Task DispatchAsync(Guid recipientId, NotificationDto dto, CancellationToken cancellationToken)
    {
        try
        {
            await _realtime.PushAsync(recipientId, dto, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Realtime push failed for recipient {RecipientId} (notification {Id}).",
                recipientId, dto.Id);
        }

        await _unreadCache.IncrementAsync(recipientId, 1, cancellationToken);
    }

    private async Task<bool> IsInAppEnabledAsync(Guid recipientId, NotificationType type, CancellationToken cancellationToken)
    {
        // Email-only categories (transient OTP) are never persisted in-app.
        if (type == NotificationType.AccountVerifiedOtp)
            return false;

        // System notifications are always persisted in-app; user preferences cannot mute them.
        if (type == NotificationType.SystemBroadcast)
            return true;

        var preference = await _db.GetPreferenceAsync(recipientId, type, cancellationToken);
        return preference?.InAppEnabled ?? true;
    }

    private async Task<bool> IsEmailEnabledAsync(Guid recipientId, NotificationType type, bool allowEmail, CancellationToken cancellationToken)
    {
        // Callers can force in-app-only delivery (transient/internal notifications).
        if (!allowEmail)
            return false;

        // OTP is delivered exclusively by email; it has no in-app representation.
        if (type == NotificationType.AccountVerifiedOtp)
            return true;

        var preference = await _db.GetPreferenceAsync(recipientId, type, cancellationToken);
        return preference?.EmailEnabled ?? true;
    }

    private static string? BuildDedupKey(NotificationType type, Guid recipientId, Guid? entityId)
        => entityId.HasValue ? $"{(int)type}:{recipientId:N}:{entityId.Value:N}" : null;

    private static string? SerializePayload(IReadOnlyDictionary<string, object?>? payload)
        => payload is null || payload.Count == 0
            ? null
            : JsonSerializer.Serialize(payload, PayloadOptions);

    private static string Sanitize(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        // Strip every HTML tag/attribute: notification bodies may embed user-supplied text.
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedCssProperties.Clear();
        return sanitizer.Sanitize(input);
    }

    private static NotificationDto ToDto(Notification notification)
    {
        JsonElement? payload = null;
        if (!string.IsNullOrWhiteSpace(notification.Payload))
        {
            try
            {
                using var document = JsonDocument.Parse(notification.Payload);
                payload = document.RootElement.Clone();
            }
            catch (JsonException)
            {
                payload = null;
            }
        }

        return new NotificationDto(
            notification.Id,
            notification.Type,
            notification.Title,
            notification.Body,
            payload,
            notification.IsRead,
            notification.ReadAt,
            notification.CreatedAt);
    }

    private static IEnumerable<ValidationError> ToValidationErrors(ValidationResult result)
        => result.Errors.Select(e => new ValidationError(e.PropertyName, e.ErrorMessage));
}
