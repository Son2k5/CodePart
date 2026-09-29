using CodePath.Shared.Kernel.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CodePath.Api.Extensions;

public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            if (result.Value is null)
                return Results.Ok();

            if (result.Value is string msg)
                return Results.Ok(new { message = msg });

            return Results.Ok(result.Value);
        }

        if (result.ErrorCode == ErrorCodes.ValidationError && result.ValidationErrors.Count > 0)
        {
            var errorsDict = result.ValidationErrors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            return Results.ValidationProblem(
                errorsDict,
                title: "Dữ liệu không hợp lệ.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var (statusCode, title) = result.ErrorCode switch
        {
            ErrorCodes.NotFound => (StatusCodes.Status404NotFound, "Không tìm thấy dữ liệu"),
            ErrorCodes.Unauthorized => (StatusCodes.Status401Unauthorized, "Không được phép truy cập"),
            ErrorCodes.Forbidden or ErrorCodes.AccountPending or ErrorCodes.AccountRejected or ErrorCodes.AccountDisabled 
                => (StatusCodes.Status403Forbidden, "Truy cập bị từ chối"),
            ErrorCodes.Conflict => (StatusCodes.Status409Conflict, "Xung đột dữ liệu"),
            ErrorCodes.InfraError => (StatusCodes.Status503ServiceUnavailable, "Dịch vụ hạ tầng tạm thời gián đoạn"),
            _ => (StatusCodes.Status400BadRequest, "Yêu cầu không hợp lệ")
        };

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = result.Error,
            Type = $"https://httpstatuses.com/{statusCode}"
        };

        problem.Extensions["code"] = result.ErrorCode ?? ErrorCodes.BadRequest;

        return Results.Problem(problem);
    }
}
