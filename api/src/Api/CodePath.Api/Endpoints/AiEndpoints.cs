using CodePath.Api.Extensions;
using CodePath.Application.Ai;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
namespace CodePath.Api.Endpoints;

public sealed class AiHub : Hub { }
public sealed record AiFeedback(bool Helpful);
public static class AiEndpoints
{
    public static void MapAiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ai").WithTags("AI Assistant").RequireAuthorization("ActiveStudent");
        group.MapPost("/assist", async (AssistInput input, HttpContext http, IAiService service, IHubContext<AiHub> hub, CancellationToken ct) => {
            try {
                var id = http.User.GetUserId()!.Value;
                var result = await service.Assist(id, input, ct);
                await hub.Clients.User(id.ToString()).SendAsync("AiHint", result, ct);
                if (http.Request.Headers.Accept.Any(v => v?.Contains("text/event-stream") == true))
                {
                    http.Response.Headers.CacheControl = "no-cache";
                    http.Response.Headers["X-Accel-Buffering"] = "no";
                    // Buffer generation until guard passes. Never stream unchecked LLM tokens.
                    return Results.Stream(async stream => {
                        await using var writer = new StreamWriter(stream, leaveOpen: true);
                        foreach (var chunk in result.Hint.Message.EnumerateRunes().Chunk(80))
                        {
                            await writer.WriteAsync($"event: delta\ndata: {JsonSerializer.Serialize(string.Concat(chunk.Select(rune => rune.ToString())))}\n\n");
                            await writer.FlushAsync(ct);
                        }
                        await writer.WriteAsync($"event: result\ndata: {JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web))}\n\n");
                        await writer.FlushAsync(ct);
                    }, contentType: "text/event-stream");
                }
                return Results.Ok(result);
            }
            catch (AiPolicyException ex) { return Results.Json(new { code = ex.Code }, statusCode: ex.Status); }
        });
        group.MapPost("/{id:guid}/feedback", async (Guid id, AiFeedback feedback, HttpContext http, IAiService service, CancellationToken ct) =>
            await service.Feedback(http.User.GetUserId()!.Value, id, feedback.Helpful, ct) ? Results.NoContent() : Results.NotFound());
        app.MapHub<AiHub>("/api/ai/hub").RequireAuthorization("ActiveStudent");
    }
}
