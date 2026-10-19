using CodePath.Application.Admin.Commands;
using CodePath.Shared.Kernel.Behaviors;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CodePath.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            config.AddOpenBehavior(typeof(LoggingBehavior<,>));
            config.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<AdminStatusChangeService>();
        services.AddSingleton<CodePath.Application.Classes.JoinCodeGenerator>();
        services.AddScoped<CodePath.Application.Auth.Services.SessionActivityService>();

        return services;
    }
}
