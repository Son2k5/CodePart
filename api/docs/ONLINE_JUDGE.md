# CodePath Online Judge Backend

## Phạm vi đã triển khai

Module này là phần backend nền tảng cho trải nghiệm làm bài kiểu LeetCode:

1. Học sinh mở đề đã publish tại `GET /api/exercises/{slug}`. API chỉ trả test mẫu.
2. `POST /api/exercises/{id}/run` chạy test mẫu hoặc một `customInput`, không tạo submission.
3. `POST /api/exercises/{id}/submit` chạy toàn bộ test, bao gồm test ẩn, rồi lưu submission và kết quả từng test.
4. Core API không biên dịch hoặc chạy code. `ICodeExecutionService` gọi Judge0 bằng HTTP.
5. Migration `ExerciseOnlineJudge` tạo các bảng `exercises`, `exercise_test_cases`, `submissions` và `submission_test_results`.
6. Seeder tạo bài mẫu có slug `two-sum`.

## Luồng hệ thống

```text
Frontend / API client
       |
       | Run / Submit + JWT
       v
ASP.NET Core API -- đọc test case --> PostgreSQL
       |
       | source + stdin + resource limits
       v
Judge0 (mạng/container riêng)
       |
       | status + stdout + runtime + memory
       v
ASP.NET Core API -- lưu khi Submit --> PostgreSQL
```

## Cấu hình local

1. Chạy PostgreSQL và Redis như cấu hình hiện tại của dự án.
2. Triển khai Judge0 ở một service/container riêng theo hướng dẫn self-host của Judge0. Không đặt Judge0 public trên Internet.
3. Thêm cấu hình vào `.env`:

```dotenv
CodeExecution__Judge0__BaseUrl=http://localhost:2358
CodeExecution__Judge0__RequestTimeoutSeconds=20
# Nếu gateway Judge0 yêu cầu token:
# CodeExecution__Judge0__AuthenticationHeader=X-Auth-Token
# CodeExecution__Judge0__AuthenticationToken=replace-me
```

4. Chạy API; migration và bài mẫu được áp dụng tự động.
5. Đăng nhập bằng tài khoản học sinh đang hoạt động và gọi các endpoint trong nhóm `/api/exercises`.

Các endpoint Run/Submit yêu cầu JWT của người dùng có policy `ActiveStudent`. Endpoint đọc đề được public để trang có thể hiển thị trước khi đăng nhập.

## Hợp đồng code bài mẫu

Chương trình dùng chuẩn input/output để tương thích nhiều ngôn ngữ:

```text
Input
4 9
2 7 11 15

Output
0 1
```

Judge0 language IDs hiện được ánh xạ trong `Judge0CodeExecutionService`: Python 3, JavaScript, Java, C# và C++.

## Bảo mật và giới hạn

- Không bao giờ chạy `Process.Start`, shell, compiler hoặc code học sinh trong Core API.
- Test ẩn không được serialize ra response; với test ẩn, response submit cũng che input, expected output và actual output.
- Source code tối đa 100 KB; custom input tối đa 20 KB.
- Mỗi bài có CPU time và memory limit; API có rate limit theo user/IP.
- Judge0 cần tắt network trong sandbox, giới hạn process/file size và chỉ nhận request từ Core API.
- Với tải lớn, bước tiếp theo là đổi submit đồng bộ sang queue Redis + worker + SignalR. Interface hiện tại cho phép thay adapter mà không đổi Domain/Application.

## Lộ trình tiếp theo

- P1: CRUD đề/test case và workflow Draft → Review → Published cho giáo viên.
- P1: giao diện editor và trang làm bài React.
- P1: danh sách đề và lịch sử submission có phân trang.
- P2: Redis queue, worker chấm bất đồng bộ và SignalR cập nhật trạng thái.
- P2: rejudge, plagiarism checks và version hóa test case.
- P3: AI Hint Engine sử dụng kết quả lỗi đã chuẩn hóa, không nhận full đáp án/test ẩn.
