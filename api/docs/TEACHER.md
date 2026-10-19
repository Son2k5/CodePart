# Giáo viên và bài tập

Tài liệu liên quan: [Kiến trúc](ARCHITECTURE.md) · [Quản lý lớp](CLASS.md).

## Phân quyền

Admin quản lý tài khoản giáo viên, phân công lớp và bài tập. Giáo viên quản lý bài trong Class được phân công hoặc Course do mình sở hữu. Học viên sử dụng bài đã xuất bản trong lớp mình.

Backend kiểm tra tài khoản đang hoạt động và phạm vi dữ liệu. Chưa đăng nhập trả 401; sai vai trò trả 403; tài nguyên ngoài phạm vi có thể trả 404.

## Quản lý tài khoản giáo viên

Giao diện /admin/teachers; tiền tố API /api/admin/teachers.

| Method và đường dẫn | Chức năng |
| --- | --- |
| GET / | Danh sách, tìm kiếm và bộ lọc |
| GET /stats | Thống kê |
| GET /{id} | Chi tiết |
| POST / | Tạo giáo viên |
| PUT /{id} | Cập nhật hồ sơ |
| POST /bulk-approve | Duyệt hàng loạt |
| POST /bulk-disable | Vô hiệu hóa hàng loạt |
| GET /export | Xuất CSV |

Danh mục khoa ở /api/admin/faculties; mở lại tài khoản qua /api/admin/users/{id}/enable.

Tạo mới hỗ trợ Active/Pending. Cập nhật hồ sơ không đổi email, password hoặc trạng thái. Mã giáo viên tối đa 50 ký tự, chuẩn hóa chữ hoa và duy nhất khi có giá trị; điện thoại tối đa 20, giới thiệu 1.000 ký tự.

Thao tác hàng loạt tối đa 100 ID khác nhau, kiểm tra toàn bộ rồi ghi trong transaction. Đổi trạng thái có audit và thu hồi phiên theo nghiệp vụ. CSV dùng UTF-8 BOM, escape nội dung và xử lý công thức bảng tính.

## Cổng giáo viên và lớp học phần

- /teacher/classes: lớp được phân công.
- /teacher/courses: lớp học phần phụ trách.
- /teacher/courses/{id}: thông tin và học viên.
- /teacher/courses/{id}/submissions: bài nộp của luồng cũ, hỗ trợ chấm thủ công.
- /manage/problems: bài toán chấm tự động theo lớp.

API dưới /api/teacher:

| Method và đường dẫn | Chức năng |
| --- | --- |
| GET/POST /courses | Xem/tạo lớp học phần |
| GET /courses/{id} | Chi tiết |
| PUT /courses/{id}/status | Chuyển Active/Closed |
| GET/POST /courses/{id}/students | Xem/thêm học viên bằng email |
| DELETE /courses/{id}/students/{studentId} | Loại học viên |
| GET/POST /courses/{id}/exercises | Bài tập theo luồng cũ |
| GET /classes | Lớp được phân công |
| GET /classes/{id}/students | Học viên của lớp |

Luồng chấm thủ công hỗ trợ danh sách/chi tiết bài nộp, điểm 0–10 với tối đa hai chữ số thập phân và nhận xét tối đa 2.000 ký tự. Học viên dùng /api/student/courses, tham gia qua /join với joinCode.

## Tạo bài tập chấm tự động

1. Mở /manage/problems, chọn lớp hoặc lớp học phần theo tên.
2. Tạo bản nháp gồm đề bài, ngôn ngữ, starter code và giới hạn.
3. Thêm 10–15 test, có 1–2 sample; trọng số dương, thứ tự không trùng.
4. Kiểm tra và xuất bản bài.
5. Bài xuất hiện ở lớp tương ứng; học viên mở vào khung code dùng chung.
6. Theo dõi bài nộp và kết quả trong phạm vi được quản lý.

Mỗi bài có đúng một ClassId hoặc CourseId; không chuyển bài hiện có sang lớp khác. Giáo viên đang được phân công có thể quản lý bài admin tạo trong lớp đó. Admin quản lý mọi phạm vi hợp lệ. Bài cũ không gắn lớp áp dụng quy tắc tương thích theo người tạo.

Test hỗ trợ thay thế danh sách, sắp xếp, xóa và nhập JSON. Dữ liệu hidden test không hiển thị cho học viên.

## API bài toán

Route quản lý nằm dưới /api/admin/problems, dùng chính sách module Judge cho admin/giáo viên theo phạm vi:

- /scopes: lớp có thể quản lý.
- CRUD bài toán và thao tác publish/unpublish.
- PUT /{id}/tests: thay danh sách test.
- POST /{id}/tests/import: nhập JSON.
- /api/admin/submissions: danh sách, chi tiết và source bài nộp được phép xem.

API học viên dưới /api/problems có danh sách/chi tiết, /scopes và bộ lọc classId/courseId; POST /{id}/run để chạy thử, POST /{id}/submit để gửi chấm (HTTP 202), GET /{id}/submissions để xem lịch sử. Trạng thái được lấy qua API submission của module Judge. ClassId được lưu trên bài nộp để hỗ trợ lịch sử theo lớp.

## Khung code dùng chung

Trang /problems/:id tải đề theo ID bài, dùng chung editor cho mọi lớp và hiển thị phạm vi lớp. Bản nháp tách theo bài/ngôn ngữ. Run dùng chạy thử; Submit ghi nhận lần chấm. Kết quả có verdict, điểm, thời gian và bộ nhớ; dữ liệu hidden test được giữ kín.

Người rời lớp hoặc bị loại không còn quyền xem/chạy/nộp bài. Trang /exercise/:slug vẫn phục vụ bài tập cũ. Cấu hình MySQL local, Judge0 riêng và giới hạn kỹ thuật ở [ARCHITECTURE.md](ARCHITECTURE.md).

## Mã nguồn

- Tài khoản giáo viên: Application/Admin, AdminTeacherEndpoints.cs và frontend/src/features/admin.
- Cổng giáo viên: Application/Teaching, TeacherEndpoints.cs và frontend/src/features/teacher.
- Chấm code: Application/Judge, Infrastructure/Judge, JudgeEndpoints.cs và frontend/src/features/code-judge.
