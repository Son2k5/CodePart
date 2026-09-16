using CodePath.Shared.Kernel.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

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

                var (statusCode, title) = exception switch
                {
                    ValidationException => (StatusCodes.Status400BadRequest, "Du lieu khong hop le"),
                    NotFoundException => (StatusCodes.Status404NotFound, "Khong tim thay du lieu"),
                    ConflictException => (StatusCodes.Status409Conflict, "Xung dot du lieu"),
                    UnauthorizedAppException => (StatusCodes.Status401Unauthorized, "Khong duoc phep"),
                    AppException => (StatusCodes.Status400BadRequest, "Yeu cau khong hop le"),
                    _ => (StatusCodes.Status500InternalServerError, "Da co loi xay ra o may chu")
                };

                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "application/problem+json";

                var problem = new
                {
                    status = statusCode,
                    title,
                    detail = exception is ValidationException validationException
                        ? string.Join("; ", validationException.Errors.Select(e => e.ErrorMessage))
                        : exception?.Message,
                    traceId = context.TraceIdentifier
                };

                await context.Response.WriteAsJsonAsync(problem);
            });
        });

        return app;
    }
}
