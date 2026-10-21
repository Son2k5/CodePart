using System.Text.RegularExpressions;

namespace CodePath.Shared.Kernel.Common;

public static class ErrorCatalog
{
    private static readonly Regex TechnicalPattern = new(
        @"(exception|stacktrace|\bat\b\s+[a-zA-Z0-9_\.]+\(|npgsql|mysql|sqlite|sqlclient|syntax\s+error|foreign\s+key|unique\s+constraint|duplicate\s+entry|table\s+'|column\s+'|connectionstring|password=|secret|jwt|bearer|\/app\/|[a-zA-Z]:\\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Dictionary<string, (int StatusCode, string Title, string SafeMessage)> Catalog = new(StringComparer.OrdinalIgnoreCase)
    {
        [ErrorCodes.BadRequest] = (400, "Yêu cầu không hợp lệ", "Dữ liệu yêu cầu không hợp lệ hoặc sai định dạng."),
        [ErrorCodes.ValidationError] = (400, "Dữ liệu không hợp lệ", "Một hoặc nhiều trường dữ liệu không vượt qua kiểm tra tính hợp lệ."),
        [ErrorCodes.Unauthorized] = (401, "Không được phép truy cập", "Bạn cần đăng nhập để thực hiện thao tác này."),
        [ErrorCodes.AuthInvalidCredentials] = (401, "Thông tin đăng nhập không hợp lệ", "Email hoặc mật khẩu không chính xác."),
        [ErrorCodes.AuthSessionExpired] = (401, "Phiên đăng nhập đã hết hạn", "Phiên đăng nhập của bạn đã hết hạn. Vui lòng đăng nhập lại."),
        [ErrorCodes.SessionIdleTimeout] = (401, "Phiên hết hạn do không hoạt động", "Phiên đăng nhập đã hết hạn do không hoạt động."),
        [ErrorCodes.SessionExpired] = (401, "Phiên đăng nhập đã hết hạn", "Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại."),
        [ErrorCodes.AuthOtpInvalid] = (400, "Mã xác thực không hợp lệ", "Mã xác thực OTP không chính xác hoặc đã hết hạn."),
        [ErrorCodes.Forbidden] = (403, "Truy cập bị từ chối", "Bạn không có quyền thực hiện thao tác này."),
        [ErrorCodes.AccountUnverified] = (403, "Email chưa được xác thực", "Tài khoản chưa được xác thực email. Vui lòng xác thực trước khi tiếp tục."),
        [ErrorCodes.AccountPending] = (403, "Tài khoản đang chờ duyệt", "Tài khoản đang chờ quản trị viên phê duyệt."),
        [ErrorCodes.AccountRejected] = (403, "Tài khoản bị từ chối", "Yêu cầu tài khoản của bạn đã bị từ chối."),
        [ErrorCodes.AccountDisabled] = (403, "Tài khoản bị vô hiệu hóa", "Tài khoản của bạn hiện đang bị vô hiệu hóa."),
        [ErrorCodes.NotFound] = (404, "Không tìm thấy dữ liệu", "Tài nguyên được yêu cầu không tồn tại hoặc đã bị xóa."),
        [ErrorCodes.Conflict] = (409, "Xung đột dữ liệu", "Dữ liệu đã tồn tại hoặc có xung đột với trạng thái hiện tại."),
        [ErrorCodes.TooManyRequests] = (429, "Quá nhiều yêu cầu", "Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau."),
        [ErrorCodes.RateLimitExceeded] = (429, "Vượt quá giới hạn lượt gọi", "Bạn đã đạt giới hạn yêu cầu. Vui lòng thử lại sau."),
        [ErrorCodes.InfraError] = (503, "Dịch vụ tạm thời gián đoạn", "Hệ thống hạ tầng tạm thời gián đoạn. Vui lòng thử lại sau ít phút."),
        [ErrorCodes.InternalServerError] = (500, "Đã có lỗi xảy ra ở máy chủ", "Đã có lỗi xảy ra từ hệ thống máy chủ. Vui lòng liên hệ quản trị viên để được hỗ trợ."),
        [ErrorCodes.ServiceUnavailable] = (503, "Dịch vụ chưa sẵn sàng", "Dịch vụ hiện tại không khả dụng. Vui lòng thử lại sau."),

        // Learning & Classes
        [ErrorCodes.TopicLocked] = (403, "Bài học chưa mở khóa", "Bài học này hiện chưa đến thời gian mở khóa hoặc chưa đủ điều kiện."),
        [ErrorCodes.NotEnrolled] = (403, "Chưa tham gia lớp học", "Bạn cần tham gia lớp học phần để truy cập nội dung này."),
        [ErrorCodes.ClassNotFound] = (404, "Không tìm thấy lớp học", "Lớp học phần không tồn tại hoặc bạn không có quyền truy cập."),
        [ErrorCodes.EnrollmentNotFound] = (404, "Không tìm thấy thông tin ghi danh", "Học sinh không có trong danh sách lớp học phần này."),
        [ErrorCodes.ClassFull] = (409, "Lớp đã đủ sĩ số", "Lớp học phần đã đạt số lượng học sinh tối đa."),
        [ErrorCodes.AlreadyEnrolled] = (409, "Đã tham gia lớp học", "Học sinh đã được ghi danh trong lớp học phần này."),
        [ErrorCodes.ClassHasStudents] = (409, "Lớp đang có học sinh", "Không thể thực hiện thao tác do lớp học phần đã có học sinh tham gia."),
        [ErrorCodes.ClassConcurrencyConflict] = (409, "Dữ liệu lớp học đã thay đổi", "Thông tin lớp học đã được cập nhật bởi thao tác khác. Vui lòng tải lại."),
        [ErrorCodes.InvalidJoinCode] = (400, "Mã tham gia không hợp lệ", "Mã tham gia lớp học không chính xác hoặc đã hết hạn."),
        [ErrorCodes.ContentInvalid] = (400, "Nội dung không hợp lệ", "Nội dung học liệu hoặc dữ liệu gửi lên không đúng định dạng quy định."),
        [ErrorCodes.FileTooLarge] = (400, "Tệp tin quá dung lượng", "Dung lượng tệp tin tải lên vượt quá giới hạn cho phép."),
        [ErrorCodes.AssetNotReady] = (400, "Tệp tin chưa sẵn sàng", "Tài nguyên học liệu đang trong quá trình xử lý. Vui lòng thử lại sau."),
        [ErrorCodes.FeatureNotEnabled] = (503, "Tính năng chưa được bật", "Tính năng này hiện chưa được kích hoạt trên hệ thống."),

        // Judge & Sandbox
        [ErrorCodes.SubmissionInProgress] = (409, "Bài nộp đang được chấm", "Một bài nộp khác đang trong quá trình chạy. Vui lòng chờ kết quả."),
        [ErrorCodes.CompileError] = (400, "Lỗi biên dịch", "Chương trình không thể biên dịch thành công."),
        [ErrorCodes.RuntimeError] = (400, "Lỗi khi thực thi", "Chương trình phát sinh lỗi trong quá trình thực thi."),
        [ErrorCodes.TimeLimitExceeded] = (400, "Quá thời gian chạy", "Chương trình vượt quá giới hạn thời gian cho phép."),
        [ErrorCodes.MemoryLimitExceeded] = (400, "Quá giới hạn bộ nhớ", "Chương trình vượt quá giới hạn bộ nhớ cho phép."),

        // AI & RAG
        [ErrorCodes.AiUnavailable] = (503, "Trợ giảng AI tạm thời gián đoạn", "Trợ giảng AI hiện đang quá tải hoặc tạm thời gián đoạn. Vui lòng thử lại sau."),
        [ErrorCodes.RagUnavailable] = (503, "Tìm kiếm học liệu gián đoạn", "Hệ thống tra cứu học liệu tạm thời gián đoạn. Vui lòng thử lại sau."),
        [ErrorCodes.RagDisabled] = (503, "Trợ giảng học liệu chưa được bật", "Tính năng trợ giảng theo học liệu chưa được kích hoạt."),
        [ErrorCodes.ContextChanged] = (409, "Học liệu đã được cập nhật", "Học liệu tham chiếu đã thay đổi trong quá trình xử lý. Vui lòng gửi lại câu hỏi.")
    };

    public static (int StatusCode, string Title, string SafeMessage) GetDetails(string? errorCode, string? rawMessage = null)
    {
        if (!string.IsNullOrWhiteSpace(errorCode) && Catalog.TryGetValue(errorCode, out var entry))
        {
            return entry;
        }

        var safeDetail = SanitizeMessage(rawMessage) ?? "Đã có lỗi xảy ra trong quá trình xử lý yêu cầu.";
        return (400, "Yêu cầu không hợp lệ", safeDetail);
    }

    public static string GetSafeMessage(string? errorCode, string? rawMessage = null)
    {
        if (!string.IsNullOrWhiteSpace(errorCode) && Catalog.TryGetValue(errorCode, out var entry))
        {
            return entry.SafeMessage;
        }

        return SanitizeMessage(rawMessage) ?? "Đã có lỗi xảy ra. Vui lòng thử lại sau.";
    }

    public static string GetSafeTitle(int statusCode, string? defaultTitle = null)
    {
        if (!string.IsNullOrWhiteSpace(defaultTitle) && !TechnicalPattern.IsMatch(defaultTitle))
        {
            return defaultTitle;
        }

        return statusCode switch
        {
            400 => "Yêu cầu không hợp lệ",
            401 => "Không được phép truy cập",
            403 => "Truy cập bị từ chối",
            404 => "Không tìm thấy dữ liệu",
            409 => "Xung đột dữ liệu",
            422 => "Dữ liệu không thể xử lý",
            429 => "Quá nhiều yêu cầu",
            503 => "Dịch vụ tạm thời gián đoạn",
            _ => "Đã có lỗi xảy ra ở máy chủ"
        };
    }

    public static string? SanitizeMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;

        if (TechnicalPattern.IsMatch(message))
            return "Đã có lỗi xảy ra trong quá trình xử lý hệ thống. Vui lòng liên hệ quản trị viên.";

        return message.Trim();
    }
}
