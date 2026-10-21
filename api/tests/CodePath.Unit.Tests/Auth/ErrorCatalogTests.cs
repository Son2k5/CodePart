using CodePath.Shared.Kernel.Common;
using Xunit;

namespace CodePath.Unit.Tests.Auth;

public class ErrorCatalogTests
{
    [Theory]
    [InlineData(ErrorCodes.AuthInvalidCredentials, "Email hoặc mật khẩu không chính xác.")]
    [InlineData(ErrorCodes.AuthSessionExpired, "Phiên đăng nhập của bạn đã hết hạn. Vui lòng đăng nhập lại.")]
    [InlineData(ErrorCodes.AuthOtpInvalid, "Mã xác thực OTP không chính xác hoặc đã hết hạn.")]
    [InlineData(ErrorCodes.ValidationError, "Một hoặc nhiều trường dữ liệu không vượt qua kiểm tra tính hợp lệ.")]
    [InlineData(ErrorCodes.NotFound, "Tài nguyên được yêu cầu không tồn tại hoặc đã bị xóa.")]
    [InlineData(ErrorCodes.Conflict, "Dữ liệu đã tồn tại hoặc có xung đột với trạng thái hiện tại.")]
    [InlineData(ErrorCodes.TooManyRequests, "Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau.")]
    [InlineData(ErrorCodes.InternalServerError, "Đã có lỗi xảy ra từ hệ thống máy chủ. Vui lòng liên hệ quản trị viên để được hỗ trợ.")]
    public void GetSafeMessage_ShouldReturnStandardUserFriendlyMessage(string code, string expected)
    {
        var message = ErrorCatalog.GetSafeMessage(code);
        Assert.Equal(expected, message);
    }

    [Fact]
    public void SanitizeMessage_ShouldReplaceTechnicalDatabaseKeywords()
    {
        var technicalMsg = "Exception thrown: Npgsql.PostgresException: 23505: duplicate key value violates unique constraint 'users_email_key'";
        var sanitized = ErrorCatalog.SanitizeMessage(technicalMsg);

        Assert.Equal("Đã có lỗi xảy ra trong quá trình xử lý hệ thống. Vui lòng liên hệ quản trị viên.", sanitized);
    }

    [Fact]
    public void SanitizeMessage_ShouldReplaceStackTraceAndSystemPaths()
    {
        var technicalMsg = "at CodePath.Infrastructure.Data.AppDbContext.SaveChangesAsync() in /app/src/Infrastructure/AppDbContext.cs:line 120";
        var sanitized = ErrorCatalog.SanitizeMessage(technicalMsg);

        Assert.Equal("Đã có lỗi xảy ra trong quá trình xử lý hệ thống. Vui lòng liên hệ quản trị viên.", sanitized);
    }

    [Fact]
    public void SanitizeMessage_ShouldKeepSafeFriendlyMessage()
    {
        var friendly = "Mã bài tập không tồn tại hoặc đã bị ẩn.";
        var result = ErrorCatalog.SanitizeMessage(friendly);

        Assert.Equal(friendly, result);
    }
}
