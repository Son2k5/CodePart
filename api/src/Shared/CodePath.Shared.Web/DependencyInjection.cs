using CodePath.Shared.Web.Extensions;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    /// <summary>
    /// Điểm tập hợp duy nhất để đăng ký toàn bộ hạ tầng dùng chung của Shared.Web vào DI Container.
    /// Shared.Web KHÔNG owns Redis connection (Infra concern) — chỉ Swagger + ExceptionHandling helpers.
    /// </summary>
    public static IServiceCollection AddSharedInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCodePathSwagger();
        services.AddAppExceptionHandling();

        return services;
    }
}
