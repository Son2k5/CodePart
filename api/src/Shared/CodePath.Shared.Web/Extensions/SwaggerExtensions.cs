using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

namespace CodePath.Shared.Web.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddCodePathSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "CodePath API",
                Version = "v1",
                Description = "Nen tang hoc va luyen lap trinh truc tuyen (Modular Monolith)."
            });

            var jwtScheme = new OpenApiSecurityScheme
            {
                Scheme = "bearer",
                BearerFormat = "JWT",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Description = "Nhap access token dang: Bearer {token}",
                Reference = new OpenApiReference
                {
                    Id = JwtBearerDefaults,
                    Type = ReferenceType.SecurityScheme
                }
            };

            options.AddSecurityDefinition(JwtBearerDefaults, jwtScheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                { jwtScheme, Array.Empty<string>() }
            });
        });

        return services;
    }

    private const string JwtBearerDefaults = "Bearer";
}
