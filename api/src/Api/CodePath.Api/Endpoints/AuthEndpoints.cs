using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CodePath.Api.Contracts;
using CodePath.Application.Auth.Commands;
using CodePath.Application.Auth.Queries;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

namespace CodePath.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterRequest req, ISender sender) =>
        {
            var result = await sender.Send(new RegisterCommand(req.FullName, req.Email, req.Password));
            return result.IsSuccess
                ? Results.Ok(new { message = result.Value })
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("Register")
        .RequireRateLimiting("auth-rate-limit");

        group.MapPost("/verify-otp", async (VerifyOtpRequest req, ISender sender) =>
        {
            var result = await sender.Send(new VerifyOtpCommand(req.Email, req.Otp));
            return result.IsSuccess
                ? Results.Ok(new { message = result.Value })
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("VerifyOtp")
        .RequireRateLimiting("auth-rate-limit");

        group.MapPost("/resend-otp", async (ResendOtpRequest req, ISender sender) =>
        {
            var result = await sender.Send(new ResendOtpCommand(req.Email));
            return result.IsSuccess
                ? Results.Ok(new { message = result.Value })
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("ResendOtp")
        .RequireRateLimiting("auth-rate-limit");

        group.MapPost("/login", async (LoginRequest req, HttpContext httpContext, ISender sender, IConfiguration config) =>
        {
            var result = await sender.Send(new LoginCommand(req.Email, req.Password));
            if (result.IsSuccess)
            {
                var isDev = string.Equals(config["ASPNETCORE_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase);
                httpContext.Response.Cookies.Append("refreshToken", result.Value!.RefreshToken, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = !isDev,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.AddDays(7)
                });

                return Results.Ok(result.Value);
            }

            return result.ErrorCode switch
            {
                "ACCOUNT_PENDING" or "ACCOUNT_REJECTED" or "ACCOUNT_DISABLED" =>
                    Results.Json(new { error = result.Error, code = result.ErrorCode }, statusCode: StatusCodes.Status403Forbidden),
                "UNAUTHORIZED" =>
                    Results.Json(new { error = result.Error, code = "UNAUTHORIZED" }, statusCode: StatusCodes.Status401Unauthorized),
                _ =>
                    Results.BadRequest(new { error = result.Error, code = result.ErrorCode ?? "BAD_REQUEST" })
            };
        })
        .WithName("Login")
        .RequireRateLimiting("login-rate-limit");

        group.MapPost("/refresh", async (RefreshTokenRequest? req, HttpContext httpContext, ISender sender, IConfiguration config) =>
        {
            var tokenStr = req?.RefreshToken;
            if (string.IsNullOrWhiteSpace(tokenStr))
            {
                tokenStr = httpContext.Request.Cookies["refreshToken"];
            }

            if (string.IsNullOrWhiteSpace(tokenStr))
            {
                return Results.Json(new { error = "Refresh token không được để trống.", code = "UNAUTHORIZED" }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var result = await sender.Send(new RefreshTokenCommand(tokenStr));
            if (result.IsSuccess)
            {
                var isDev = string.Equals(config["ASPNETCORE_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase);
                httpContext.Response.Cookies.Append("refreshToken", result.Value!.RefreshToken, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = !isDev,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.AddDays(7)
                });

                return Results.Ok(result.Value);
            }

            return Results.Json(new { error = result.Error, code = result.ErrorCode ?? "UNAUTHORIZED" }, statusCode: StatusCodes.Status401Unauthorized);
        })
        .WithName("RefreshToken")
        .RequireRateLimiting("auth-rate-limit");

        group.MapPost("/logout", async (LogoutRequest? req, HttpContext httpContext, ISender sender, IConfiguration config) =>
        {
            var jti = httpContext.User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value ?? "";
            var expClaim = httpContext.User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;

            var expiryMinutes = int.TryParse(config["Jwt:AccessTokenExpiryMinutes"], out var mins) ? mins : 15;
            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(expiryMinutes);

            if (long.TryParse(expClaim, out var expSeconds))
            {
                expiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime;
            }

            var tokenStr = req?.RefreshToken ?? httpContext.Request.Cookies["refreshToken"];

            var sub = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                   ?? httpContext.User.FindFirst("sub")?.Value;
            Guid.TryParse(sub, out var currentUserId);

            await sender.Send(new LogoutCommand(tokenStr, jti, expiresAtUtc, currentUserId));

            httpContext.Response.Cookies.Delete("refreshToken");

            return Results.Ok(new { message = "Đăng xuất thành công." });
        })
        .WithName("Logout")
        .RequireAuthorization("ActiveUser");

        group.MapGet("/me", async (HttpContext httpContext, ISender sender) =>
        {
            var sub = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                   ?? httpContext.User.FindFirst("sub")?.Value;

            if (!Guid.TryParse(sub, out var userId))
                return Results.Unauthorized();

            var result = await sender.Send(new GetCurrentUserQuery(userId));
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.NotFound(new { error = result.Error });
        })
        .WithName("GetCurrentUser")
        .RequireAuthorization("ActiveUser");

        return app;
    }
}
