using CodePath.Shared.Kernel.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CodePath.Shared.Web.Extensions;

public static class ExceptionHandlingExtensions
{
    public static WebApplication UseAppExceptionHandling(this WebApplication app)
    {
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var feature = context.Features.Get<IExceptionHandlerFeature>();
                var exception = feature?.Error;
                var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("CodePath.Exceptions");

                if (exception is not null)
                {
                    logger.LogError(exception, "Unhandled exception occurred. TraceId: {TraceId}", context.TraceIdentifier);
                }

                var (statusCode, title, errorCode) = exception switch
                {
                    ValidationException => (StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ", "VALIDATION_ERROR"),
                    NotFoundException => (StatusCodes.Status404NotFound, "Không tìm thấy dữ liệu", "NOT_FOUND"),
                    ConflictException => (StatusCodes.Status409Conflict, "Xung đột dữ liệu", "CONFLICT"),
                    ForbiddenException fb => (StatusCodes.Status403Forbidden, "Truy cập bị từ chối", fb.ErrorCode),
                    UnauthorizedAppException => (StatusCodes.Status401Unauthorized, "Không được phép", "UNAUTHORIZED"),
                    AppException => (StatusCodes.Status400BadRequest, "Yêu cầu không hợp lệ", "BAD_REQUEST"),
                    _ => (StatusCodes.Status500InternalServerError, "Đã có lỗi xảy ra ở máy chủ", "INTERNAL_SERVER_ERROR")
                };

                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "application/problem+json";

                var problem = new
                {
                    status = statusCode,
                    code = errorCode,
                    title,
                    detail = exception switch
                    {
                        ValidationException v => string.Join("; ", v.Errors.Select(e => e.ErrorMessage)),
                        AppException appEx => appEx.Message,
                        _ => "Đã có lỗi xảy ra từ hệ thống máy chủ. Vui lòng liên hệ quản trị viên với mã traceId để được hỗ trợ."
                    },
                    traceId = context.TraceIdentifier
                };

                await context.Response.WriteAsJsonAsync(problem);
            });
        });

        return app;
    }
}
