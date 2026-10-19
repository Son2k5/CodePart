using CodePath.Application.Judge;
using CodePath.Domain.Learning;
using CodePath.Shared.Kernel.Common;
using MediatR;

namespace CodePath.Application.Learning;

public sealed record CurriculumTemplateInput(string Name, string Description, string? Thumbnail = null, Guid? FacultyId = null);
public sealed record ApplyTemplateInput(Guid TemplateId, Guid[]? ChapterIds = null, bool CopyProblems = false, Guid? AfterChapterId = null);
public sealed record LectureVideo(string Provider, string Url, double DurationSec, string? Title = null);
public sealed record LectureDocument(Guid AssetId, string Title);
public sealed record LectureContent(string? Markdown = null, LectureVideo? Video = null, LectureDocument[]? Documents = null);
public sealed record CreateFullTopicInput(Guid ChapterId, string Title, string Slug, string Summary, int Order,
    int EstimatedMinutes, LectureContent LectureContent, ExerciseInput[] Exercises, CompletionRule CompletionRule,
    UnlockRule UnlockRule = UnlockRule.None, DateTime? UnlockAt = null, DateTime? DueAt = null, bool PublishImmediately = false);
public interface ICurriculumService
{
    Task<Result<object>> Templates(Guid? id, string action, CurriculumTemplateInput? input, bool confirm, Actor actor, CancellationToken ct);
    Task<Result<object>> CreateFullTopic(Guid classId, CreateFullTopicInput input, Actor actor, CancellationToken ct);
    Task<Result<object>> AvailableProblems(Guid classId, string? search, string? difficulty, int page, int pageSize, Actor actor, CancellationToken ct);
    Task<Result<object>> ApplyTemplate(Guid classId, ApplyTemplateInput input, string key, Actor actor, CancellationToken ct);
}
public sealed record CreateFullTopicCommand(Guid ClassId, CreateFullTopicInput Input, Actor Actor) : IRequest<Result<object>>;
public sealed record CreateCurriculumTemplateCommand(CurriculumTemplateInput Input, Actor Actor) : IRequest<Result<object>>;
public sealed record ListCurriculumTemplatesQuery(Actor Actor) : IRequest<Result<object>>;
public sealed class CurriculumHandlers(ICurriculumService service) :
    IRequestHandler<CreateFullTopicCommand, Result<object>>,
    IRequestHandler<CreateCurriculumTemplateCommand, Result<object>>,
    IRequestHandler<ListCurriculumTemplatesQuery, Result<object>>
{
    public Task<Result<object>> Handle(CreateFullTopicCommand r, CancellationToken ct) => service.CreateFullTopic(r.ClassId, r.Input, r.Actor, ct);
    public Task<Result<object>> Handle(CreateCurriculumTemplateCommand r, CancellationToken ct) => service.Templates(null, "create", r.Input, false, r.Actor, ct);
    public Task<Result<object>> Handle(ListCurriculumTemplatesQuery r, CancellationToken ct) => service.Templates(null, "list", null, false, r.Actor, ct);
}
