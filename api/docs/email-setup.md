# Gửi email xác thực và khôi phục mật khẩu

Backend dùng MailKit/SMTP cho mã OTP, liên kết đặt lại mật khẩu và thông báo đổi mật khẩu.
Development gửi email thật khi SMTP được cấu hình; Testing luôn bỏ qua gửi thư.
Nếu thiếu cấu hình, dịch vụ báo lỗi vào log, không giả lập gửi thành công.
API quên mật khẩu/gửi lại OTP vẫn trả thông báo trung lập để không tiết lộ tài khoản tồn tại.

## Cấu hình Gmail khi chạy local

Trong tài khoản Google dùng để gửi thư, bật xác minh hai bước và tạo App Password
(nếu chính sách tài khoản cho phép). Dùng App Password, không dùng mật khẩu đăng nhập Google.
Thêm các biến sau vào `api/.env`, thay các giá trị mẫu bằng thông tin thật:

```dotenv
Email__Smtp__Host=smtp.gmail.com
Email__Smtp__Port=587
Email__Smtp__Username=your-sender@gmail.com
Email__Smtp__Password=YOUR_GOOGLE_APP_PASSWORD
Email__Smtp__FromAddress=your-sender@gmail.com
Email__Smtp__FromName=CodePart
Email__Smtp__SocketOptions=StartTls
AuthFrontend__PublicBaseUrl=https://localhost:5173
```

Không commit mật khẩu vào Git hoặc gửi mật khẩu qua chat. Khởi động lại API sau khi cấu hình.
Nếu dùng tên miền/frontend khác, đổi `AuthFrontend__PublicBaseUrl` thành URL thực tế.
Production dùng biến môi trường hoặc secrets của môi trường triển khai.

## Kiểm tra luồng

1. Đăng ký bằng email HANU hợp lệ mà bạn truy cập được; mở thư OTP trong hộp thư đó.
2. Nhập mã 6 chữ số trong 10 phút; chức năng gửi lại mã có thời gian chờ 60 giây.
3. Với tài khoản đã xác thực email, chọn quên mật khẩu và mở liên kết nhận trong thư trong 30 phút.
4. Đổi mật khẩu; kiểm tra email thông báo đổi mật khẩu và đăng nhập bằng mật khẩu mới.

Gmail trong cấu hình là **tài khoản gửi**. Người nhận vẫn là email tài khoản đã đăng ký;
quy tắc đăng ký hiện tại chỉ chấp nhận `@hanu.edu.vn`, không chấp nhận Gmail cá nhân.
Nếu không nhận thư, kiểm tra Spam và log API: `Authentication email delivered`,
`email delivery failed` hoặc `Email SMTP is not configured`.
SMTP chấp nhận thư chưa đảm bảo thư được đưa vào Inbox.

Tài liệu Google: [SMTP Gmail](https://support.google.com/mail/answer/7104828?hl=en),
[App Passwords](https://support.google.com/accounts/answer/185833).
