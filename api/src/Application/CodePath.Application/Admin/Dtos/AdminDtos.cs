using System.Text.Json;

namespace CodePath.Application.Admin.Dtos;

public sealed record TeacherSummaryDto(
    Guid Id,
    string FullName,
    string Email,
    string Status,
    Guid? FacultyId,
    DateTime? EmailVerifiedAt,
    DateTime CreatedAt);

public sealed record AuditLogDto(
    Guid Id,
    Guid ActorId,
    string Action,
    Guid TargetUserId,
    DateTime OccurredAt,
    string? IpAddress,
    JsonElement Data);

public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor);

public sealed record AdminUserStatusChangeDto(
    Guid UserId,
    string Status,
    int RevokedSessionCount,
    Guid AuditLogId);
