namespace CodePath.Application.Auth.Abstractions;

public interface IOtpService
{
    Task<(bool Success, string? ErrorMessage)> CanRequestOtpAsync(string email);
    Task<string> GenerateAndStoreOtpAsync(string email);
    Task<(bool IsValid, string? ErrorMessage)> VerifyOtpAsync(string email, string otp);
}
