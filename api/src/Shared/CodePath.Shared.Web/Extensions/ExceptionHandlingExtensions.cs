using CodePath.Shared.Web.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CodePath.Shared.Web.Extensions;

public static class ExceptionHandlingExtensions
{
    public static IServiceCollection AddAppExceptionHandling(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        return services;
    }

    public static IApplicationBuilder UseAppExceptionHandling(this IApplicationBuilder app)
    {
        app.UseExceptionHandler();
        return app;
    }
}
