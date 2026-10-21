import axios from 'axios';

export const ERROR_MESSAGES: Record<string, string> = {
  // Authentication & Session
  BAD_REQUEST: 'Yêu cầu không hợp lệ hoặc dữ liệu không đúng định dạng.',
  UNAUTHORIZED: 'Bạn cần đăng nhập để thực hiện thao tác này.',
  FORBIDDEN: 'Bạn không có quyền thực hiện thao tác này.',
  NOT_FOUND: 'Không tìm thấy dữ liệu yêu cầu.',
  CONFLICT: 'Dữ liệu đã tồn tại hoặc xảy ra xung đột với trạng thái hiện tại.',
  VALIDATION_ERROR: 'Dữ liệu đầu vào không hợp lệ. Vui lòng kiểm tra lại các trường.',
  AUTH_INVALID_CREDENTIALS: 'Email hoặc mật khẩu không chính xác.',
  AUTH_SESSION_EXPIRED: 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.',
  AUTH_OTP_INVALID: 'Mã xác thực OTP không chính xác hoặc đã hết hạn.',
  UNVERIFIED_EMAIL: 'Email chưa được xác thực. Vui lòng xác thực tài khoản.',
  ACCOUNT_PENDING: 'Tài khoản Giảng viên đang chờ Quản trị viên cấp quyền.',
  ACCOUNT_REJECTED: 'Yêu cầu kích hoạt tài khoản đã bị từ chối.',
  ACCOUNT_DISABLED: 'Tài khoản của bạn hiện đang bị vô hiệu hóa.',
  SESSION_IDLE_TIMEOUT: 'Phiên đăng nhập đã hết hạn do không hoạt động.',
  SESSION_EXPIRED: 'Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại.',
  TOO_MANY_REQUESTS: 'Bạn đã thao tác quá nhanh hoặc gửi quá nhiều yêu cầu. Vui lòng đợi trong giây lát.',
  RATE_LIMIT_EXCEEDED: 'Đã đạt giới hạn lượt gọi. Vui lòng thử lại sau một phút.',
  RATE_LIMITED: 'Thao tác quá nhanh. Vui lòng đợi trong giây lát.',
  INFRASTRUCTURE_ERROR: 'Dịch vụ hạ tầng tạm thời gián đoạn. Vui lòng thử lại sau.',
  INTERNAL_SERVER_ERROR: 'Đã có lỗi xảy ra từ máy chủ.',
  SERVICE_UNAVAILABLE: 'Dịch vụ tạm thời chưa sẵn sàng. Vui lòng thử lại sau.',

  // Classes & Curriculum
  TOPIC_LOCKED: 'Bài học này chưa được mở khóa.',
  NOT_ENROLLED: 'Bạn chưa được ghi danh vào lớp học phần này.',
  CLASS_NOT_FOUND: 'Không tìm thấy lớp học phần hoặc lớp đã bị xóa.',
  ENROLLMENT_NOT_FOUND: 'Học sinh không có trong danh sách lớp học.',
  CLASS_FULL: 'Lớp học phần đã đủ số lượng học sinh tối đa.',
  ALREADY_ENROLLED: 'Bạn đã tham gia lớp học phần này trước đó.',
  CLASS_HAS_STUDENTS: 'Không thể xóa lớp do lớp đang có học sinh.',
  CLASS_CONCURRENCY_CONFLICT: 'Thông tin lớp học đã bị thay đổi bởi thao tác khác. Vui lòng tải lại trang.',
  INVALID_JOIN_CODE: 'Mã tham gia lớp học không chính xác hoặc đã hết hạn.',
  CONTENT_INVALID: 'Nội dung hoặc tài liệu gửi lên không đúng định dạng.',
  FILE_TOO_LARGE: 'Kích thước tệp tin vượt quá giới hạn cho phép.',
  ASSET_NOT_READY: 'Tài liệu đang được hệ thống xử lý, vui lòng thử lại sau ít phút.',
  FEATURE_NOT_ENABLED: 'Tính năng này hiện chưa được kích hoạt trên hệ thống.',

  // Online Judge
  SUBMISSION_IN_PROGRESS: 'Bài nộp trước của bạn vẫn đang được chấm. Vui lòng đợi hoàn thành.',
  COMPILE_ERROR: 'Chương trình chưa biên dịch thành công. Vui lòng kiểm tra lại mã nguồn.',
  RUNTIME_ERROR: 'Chương trình gặp lỗi khi thực thi (Runtime Error).',
  TIME_LIMIT_EXCEEDED: 'Chương trình chạy quá giới hạn thời gian (Time Limit Exceeded).',
  MEMORY_LIMIT_EXCEEDED: 'Chương trình vượt quá dung lượng bộ nhớ cho phép (Memory Limit Exceeded).',
  JUDGE_EXECUTION_ERROR: 'Máy chủ chấm bài gặp sự cố khi chạy mã nguồn. Vui lòng thử lại sau.',
  JUDGE_UNAVAILABLE: 'Dịch vụ chấm code chưa sẵn sàng cho ngôn ngữ này.',
  JUDGE_TIMEOUT: 'Kiểm chứng vượt thời gian cho phép. Hãy kiểm tra dịch vụ chấm và thử lại.',

  // AI Assistant & RAG
  AI_UNAVAILABLE: 'Hệ thống trợ giảng AI tạm thời gián đoạn. Vui lòng thử lại sau.',
  RAG_UNAVAILABLE: 'Hệ thống tra cứu học liệu tạm thời gián đoạn. Vui lòng thử lại sau.',
  RAG_DISABLED: 'Tính năng trợ giảng theo học liệu chưa được bật.',
  CONTEXT_CHANGED: 'Học liệu đã được cập nhật lại. Vui lòng gửi lại câu hỏi.',
  INSUFFICIENT_CONTEXT: 'Chưa có đủ thông tin từ học liệu để trả lời câu hỏi này.',
  DRAFT_GENERATION_FAILED: 'Không thể tạo bản nháp học liệu. Vui lòng thử lại.',
  INVALID_DRAFT: 'Bản nháp chưa hợp lệ. Bài lập trình cần 10–15 test, trong đó 1–2 sample; quiz cần 3–5 câu hỏi với bốn lựa chọn.',
  VERIFY_REQUIRED: 'Cần kiểm chứng lời giải và test trước khi duyệt.',
  LEVEL_LOCKED: 'Cấp độ gợi ý này hiện chưa mở khóa. Hãy tiếp tục giải bài để mở khóa.',
  RATE_LIMIT: 'Bạn đã dùng hết lượt trong khoảng thời gian này. Hãy thử lại sau.'
};

