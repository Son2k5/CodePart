using CodePath.Application.Ai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
namespace CodePath.Infrastructure.Ai;

public static class AiRegistration
{
    public static IServiceCollection AddAiAssistant(this IServiceCollection services, IConfiguration config)
    {
        var key = config["Ai:ServiceKey"] ?? "";
        var secret = config["Ai:AnonymizationKey"] ?? "";
        if (key.Length < 32 || secret.Length < 32) throw new InvalidOperationException("AI service and anonymization keys require at least 32 characters.");
        services.AddScoped<IAiService, AiService>();
        services.AddHttpClient<IAiAssistantClient, PythonAiServiceClient>(http => {
            http.BaseAddress = new Uri(config["Ai:BaseUrl"] ?? "http://ai-service:8000/");
            http.DefaultRequestHeaders.Add("X-Service-Key", key);
            http.Timeout = TimeSpan.FromSeconds(20);
        }).AddPolicyHandler(Policy<HttpResponseMessage>.Handle<HttpRequestException>()
            .OrResult(r => (int)r.StatusCode >= 500).CircuitBreakerAsync(3, TimeSpan.FromSeconds(30)))
          .AddPolicyHandler(Policy<HttpResponseMessage>.Handle<HttpRequestException>()
            .WaitAndRetryAsync(1, _ => TimeSpan.FromMilliseconds(200)));
        return services;
    }
}
