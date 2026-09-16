using CodePath.Shared.Web.Contracts;
using CodePath.Shared.Web.Extensions;
using DotNetEnv;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

IModule[] modules = [];

foreach (var module in modules)
{
    module.RegisterModule(builder.Services, builder.Configuration);
}

builder.Services.AddCodePathSwagger();

var app = builder.Build();

app.UseAppExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "CodePath API v1");
    });
}

app.UseHttpsRedirection();

app.MapGet("/", () => Results.Ok(new { service = "CodePath API", status = "healthy" }))
   .ExcludeFromDescription();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
   .ExcludeFromDescription();

foreach (var module in modules)
{
    module.MapEndpoints(app);
}

app.Run();

public partial class Program { }
