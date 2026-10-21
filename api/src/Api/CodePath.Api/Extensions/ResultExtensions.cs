using System.Diagnostics;
using CodePath.Shared.Kernel.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CodePath.Api.Extensions;

public static class ResultExtensions
{
    private static string ResolveTraceId()
    {
        return Activity.Current?.TraceId.ToString()
            ?? Activity.Current?.Id
            ?? (new HttpContextAccessor().HttpContext?.TraceIdentifier)
            ?? Guid.NewGuid().ToString("N");
    }

    public static Microsoft.AspNetCore.Http.IResult ToHttpResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            if (result.Value is null)
                return Results.Ok();

            if (result.Value is string msg)
                return Results.Ok(new { message = msg });

            return Results.Ok(result.Value);
        }

        var traceId = ResolveTraceId();

        if (result.ErrorCode is ErrorCodes.SessionIdleTimeout or ErrorCodes.SessionExpired or "SESSION_IDLE_TIMEOUT" or "SESSION_EXPIRED")
        {
            var sessionProblem = new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Phiên đăng nhập đã hết hạn",
                Detail = "Phiên làm việc của bạn đã hết hạn. Vui lòng đăng nhập lại.",
                Type = "https://httpstatuses.com/401"
            };
            sessionProblem.Extensions["code"] = result.ErrorCode;
            sessionProblem.Extensions["traceId"] = traceId;
            return Results.Problem(sessionProblem);
        }

        if (result.ErrorCode == ErrorCodes.ValidationError && result.ValidationErrors.Count > 0)
        {
            var errorsDict = result.ValidationErrors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            var validationProblem = new HttpValidationProblemDetails(errorsDict)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Dữ liệu không hợp lệ.",
                Detail = "Một hoặc nhiều trường dữ liệu không vượt qua kiểm tra tính hợp lệ.",
                Type = "https://httpstatuses.com/400"
            };
            validationProblem.Extensions["code"] = ErrorCodes.ValidationError;
            validationProblem.Extensions["traceId"] = traceId;

            return Results.Problem(validationProblem);
        }

        var (statusCode, title, safeMessage) = ErrorCatalog.GetDetails(result.ErrorCode, result.Error);

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = safeMessage,
            Type = $"https://httpstatuses.com/{statusCode}"
        };

        problem.Extensions["code"] = result.ErrorCode ?? ErrorCodes.BadRequest;
        problem.Extensions["traceId"] = traceId;

        return Results.Problem(problem);
    }
}
