using CodePath.Application.Judge;
using CodePath.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CodePath.Infrastructure.Judge;

public static class JudgeRegistration
{
    public static IServiceCollection AddCodeJudge(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<JudgeOptions>().Bind(config.GetSection("Judge"))
            .Validate(o => o.Concurrency is >= 1 and <= 16 && o.JobTimeoutSeconds is >= 60 and <= 3600 && o.ExecutionTimeoutSeconds is >= 10 and <= 120)
            .Validate(o => !o.Enabled || o.ApiToken.Length >= 32, "Judge API token must have at least 32 characters.").ValidateOnStart();
        // Use the application's existing MySQL database and connection/pool settings.
        services.AddDbContext<JudgeDbContext>((sp, o) => JudgeDbContext.ConfigureMySql(o,
            sp.GetRequiredService<AppDbContext>().Database.GetConnectionString()
                ?? throw new InvalidOperationException("Application MySQL connection is missing.")));
        services.AddScoped<IJudgeService, JudgeService>(); services.AddScoped<IJudgeAccess, JudgeAccess>(); services.AddSingleton<JudgeQueue>();
        services.AddHttpClient<ICodeExecutor, Judge0CodeExecutor>((sp, http) => {
            var o = sp.GetRequiredService<IOptions<JudgeOptions>>().Value;
            http.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/') + "/");
            http.DefaultRequestHeaders.Add("X-Auth-Token", o.ApiToken); http.Timeout = TimeSpan.FromSeconds(o.ExecutionTimeoutSeconds);
        });
        services.AddHostedService<JudgeWorker>();
        return services;
    }
}
