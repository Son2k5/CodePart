using CodePath.Shared.Kernel.Common;
using CodePath.Shared.Kernel.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodePath.Shared.Web.Exceptions;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment env) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = httpContext.TraceIdentifier;

        if (exception is ValidationException validationException)
        {
            logger.LogWarning(
                "Yêu cầu không hợp lệ do lỗi dữ liệu đầu vào. Path: {Path}, TraceId: {TraceId}",
                httpContext.Request.Path,
                traceId);

            var errors = validationException.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            var validationProblem = new HttpValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Dữ liệu không hợp lệ.",
                Detail = "Một hoặc nhiều trường dữ liệu không vượt qua kiểm tra tính hợp lệ.",
                Type = "https://httpstatuses.com/400",
                Instance = httpContext.Request.Path
            };

            validationProblem.Extensions["code"] = ErrorCodes.ValidationError;
            validationProblem.Extensions["traceId"] = traceId;

            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            httpContext.Response.ContentType = "application/problem+json";
            await httpContext.Response.WriteAsJsonAsync(validationProblem, cancellationToken);
            return true;
        }

        if (exception is AppException appException)
        {
            var (statusCode, title) = appException switch
            {
                NotFoundException => (StatusCodes.Status404NotFound, "Không tìm thấy dữ liệu"),
                ConflictException => (StatusCodes.Status409Conflict, "Xung đột dữ liệu"),
                UnauthorizedAppException => (StatusCodes.Status401Unauthorized, "Không được phép truy cập"),
                ForbiddenException => (StatusCodes.Status403Forbidden, "Truy cập bị từ chối"),
                _ => appException.ErrorCode switch
                {
                    ErrorCodes.NotFound => (StatusCodes.Status404NotFound, "Không tìm thấy dữ liệu"),
                    ErrorCodes.Conflict => (StatusCodes.Status409Conflict, "Xung đột dữ liệu"),
                    ErrorCodes.Unauthorized => (StatusCodes.Status401Unauthorized, "Không được phép truy cập"),
                    ErrorCodes.Forbidden or ErrorCodes.AccountPending or ErrorCodes.AccountRejected or ErrorCodes.AccountDisabled
                        => (StatusCodes.Status403Forbidden, "Truy cập bị từ chối"),
                    ErrorCodes.InfraError => (StatusCodes.Status503ServiceUnavailable, "Dịch vụ hạ tầng tạm thời gián đoạn"),
                    _ => (StatusCodes.Status400BadRequest, "Yêu cầu không hợp lệ")
                }
            };

            logger.LogWarning(
                "Ngoại lệ nghiệp vụ xảy ra ({ErrorCode}): {Message}. Path: {Path}, TraceId: {TraceId}",
                appException.ErrorCode,
                appException.Message,
                httpContext.Request.Path,
                traceId);

            var problem = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = appException.Message,
                Type = $"https://httpstatuses.com/{statusCode}",
                Instance = httpContext.Request.Path
            };

            problem.Extensions["code"] = appException.ErrorCode;
            problem.Extensions["traceId"] = traceId;

            httpContext.Response.StatusCode = statusCode;
            httpContext.Response.ContentType = "application/problem+json";
            await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
            return true;
        }

        logger.LogError(
            exception,
            "Ngoại lệ hệ thống chưa xử lý xảy ra: {Message}. Path: {Path}, TraceId: {TraceId}",
            exception.Message,
            httpContext.Request.Path,
            traceId);

        var systemProblem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Đã có lỗi xảy ra ở máy chủ",
            Detail = env.IsDevelopment()
                ? exception.ToString()
                : "Đã có lỗi xảy ra từ hệ thống máy chủ. Vui lòng liên hệ quản trị viên với mã traceId để được hỗ trợ.",
            Type = "https://httpstatuses.com/500",
            Instance = httpContext.Request.Path
        };

        systemProblem.Extensions["code"] = ErrorCodes.InternalServerError;
        systemProblem.Extensions["traceId"] = traceId;

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(systemProblem, cancellationToken);
        return true;
    }
}