export interface NormalizedApiError {
  code?: string;
  traceId?: string;
  status?: number;
  message: string;
}

export function extractErrorDetails(error: unknown): NormalizedApiError {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as Record<string, any> | undefined;
    const status = error.response?.status;
    const code = data?.code as string | undefined;
    const traceId = (data?.traceId || error.response?.headers?.['x-correlation-id']) as string | undefined;

    // Check if validation errors object exists: { errors: { field: ["msg"] } }
    if (data?.errors && typeof data.errors === 'object') {
      const firstError = Object.values(data.errors).flat()[0];
      if (typeof firstError === 'string' && firstError.length > 0 && !isTechnicalMessage(firstError)) {
        return { code: code || 'VALIDATION_ERROR', traceId, status, message: firstError };
      }
    }

    if (code && ERROR_MESSAGES[code]) {
      return { code, traceId, status, message: ERROR_MESSAGES[code] };
    }

    // If server sent title and it's non-technical, user-friendly text
    if (data?.title && typeof data.title === 'string' && !isTechnicalMessage(data.title)) {
      return { code, traceId, status, message: data.title };
    }

    if (status === 429) {
      return { code: 'RATE_LIMIT_EXCEEDED', traceId, status, message: ERROR_MESSAGES.RATE_LIMIT_EXCEEDED };
    }

    if (status === 502 || status === 503 || status === 504) {
      return { code: 'SERVICE_UNAVAILABLE', traceId, status, message: ERROR_MESSAGES.SERVICE_UNAVAILABLE };
    }

    const fallbackMsg = traceId
      ? `Đã có lỗi xảy ra. Vui lòng thử lại sau (Mã lỗi: ${traceId}).`
      : 'Đã có lỗi xảy ra từ máy chủ. Vui lòng thử lại sau.';

    return { code, traceId, status, message: fallbackMsg };
  }

  return {
    message: 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra lại kết nối mạng.'
  };
}

export function getErrorMessage(error: unknown, fallbackMessage?: string): string {
  const details = extractErrorDetails(error);
  if (details.message) {
    return details.message;
  }
  return fallbackMessage || 'Đã có lỗi xảy ra. Vui lòng thử lại sau.';
}

function isTechnicalMessage(text: string): boolean {
  const pattern = /(exception|stacktrace|\bat\b\s+[a-zA-Z0-9_\.]+\(|npgsql|mysql|sqlite|sqlclient|foreign\s+key|unique\s+constraint|duplicate\s+entry|table\s+'|column\s+'|connectionstring|password=|secret|jwt|bearer|\/app\/|[a-zA-Z]:\\)/i;
  return pattern.test(text);
}
