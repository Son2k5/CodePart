using System.Diagnostics;
using CodePath.Shared.Kernel.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CodePath.Shared.Kernel.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation("Bắt đầu xử lý {RequestName}", requestName);

        try
        {
            var response = await next();
            stopwatch.Stop();

            if (response is Result<object> or IValidationResult)
            {
                dynamic result = response;
                if (!result.IsSuccess)
                {
                    logger.LogWarning(
                        "Kết thúc {RequestName} với kết quả thất bại. Mã lỗi: {ErrorCode}, Thông báo: {Error} ({ElapsedMilliseconds}ms)",
                        requestName,
                        (string?)result.ErrorCode ?? "UNKNOWN",
                        (string?)result.Error ?? "None",
                        stopwatch.ElapsedMilliseconds);

                    return response;
                }
            }

            logger.LogInformation(
                "Hoàn thành xử lý {RequestName} ({ElapsedMilliseconds}ms)",
                requestName,
                stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(
                ex,
                "Lỗi ngoại lệ chưa xử lý xảy ra trong {RequestName} ({ElapsedMilliseconds}ms)",
                requestName,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
    }
}
