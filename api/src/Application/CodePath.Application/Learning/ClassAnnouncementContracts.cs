using CodePath.Application.Judge;
using CodePath.Shared.Kernel.Common;
using FluentValidation;

namespace CodePath.Application.Learning;

/// <summary>
/// Class-scoped announcements. Teachers of the class (or an admin, who can act for any class
/// including ones without an assigned teacher) post a note; every enrolled student receives it
/// on the class page plus an in-app notification.
/// </summary>
public sealed record ClassAnnouncementInput(string Title, string Body);
public sealed record ClassAnnouncementDto(Guid Id, string Title, string Body, string SenderName, DateTime CreatedAt);
public interface IClassAnnouncementService
{
    Task<Result<object>> List(Guid classId, Actor actor, CancellationToken ct);
    Task<Result<object>> Create(Guid classId, ClassAnnouncementInput input, Actor actor, CancellationToken ct);
    Task<Result<object>> Delete(Guid classId, Guid id, Actor actor, CancellationToken ct);
}
public sealed class ClassAnnouncementInputValidator : AbstractValidator<ClassAnnouncementInput>
{
    public ClassAnnouncementInputValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(5000);
    }
}
