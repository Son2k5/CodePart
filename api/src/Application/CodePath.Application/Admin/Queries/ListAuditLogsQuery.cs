using System.Text.Json;
using CodePath.Application.Admin.Abstractions;
using CodePath.Application.Admin.Dtos;
using CodePath.Shared.Kernel.Common;
using FluentValidation;
using MediatR;

namespace CodePath.Application.Admin.Queries;

public sealed record ListAuditLogsQuery(
    Guid? TargetUserId,
    string? Cursor,
    int Limit = 20) : IRequest<Result<CursorPage<AuditLogDto>>>;

public sealed class ListAuditLogsQueryValidator : AbstractValidator<ListAuditLogsQuery>
{
    public ListAuditLogsQueryValidator() =>
        RuleFor(query => query.Limit).InclusiveBetween(1, 100);
}

internal sealed class ListAuditLogsQueryHandler
    : IRequestHandler<ListAuditLogsQuery, Result<CursorPage<AuditLogDto>>>
{
    private readonly IAdminDbContext _dbContext;

    public ListAuditLogsQueryHandler(IAdminDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<CursorPage<AuditLogDto>>> Handle(
        ListAuditLogsQuery request,
        CancellationToken cancellationToken)
    {
        AdminCursor? cursor = null;
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            if (!AdminCursor.TryDecode(request.Cursor, out var decoded))
                return Result<CursorPage<AuditLogDto>>.Failure("Cursor is invalid.");
            cursor = decoded;
        }

        var logs = await _dbContext.GetAuditLogsAsync(
            request.TargetUserId,
            cursor?.Timestamp,
            cursor?.Id,
            request.Limit + 1,
            cancellationToken);

        var hasMore = logs.Count > request.Limit;
        var pageLogs = logs.Take(request.Limit).ToArray();
        var items = pageLogs.Select(log => new AuditLogDto(
            log.Id,
            log.ActorId,
            log.Action.ToString(),
            log.TargetUserId,
            log.OccurredAt,
            log.IpAddress,
            JsonDocument.Parse(log.DataJson).RootElement.Clone())).ToArray();

        var nextCursor = hasMore && pageLogs.Length > 0
            ? AdminCursor.Encode(pageLogs[^1].OccurredAt, pageLogs[^1].Id)
            : null;

        return Result<CursorPage<AuditLogDto>>.Success(new(items, nextCursor));
    }
}
