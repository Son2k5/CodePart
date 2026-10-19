using System.Text;
using CodePath.Application.Admin.Abstractions;
using CodePath.Application.Admin.Dtos;
using CodePath.Shared.Kernel.Common;
using FluentValidation;
using MediatR;

namespace CodePath.Application.Admin.Queries;

public sealed record ExportTeachersQuery(TeacherFilter Filter) : IRequest<Result<byte[]>>;
public sealed class ExportTeachersQueryValidator : AbstractValidator<ExportTeachersQuery>
{
    public ExportTeachersQueryValidator() => RuleFor(x => x.Filter).NotNull().SetValidator(new TeacherFilterValidator());
}
internal sealed class ExportTeachersQueryHandler(IAdminDbContext db) : IRequestHandler<ExportTeachersQuery, Result<byte[]>>
{
    public Task<Result<byte[]>> Handle(ExportTeachersQuery r, CancellationToken ct) => db.ExecuteInTransactionAsync(async token =>
    {
        var csv = new StringBuilder("\uFEFFTeacher ID,Full name,Email,Phone,Faculty,Status,Classes,Courses\r\n");
        var page = 1;
        OffsetPage<TeacherDto> result;
        do
        {
            result = await db.GetPagedTeachersAsync(r.Filter with { Page = page++, PageSize = 100 }, token);
            foreach (var u in result.Items)
                csv.AppendLine(string.Join(",", new[] { u.TeacherId, u.FullName, u.Email, u.Phone, u.FacultyName,
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
