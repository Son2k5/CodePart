using CodePath.Shared.Kernel.Common;
using CodePath.Shared.Kernel.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CodePath.Shared.Web.Exceptions;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
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

        if (exception is BadHttpRequestException or JsonException)
        {
            logger.LogWarning(
                "Lỗi cú pháp yêu cầu HTTP hoặc định dạng JSON. Path: {Path}, TraceId: {TraceId}, Message: {Message}",
                httpContext.Request.Path,
                traceId,
                exception.Message);

            var badRequestProblem = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Yêu cầu không hợp lệ",
                Detail = "Dữ liệu yêu cầu không hợp lệ hoặc sai định dạng JSON.",
                Type = "https://httpstatuses.com/400",
                Instance = httpContext.Request.Path
            };

            badRequestProblem.Extensions["code"] = ErrorCodes.BadRequest;
            badRequestProblem.Extensions["traceId"] = traceId;

            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            httpContext.Response.ContentType = "application/problem+json";
            await httpContext.Response.WriteAsJsonAsync(badRequestProblem, cancellationToken);
            return true;
        }

        if (exception is DbUpdateConcurrencyException)
        {
            logger.LogWarning(
                "Xung đột phiên bản dữ liệu (Concurrency). Path: {Path}, TraceId: {TraceId}",
                httpContext.Request.Path,
                traceId);

            var conflictProblem = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Xung đột dữ liệu",
                Detail = "Dữ liệu đã bị thay đổi bởi thao tác khác. Vui lòng tải lại trang.",
                Type = "https://httpstatuses.com/409",
                Instance = httpContext.Request.Path
            };

            conflictProblem.Extensions["code"] = ErrorCodes.Conflict;
            conflictProblem.Extensions["traceId"] = traceId;

            httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
            httpContext.Response.ContentType = "application/problem+json";
            await httpContext.Response.WriteAsJsonAsync(conflictProblem, cancellationToken);
            return true;
        }

        if (exception is DbUpdateException dbUpdateException)
        {
            logger.LogError(
                dbUpdateException,
                "Lỗi cơ sở dữ liệu xảy ra: {Message}. Path: {Path}, TraceId: {TraceId}",
                dbUpdateException.Message,
                httpContext.Request.Path,
                traceId);

            var isDuplicate = dbUpdateException.InnerException?.Message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase) == true
                || dbUpdateException.InnerException?.Message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) == true
                || dbUpdateException.InnerException?.Message.Contains("23505", StringComparison.OrdinalIgnoreCase) == true;

            var (statusCode, title, detail, code) = isDuplicate
                ? (StatusCodes.Status409Conflict, "Xung đột dữ liệu", "Dữ liệu đã tồn tại trong hệ thống.", ErrorCodes.Conflict)
                : (StatusCodes.Status500InternalServerError, "Đã có lỗi xảy ra ở máy chủ", "Đã có lỗi xảy ra từ hệ thống máy chủ. Vui lòng liên hệ quản trị viên với mã traceId để được hỗ trợ.", ErrorCodes.InternalServerError);

            var dbProblem = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Type = $"https://httpstatuses.com/{statusCode}",
                Instance = httpContext.Request.Path
            };

            dbProblem.Extensions["code"] = code;
            dbProblem.Extensions["traceId"] = traceId;

            httpContext.Response.StatusCode = statusCode;
            httpContext.Response.ContentType = "application/problem+json";
            await httpContext.Response.WriteAsJsonAsync(dbProblem, cancellationToken);
            return true;
        }

        if (exception is AppException appException)
        {
            var (statusCode, title, safeMessage) = appException switch
            {
                NotFoundException => (StatusCodes.Status404NotFound, "Không tìm thấy dữ liệu", ErrorCatalog.GetSafeMessage(appException.ErrorCode, appException.Message)),
                ConflictException => (StatusCodes.Status409Conflict, "Xung đột dữ liệu", ErrorCatalog.GetSafeMessage(appException.ErrorCode, appException.Message)),
                UnauthorizedAppException => (StatusCodes.Status401Unauthorized, "Không được phép truy cập", ErrorCatalog.GetSafeMessage(appException.ErrorCode, appException.Message)),
                ForbiddenException => (StatusCodes.Status403Forbidden, "Truy cập bị từ chối", ErrorCatalog.GetSafeMessage(appException.ErrorCode, appException.Message)),
                _ => ErrorCatalog.GetDetails(appException.ErrorCode, appException.Message)
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
                Detail = safeMessage,
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
            Detail = "Đã có lỗi xảy ra từ hệ thống máy chủ. Vui lòng liên hệ quản trị viên với mã traceId để được hỗ trợ.",
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
