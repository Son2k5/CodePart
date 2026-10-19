using CodePath.Api.Extensions;
using CodePath.Application.Learning;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CodePath.Api.Endpoints;

public static partial class LearningEndpoints
{
    private static void MapCurriculumEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").RequireAuthorization("CanManageClassContent").WithTags("Curriculum");
        group.MapGet("/curriculum-templates", async (HttpContext h, ISender sender, CancellationToken ct) =>
            (await sender.Send(new ListCurriculumTemplatesQuery(Actor(h)), ct)).ToHttpResult()).WithSummary("List shared and owned curriculum templates");
        group.MapPost("/curriculum-templates", async (CurriculumTemplateInput body, HttpContext h, ISender sender, CancellationToken ct) =>
            (await sender.Send(new CreateCurriculumTemplateCommand(body, Actor(h)), ct)).ToHttpResult()).WithSummary("Create a curriculum template");
        group.MapGet("/curriculum-templates/{id:guid}", async (Guid id, HttpContext h, ICurriculumService s, CancellationToken ct) =>
            (await s.Templates(id, "get", null, false, Actor(h), ct)).ToHttpResult());
        group.MapPut("/curriculum-templates/{id:guid}", async (Guid id, CurriculumTemplateInput body, HttpContext h, ICurriculumService s, CancellationToken ct) =>
            (await s.Templates(id, "update", body, false, Actor(h), ct)).ToHttpResult());
        group.MapDelete("/curriculum-templates/{id:guid}", async (Guid id, [FromQuery] bool? confirm, HttpContext h, ICurriculumService s, CancellationToken ct) =>
            (await s.Templates(id, "delete", null, confirm == true, Actor(h), ct)).ToHttpResult());
        group.MapPost("/classes/{classId:guid}/apply-template", async (Guid classId, ApplyTemplateInput body, [FromHeader(Name = "Idempotency-Key")] string key, HttpContext h, ICurriculumService s, CancellationToken ct) =>
            (await s.ApplyTemplate(classId, body, key, Actor(h), ct)).ToHttpResult())
            .WithSummary("Copy published template topics into a class; requires Idempotency-Key");
        group.MapPost("/classes/{classId:guid}/topics/complete", async (Guid classId, CreateFullTopicInput body, HttpContext h, ISender sender, CancellationToken ct) =>
            (await sender.Send(new CreateFullTopicCommand(classId, body, Actor(h)), ct)).ToHttpResult())
            .WithSummary("Atomically create topic, lecture blocks, exercises and optional publication");
        group.MapGet("/classes/{classId:guid}/available-problems", async (Guid classId, [FromQuery] string? search, [FromQuery] string? difficulty,
            [FromQuery] int? page, [FromQuery] int? pageSize, HttpContext h, ICurriculumService s, CancellationToken ct) =>
            (await s.AvailableProblems(classId, search, difficulty, page ?? 1, pageSize ?? 20, Actor(h), ct)).ToHttpResult());
    }
}
