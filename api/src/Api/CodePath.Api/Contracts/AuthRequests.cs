namespace CodePath.Api.Contracts;

public sealed record RegisterRequest(string FullName, string Email, string Password);
public sealed record VerifyOtpRequest(string Email, string Otp);
public sealed record ResendOtpRequest(string Email);
public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record LogoutRequest(string? RefreshToken);
