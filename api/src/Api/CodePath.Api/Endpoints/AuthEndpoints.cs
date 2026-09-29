using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CodePath.Api.Contracts;
using CodePath.Api.Extensions;
using CodePath.Application.Auth.Commands;
using CodePath.Application.Auth.Queries;
using CodePath.Shared.Kernel.Common;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

namespace CodePath.Api.Endpoints;

public static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserId(this System.Security.Claims.ClaimsPrincipal user)
    {
        var sub = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var userId) ? userId : null;
    }

    public static string GetJti(this System.Security.Claims.ClaimsPrincipal user)
        => user.FindFirst(JwtRegisteredClaimNames.Jti)?.Value ?? "";

    public static DateTime GetTokenExpiryOrDefault(this System.Security.Claims.ClaimsPrincipal user, IConfiguration config)
    {
        var expiryMinutes = int.TryParse(config["Jwt:AccessTokenExpiryMinutes"], out var mins) ? mins : 15;
        var expClaim = user.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
        if (long.TryParse(expClaim, out var expSeconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime;
        }
        return DateTime.UtcNow.AddMinutes(expiryMinutes);
    }
}

public static class AuthEndpoints
{
    private const string RefreshTokenCookieName = "refreshToken";

    private static void AppendRefreshTokenCookie(HttpContext httpContext, string refreshToken, IConfiguration config)
    {
        var isDev = string.Equals(config["ASPNETCORE_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase);
        httpContext.Response.Cookies.Append(RefreshTokenCookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = !isDev,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });
    }

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterRequest req, ISender sender) =>
        {
            var result = await sender.Send(new RegisterCommand(req.FullName, req.Email, req.Password));
            return result.ToHttpResult();
        })
        .WithName("Register")
        .RequireRateLimiting("auth-rate-limit");

        group.MapPost("/verify-otp", async (VerifyOtpRequest req, ISender sender) =>
        {
            var result = await sender.Send(new VerifyOtpCommand(req.Email, req.Otp));
            return result.ToHttpResult();
        })
        .WithName("VerifyOtp")
        .RequireRateLimiting("auth-rate-limit");

        group.MapPost("/resend-otp", async (ResendOtpRequest req, ISender sender) =>
        {
            var result = await sender.Send(new ResendOtpCommand(req.Email));
            return result.ToHttpResult();
        })
        .WithName("ResendOtp")
        .RequireRateLimiting("auth-rate-limit");

        group.MapPost("/login", async (LoginRequest req, HttpContext httpContext, ISender sender, IConfiguration config) =>
        {
            var result = await sender.Send(new LoginCommand(req.Email, req.Password));
            if (result.IsSuccess)
            {
                AppendRefreshTokenCookie(httpContext, result.Value!.RefreshToken, config);
            }

            return result.ToHttpResult();
        })
        .WithName("Login")
        .RequireRateLimiting("login-rate-limit");

        group.MapPost("/refresh", async (RefreshTokenRequest? req, HttpContext httpContext, ISender sender, IConfiguration config) =>
        {
            var tokenStr = req?.RefreshToken;
            if (string.IsNullOrWhiteSpace(tokenStr))
            {
                tokenStr = httpContext.Request.Cookies[RefreshTokenCookieName];
            }

            if (string.IsNullOrWhiteSpace(tokenStr))
            {
                return Results.Problem(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Không được phép truy cập",
                    Detail = "Refresh token không được để trống.",
                    Type = "https://httpstatuses.com/401",
                    Extensions = { ["code"] = ErrorCodes.Unauthorized }
                });
            }

            var result = await sender.Send(new RefreshTokenCommand(tokenStr));
            if (result.IsSuccess)
            {
                AppendRefreshTokenCookie(httpContext, result.Value!.RefreshToken, config);
            }

            return result.ToHttpResult();
        })
        .WithName("RefreshToken")
        .RequireRateLimiting("auth-rate-limit");

        group.MapPost("/logout", async (LogoutRequest? req, HttpContext httpContext, ISender sender, IConfiguration config) =>
        {
            var jti = httpContext.User.GetJti();
            var expiresAtUtc = httpContext.User.GetTokenExpiryOrDefault(config);

            var tokenStr = req?.RefreshToken ?? httpContext.Request.Cookies[RefreshTokenCookieName];

            var currentUserId = httpContext.User.GetUserId() ?? Guid.Empty;

            await sender.Send(new LogoutCommand(tokenStr, jti, expiresAtUtc, currentUserId));

            httpContext.Response.Cookies.Delete(RefreshTokenCookieName);

            return Results.Ok(new { message = "Đăng xuất thành công." });
        })
        .WithName("Logout")
        .RequireAuthorization("ActiveUser");

        group.MapGet("/me", async (HttpContext httpContext, ISender sender) =>
        {
            var userId = httpContext.User.GetUserId();

            if (userId is null)
                return Results.Unauthorized();

            var result = await sender.Send(new GetCurrentUserQuery(userId.Value));
            return result.ToHttpResult();
        })
        .WithName("GetCurrentUser")
        .RequireAuthorization("ActiveUser");

        return app;
    }
}
