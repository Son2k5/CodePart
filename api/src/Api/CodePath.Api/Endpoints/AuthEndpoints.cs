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

    private static void AppendRefreshTokenCookie(
        HttpContext httpContext,
        string refreshToken,
        DateTime expiresAtUtc,
        IConfiguration config)
    {
        httpContext.Response.Cookies.Append(RefreshTokenCookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = GetCookieSameSite(config),
            Path = "/api/auth",
            Expires = new DateTimeOffset(DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc))
        });
    }

    private static void DeleteRefreshTokenCookie(HttpContext httpContext, IConfiguration config)
        => httpContext.Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = GetCookieSameSite(config),
            Path = "/api/auth"
        });

    private static SameSiteMode GetCookieSameSite(IConfiguration config)
        => Enum.TryParse<SameSiteMode>(config["AuthSession:CookieSameSite"], true, out var sameSite)
            ? sameSite
            : SameSiteMode.Strict;

    private static bool IsCookieRequestOriginAllowed(HttpContext httpContext, IConfiguration config)
    {
        if (GetCookieSameSite(config) != SameSiteMode.None
            || !httpContext.Request.Headers.TryGetValue("Origin", out var origin))
        {
            return true;
        }

        var allowedOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        return allowedOrigins.Contains(origin.ToString(), StringComparer.OrdinalIgnoreCase);
    }

    private static string? GetClientIp(HttpContext httpContext)
        => httpContext.Connection.RemoteIpAddress?.ToString();

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
            var result = await sender.Send(new LoginCommand(req.Email, req.Password, GetClientIp(httpContext)));
            if (result.IsSuccess)
            {
                AppendRefreshTokenCookie(
                    httpContext,
                    result.Value!.RefreshToken,
                    result.Value.RefreshTokenExpiresAt,
                    config);
            }

            return result.ToHttpResult();
        })
        .WithName("Login")
        .RequireRateLimiting("login-rate-limit");

        group.MapPost("/refresh", async (HttpContext httpContext, ISender sender, IConfiguration config) =>
        {
            if (!IsCookieRequestOriginAllowed(httpContext, config))
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, detail: "The request origin is not allowed.");
            }

            var tokenStr = httpContext.Request.Cookies[RefreshTokenCookieName];
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

            var result = await sender.Send(new RefreshTokenCommand(tokenStr, GetClientIp(httpContext)));
            if (result.IsSuccess)
            {
                AppendRefreshTokenCookie(
                    httpContext,
                    result.Value!.RefreshToken,
                    result.Value.RefreshTokenExpiresAt,
                    config);
            }
            else
            {
                DeleteRefreshTokenCookie(httpContext, config);
            }

            return result.ToHttpResult();
        })
        .WithName("RefreshToken")
        .RequireRateLimiting("auth-rate-limit");

        group.MapPost("/logout", async (HttpContext httpContext, ISender sender, IConfiguration config) =>
        {
            if (!IsCookieRequestOriginAllowed(httpContext, config))
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, detail: "The request origin is not allowed.");
            }

            var jti = httpContext.User.GetJti();
            var expiresAtUtc = httpContext.User.GetTokenExpiryOrDefault(config);
            var tokenStr = httpContext.Request.Cookies[RefreshTokenCookieName];

            await sender.Send(new LogoutCommand(
                tokenStr,
                jti,
                expiresAtUtc,
                GetClientIp(httpContext)));

            DeleteRefreshTokenCookie(httpContext, config);

            return Results.Ok(new { message = "Đăng xuất thành công." });
        })
        .WithName("Logout");

        group.MapPost("/logout-all", async (HttpContext httpContext, ISender sender, IConfiguration config) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            await sender.Send(new LogoutAllCommand(
                userId.Value,
                httpContext.User.GetJti(),
                httpContext.User.GetTokenExpiryOrDefault(config),
                GetClientIp(httpContext)));

            DeleteRefreshTokenCookie(httpContext, config);
            return Results.Ok(new { message = "All sessions have been signed out." });
        })
        .WithName("LogoutAll")
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
