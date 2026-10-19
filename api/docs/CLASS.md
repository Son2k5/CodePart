# Quản lý lớp

Tài liệu liên quan: [Kiến trúc](ARCHITECTURE.md) · [Giáo viên và bài tập](TEACHER.md).

## Mô hình

Class là nhóm lớp do admin quản lý; Course là lớp học phần do giáo viên phụ trách. Bài toán mới thuộc đúng một Class hoặc Course.

Class lưu tên, mô tả, ảnh, sức chứa, thời gian, trạng thái và cách tham gia. Trạng thái: Draft/Active/Archived. Truy cập: Public/CodeRequired. RowVersion GUID phát hiện cập nhật đồng thời; xóa lớp là xóa mềm.

ClassTeacher là quan hệ nhiều giáo viên/nhiều lớp. ClassEnrollment là quan hệ học viên/lớp với trạng thái Active/Removed. Đây là nguồn dữ liệu phân công và thành viên chính; TeacherId và User.ClassId cũ còn để tương thích. Migration chuyển dữ liệu cũ sang quan hệ mới; kiểm tra trạng thái Draft của lớp cũ trước khi sử dụng.

## Luồng sử dụng

1. Admin tạo lớp, đặt sức chứa và cách tham gia.
2. Admin phân công giáo viên, chuyển lớp sang Active.
3. Học viên tham gia lớp công khai hoặc nhập mã lớp.
4. Giáo viên được phân công hoặc admin tạo và xuất bản bài tập theo lớp.
5. Học viên mở bài từ trang lớp hoặc /problems vào khung code chung /problems/:id.
6. Học viên Run để thử và Submit để chấm, lưu kết quả theo bài/người học.

Mọi lớp dùng chung khung code; đề và test được tải theo bài đang chọn. Public chỉ là cách tham gia lớp, không bỏ qua kiểm tra thành viên khi xem/chạy/nộp bài.

Trang admin: /admin/classes cùng trang tạo/sửa/chi tiết. Học viên: /classes. Giáo viên: /teacher/classes. ClassProblemsPanel hiển thị bài theo Class hoặc Course.

## API admin

Tiền tố /api/admin/classes, yêu cầu admin đang hoạt động.

| Method và đường dẫn | Chức năng |
| --- | --- |
| GET / | Danh sách, tìm kiếm, phân trang và lọc |
| GET /{id} | Chi tiết |
| POST / | Tạo lớp |
| PUT /{id} | Cập nhật kèm RowVersion |
| DELETE /{id} | Xóa mềm; lớp có học viên cần force=true và confirm=true |
| PATCH /{id}/status | Đổi trạng thái |
| POST /{id}/teachers | Gán bằng teacherIds |
| DELETE /{id}/teachers/{teacherId} | Bỏ phân công |
| POST /{id}/join-code/regenerate | Tạo lại mã, có thể đặt expiresAt |
| DELETE /{id}/join-code | Thu hồi mã |
| GET /{id}/students | Danh sách học viên |
| DELETE /{id}/students/{studentId} | Loại học viên |

Bộ lọc gồm page, pageSize, search, status, accessType, hasTeacher, teacherId, sort và desc. Khi cập nhật, teacherIds=null giữ phân công; mảng rỗng xóa phân công.

## API học viên

Tiền tố /api/classes:

| Method và đường dẫn | Chức năng |
| --- | --- |
| GET /public | Lớp công khai |
| POST /{id}/join | Tham gia lớp công khai |
| POST /join-by-code | Tham gia bằng body chứa code |
| POST /{id}/leave | Rời lớp |

Mã tham gia dài 8 ký tự, sinh ngẫu nhiên mật mã, chuẩn hóa chữ hoa và tránh ký tự dễ nhầm. Nhập mã giới hạn 5 lần/phút/người dùng trên từng tiến trình API. Không ghi mã vào audit log hoặc lộ qua DTO học viên.

## Tính nhất quán

Thay đổi thành viên khóa hàng lớp bằng FOR UPDATE trong transaction ReadCommitted để kiểm soát sức chứa. Unique key ngăn trùng lớp/học viên; tham gia lại chuyển Removed thành Active. Audit ClassChanged nằm cùng transaction.

RowVersion cũ trả CLASS_CONCURRENCY_CONFLICT; cần tải lại trước khi sửa tiếp. Lỗi khác gồm CLASS_NOT_FOUND, CLASS_FULL, ALREADY_ENROLLED, CLASS_HAS_STUDENTS, INVALID_JOIN_CODE, JOIN_CODE_EXPIRED, CLASS_NOT_ACTIVE và CLASS_ACCESS_MISMATCH.

Course có chủ sở hữu là giáo viên, trạng thái Draft/Active/Closed, mã tham gia riêng và enrollment Active/Dropped/Completed. Xem quy trình tại [TEACHER.md](TEACHER.md).

## Mã nguồn

- Application/CodePath.Application/Classes và Domain/CodePath.Domain/Users/Entities/Class.Management.cs dưới api/src.
- Api/CodePath.Api/Endpoints/ClassEndpoints.cs.
- Infrastructure: AppDbContext.Classes.cs và ClassManagementConfiguration.cs.
- Frontend: frontend/src/features/classes.
