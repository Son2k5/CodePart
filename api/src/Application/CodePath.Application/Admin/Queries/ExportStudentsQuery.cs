using System.Text;
using CodePath.Application.Admin.Abstractions;
using CodePath.Application.Admin.Dtos;
using CodePath.Shared.Kernel.Common;
using FluentValidation;
using MediatR;

namespace CodePath.Application.Admin.Queries;

public sealed record ExportStudentsQuery(StudentFilter Filter) : IRequest<Result<byte[]>>;

public sealed class ExportStudentsQueryValidator : AbstractValidator<ExportStudentsQuery>
{
    public ExportStudentsQueryValidator() => RuleFor(x => x.Filter).NotNull().SetValidator(new StudentFilterValidator());
}

internal sealed class ExportStudentsQueryHandler(IAdminDbContext db) : IRequestHandler<ExportStudentsQuery, Result<byte[]>>
{
    public Task<Result<byte[]>> Handle(ExportStudentsQuery r, CancellationToken ct) => db.ExecuteInTransactionAsync(async token =>
    {
        var csv = new StringBuilder("\uFEFFStudent ID,Full name,Email,Phone,Faculty,Status,Classes,Courses\r\n");
        var page = 1;
        OffsetPage<StudentDto> result;
        do
        {
            result = await db.GetPagedStudentsAsync(r.Filter with { Page = page++, PageSize = 100 }, token);
            foreach (var u in result.Items)
                csv.AppendLine(string.Join(",", new[] { u.StudentId, u.FullName, u.Email, u.Phone, u.FacultyName,
                    u.Status, u.ClassesCount.ToString(), u.CoursesCount.ToString() }.Select(Escape)));
        } while (page <= result.TotalPages);
        return Result<byte[]>.Success(Encoding.UTF8.GetBytes(csv.ToString()));
    }, ct);

    private static string Escape(string? value)
    {
        value ??= "";
        if (value.Length > 0 && ("=+-@".Contains(value.TrimStart().FirstOrDefault()) || char.IsControl(value[0])))
            value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
