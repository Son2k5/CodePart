using CodePath.Shared.Web.Extensions;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    /// <summary>
    /// Điểm tập hợp duy nhất để đăng ký toàn bộ hạ tầng dùng chung của Shared.Web vào DI Container
    /// </summary>
    public static IServiceCollection AddSharedInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddRedis(configuration);
        services.AddCodePathSwagger();

        return services;
    }
}
