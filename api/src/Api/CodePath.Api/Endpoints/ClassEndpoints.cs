using CodePath.Api.Extensions;
using CodePath.Application.Classes;
using CodePath.Domain.Users.Entities;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CodePath.Api.Endpoints;

public static class ClassEndpoints
{
    public sealed record StatusBody(ClassStatus Status);
    public sealed record TeachersBody(Guid[] TeacherIds);
    public sealed record CodeBody(DateTime? ExpiresAt);
    public sealed record JoinBody(string Code);
    private static Guid Actor(HttpContext ctx) => ctx.User.GetUserId()!.Value;
    public static IEndpointRouteBuilder MapClassEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/classes").RequireAuthorization("AdminOnly")
            .RequireAuthorization(policy => policy.RequireRole("Admin")).WithTags("Admin Classes");
        admin.MapPost("/thumbnail", async (HttpRequest request, IWebHostEnvironment env, CancellationToken ct) => {
            const int limit = 5 * 1024 * 1024;
            if (request.ContentLength > limit) return Results.BadRequest(new { message = "Ảnh tối đa 5 MB." });
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int count;
            while ((count = await request.Body.ReadAsync(chunk, ct)) > 0) {
                if (buffer.Length + count > limit) return Results.BadRequest(new { message = "Ảnh tối đa 5 MB." });
                await buffer.WriteAsync(chunk.AsMemory(0, count), ct);
            }
            var bytes = buffer.ToArray();
            var extension = bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ? ".png"
                : bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 ? ".jpg"
                : bytes.Length >= 12 && System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP" ? ".webp" : null;
            if (extension == null) return Results.BadRequest(new { message = "Chỉ hỗ trợ ảnh PNG, JPEG hoặc WebP." });
            var directory = Path.Combine(env.ContentRootPath, "App_Data", "class-images");
            Directory.CreateDirectory(directory);
            var filename = Guid.NewGuid().ToString("N") + extension;
            await File.WriteAllBytesAsync(Path.Combine(directory, filename), bytes, ct);
            return Results.Ok(new { url = "/api/class-images/" + filename });
        });
        app.MapGet("/api/class-images/{filename}", (string filename, IWebHostEnvironment env) => {
            if (!System.Text.RegularExpressions.Regex.IsMatch(filename, @"\A[a-f0-9]{32}\.(png|jpg|webp)\z")) return Results.NotFound();
            var path = Path.Combine(env.ContentRootPath, "App_Data", "class-images", filename);
            if (!File.Exists(path)) return Results.NotFound();
            return Results.File(path, Path.GetExtension(filename) == ".jpg" ? "image/jpeg" : "image/" + Path.GetExtension(filename)[1..]);
        }).AllowAnonymous();
        admin.MapGet("", async ([AsParameters] ClassFilter f, ISender sender, CancellationToken ct) =>
            (await sender.Send(new ListClassesQuery(f), ct)).ToHttpResult());
        admin.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            (await sender.Send(new ClassDetailQuery(id), ct)).ToHttpResult());
        admin.MapPost("", async (ClassInput body, HttpContext ctx, ISender sender, CancellationToken ct) => {
            var result = await sender.Send(new SaveClassCommand(Actor(ctx), null, body), ct);
            return result.IsSuccess ? Results.Created($"/api/admin/classes/{result.Value}", new { id = result.Value }) : result.ToHttpResult();
        });
        admin.MapPut("/{id:guid}", async (Guid id, ClassInput body, HttpContext ctx, ISender sender, CancellationToken ct) =>
            (await sender.Send(new SaveClassCommand(Actor(ctx), id, body), ct)).ToHttpResult());
        admin.MapDelete("/{id:guid}", async (Guid id, [FromQuery] bool? force, [FromQuery] bool? confirm, HttpContext ctx, ISender sender, CancellationToken ct) =>
            (await sender.Send(new ChangeClassCommand(Actor(ctx), id, ClassAction.Delete, Force: force == true, Confirm: confirm == true), ct)).ToHttpResult());
        admin.MapPatch("/{id:guid}/status", async (Guid id, StatusBody body, HttpContext ctx, ISender sender, CancellationToken ct) =>
            (await sender.Send(new ChangeClassCommand(Actor(ctx), id, ClassAction.Status, Status: body.Status), ct)).ToHttpResult());
        admin.MapPost("/{id:guid}/teachers", async (Guid id, TeachersBody body, HttpContext ctx, ISender sender, CancellationToken ct) =>
            (await sender.Send(new ChangeClassCommand(Actor(ctx), id, ClassAction.AddTeachers, TeacherIds: body.TeacherIds), ct)).ToHttpResult());
        admin.MapDelete("/{id:guid}/teachers/{teacherId:guid}", async (Guid id, Guid teacherId, HttpContext ctx, ISender sender, CancellationToken ct) =>
            (await sender.Send(new ChangeClassCommand(Actor(ctx), id, ClassAction.RemoveTeacher, Subject: teacherId), ct)).ToHttpResult());
        admin.MapPost("/{id:guid}/join-code/regenerate", async (Guid id, CodeBody body, HttpContext ctx, ISender sender, CancellationToken ct) =>
            (await sender.Send(new ChangeClassCommand(Actor(ctx), id, ClassAction.RegenerateCode, ExpiresAt: body.ExpiresAt), ct)).ToHttpResult());
        admin.MapDelete("/{id:guid}/join-code", async (Guid id, HttpContext ctx, ISender sender, CancellationToken ct) =>
            (await sender.Send(new ChangeClassCommand(Actor(ctx), id, ClassAction.RevokeCode), ct)).ToHttpResult());
        admin.MapGet("/{id:guid}/students", async (Guid id, [AsParameters] ClassFilter f, ISender sender, CancellationToken ct) =>
            (await sender.Send(new ClassStudentsQuery(id, f), ct)).ToHttpResult());
        admin.MapDelete("/{id:guid}/students/{studentId:guid}", async (Guid id, Guid studentId, HttpContext ctx, ISender sender, CancellationToken ct) =>
            (await sender.Send(new ChangeClassCommand(Actor(ctx), id, ClassAction.RemoveStudent, Subject: studentId), ct)).ToHttpResult());
        var student = app.MapGroup("/api/classes").RequireAuthorization("ActiveStudent").WithTags("Student Classes");
        student.MapGet("/public", async ([AsParameters] ClassFilter f, HttpContext ctx, ISender sender, CancellationToken ct) =>
            (await sender.Send(new PublicClassesQuery(f, Actor(ctx)), ct)).ToHttpResult());
        student.MapPost("/{id:guid}/join", async (Guid id, HttpContext ctx, ISender sender, CancellationToken ct) =>
            (await sender.Send(new JoinClassCommand(Actor(ctx), id, null), ct)).ToHttpResult());
        student.MapPost("/join-by-code", async (JoinBody body, HttpContext ctx, ISender sender, CancellationToken ct) =>
            (await sender.Send(new JoinClassCommand(Actor(ctx), null, body.Code), ct)).ToHttpResult()).RequireRateLimiting("class-join-code");
        student.MapPost("/{id:guid}/leave", async (Guid id, HttpContext ctx, ISender sender, CancellationToken ct) =>
            (await sender.Send(new LeaveClassCommand(Actor(ctx), id), ct)).ToHttpResult());
        return app;
    }
}
