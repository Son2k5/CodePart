using CodePath.Application.Learning;
using CodePath.Infrastructure.Judge;
using CodePath.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace CodePath.Infrastructure.Learning;
public static class LearningRegistration
{
    public static IServiceCollection AddLearning(this IServiceCollection services, IConfiguration config)
    {
        if (!config.GetValue<bool>("Judge:Enabled")) services.AddDbContext<JudgeDbContext>((sp, o) => JudgeDbContext.ConfigureMySql(o, sp.GetRequiredService<AppDbContext>().Database.GetConnectionString()!));
        services.AddScoped<ILearningService, LearningService>(); services.AddScoped<ContentValidator>();
        services.AddScoped<ILearningRagAccess, LearningService>();
        services.AddScoped<IRagService, RagService>();
        services.AddHttpClient("rag-indexer", http => {
            http.BaseAddress = new Uri(config["Rag:IndexBaseUrl"] ?? config["Ai:BaseUrl"] ?? "http://ai-service:8000/");
            http.DefaultRequestHeaders.Add("X-Service-Key", config["Ai:ServiceKey"] ?? "");
            http.Timeout = TimeSpan.FromSeconds(250);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddHttpClient<IRagClient, PythonRagClient>(http => {
            http.BaseAddress = new Uri(config["Ai:BaseUrl"] ?? "http://ai-service:8000/");
            http.DefaultRequestHeaders.Add("X-Service-Key", config["Ai:ServiceKey"] ?? "");
            http.Timeout = TimeSpan.FromSeconds(250);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        if (config.GetValue<bool>("Rag:Enabled"))
        {
            if ((config["Ai:ServiceKey"] ?? "").Length < 32) throw new InvalidOperationException("RAG requires Ai:ServiceKey with at least 32 characters.");
            services.AddHostedService<RagIndexWorker>();
        }
        services.AddScoped<ILearningExtras, LearningService>();
        services.AddScoped<ICurriculumService, LearningService>();
        services.AddScoped<ILearningAssets, LearningAssets>();
        services.AddHttpClient<IFileStorage, CloudinaryFileStorage>().ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<INotificationHandler<SubmissionJudgedEvent>, LearningJudgeHandler>();
        services.AddScoped<ITopicExerciseDraftGenerator, NullTopicExerciseDraftGenerator>();
        services.AddHostedService<LearningJudgeRelay>();
        return services;
    }
}
public sealed class LearningJudgeHandler(ILearningService service) : INotificationHandler<SubmissionJudgedEvent>
{
    public Task Handle(SubmissionJudgedEvent notification, CancellationToken ct) => service.Judged(notification, ct);
}
// Durable at-least-once delivery. Completion is committed before marking the source processed.
public sealed class LearningJudgeRelay(IServiceScopeFactory scopes, TimeProvider clock, ILogger<LearningJudgeRelay> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<JudgeDbContext>();
                var rows = await db.Submissions.Where(s => s.TopicId != null && s.FinishedAt != null && s.LearningProcessedAt == null).OrderBy(s => s.FinishedAt).Take(100).ToListAsync(ct);
                foreach (var s in rows)
                {
                    await scope.ServiceProvider.GetRequiredService<IPublisher>().Publish(new SubmissionJudgedEvent(s.Id, s.UserId, s.TopicId!.Value), ct);
                    s.LearningProcessedAt = clock.GetUtcNow().UtcDateTime; await db.SaveChangesAsync(ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception e) { logger.LogError(e, "Learning judge events will be retried."); }
            await Task.Delay(TimeSpan.FromSeconds(5), clock, ct);
        }
    }
}
