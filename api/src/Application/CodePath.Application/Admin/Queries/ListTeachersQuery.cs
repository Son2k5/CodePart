using CodePath.Application.Admin.Abstractions;
using CodePath.Application.Admin.Dtos;
using CodePath.Shared.Kernel.Common;
using CodePath.Shared.Kernel.Enums;
using FluentValidation;
using MediatR;

namespace CodePath.Application.Admin.Queries;

public sealed record ListTeachersQuery(
    UserStatus? Status,
    string? Cursor,
    int Limit = 20) : IRequest<Result<CursorPage<TeacherSummaryDto>>>;

public sealed class ListTeachersQueryValidator : AbstractValidator<ListTeachersQuery>
{
    public ListTeachersQueryValidator()
    {
        RuleFor(query => query.Limit).InclusiveBetween(1, 100);
        RuleFor(query => query.Status)
            .Must(status => status is null || Enum.IsDefined(status.Value))
            .WithMessage("Status is invalid.");
    }
}

internal sealed class ListTeachersQueryHandler
    : IRequestHandler<ListTeachersQuery, Result<CursorPage<TeacherSummaryDto>>>
{
    private readonly IAdminDbContext _dbContext;

    public ListTeachersQueryHandler(IAdminDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<CursorPage<TeacherSummaryDto>>> Handle(
        ListTeachersQuery request,
        CancellationToken cancellationToken)
    {
        AdminCursor? cursor = null;
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            if (!AdminCursor.TryDecode(request.Cursor, out var decoded))
                return Result<CursorPage<TeacherSummaryDto>>.Failure("Cursor is invalid.");
            cursor = decoded;
        }

        var users = await _dbContext.GetTeachersAsync(
            request.Status,
            cursor?.Timestamp,
            cursor?.Id,
            request.Limit + 1,
            cancellationToken);

        var hasMore = users.Count > request.Limit;
        var pageUsers = users.Take(request.Limit).ToArray();
        var items = pageUsers.Select(user => new TeacherSummaryDto(
            user.Id,
            user.FullName,
            user.Email,
            user.Status.ToString(),
            user.FacultyId,
            user.EmailVerifiedAt,
            user.CreatedAt)).ToArray();

        var nextCursor = hasMore && pageUsers.Length > 0
            ? AdminCursor.Encode(pageUsers[^1].CreatedAt, pageUsers[^1].Id)
            : null;

        return Result<CursorPage<TeacherSummaryDto>>.Success(new(items, nextCursor));
    }
}
