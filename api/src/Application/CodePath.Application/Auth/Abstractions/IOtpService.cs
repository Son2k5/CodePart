namespace CodePath.Application.Auth.Abstractions;

public interface IOtpService
{
    Task<(bool Issued, string? Otp)> TryIssueOtpAsync(string email);
    Task InvalidateOtpAsync(string email, string otp);
    Task<(bool IsValid, string? ErrorMessage)> VerifyOtpAsync(string email, string otp);
}
