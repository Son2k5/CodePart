# Khung bài giảng và tạo chủ đề trọn gói

Các endpoint yêu cầu Bearer token của Admin hoặc Teacher đang hoạt động. Giáo viên chỉ quản lý nội dung lớp có phân công `ClassTeacher`, hoặc template do chính mình tạo. Admin quản lý tất cả. Template của người dùng có vai trò Admin được chia sẻ để giáo viên đọc và áp dụng; template của Teacher là riêng tư.

## API

| Method | Route | Chức năng |
| --- | --- | --- |
| GET / POST | `/api/curriculum-templates` | Danh sách được phép xem / tạo template |
| GET / PUT / DELETE | `/api/curriculum-templates/{id}` | Chi tiết / sửa metadata / xóa mềm |
| POST | `/api/classes/{classId}/apply-template` | Áp dụng template |
| POST | `/api/classes/{classId}/topics/complete` | Tạo chủ đề, nội dung, bài tập và xuất bản trong một transaction |
| GET | `/api/classes/{classId}/available-problems` | Tìm bài tập theo `search`, `difficulty`, `page`, `pageSize` |

Các route được đăng ký trong Swagger, nhóm `Curriculum`. Các endpoint quản lý chương, chủ đề, draft, completion rule, deadline, exercises và publish hiện có tiếp tục hoạt động. Dùng ID của template ở vị trí `classId` để biên soạn chương/chủ đề mẫu. `GET /api/classes/{classId}/chapters` cũng cho phép học viên đã đăng ký lớp truy cập.

## Tạo và áp dụng template

```json
{
  "name": "Lập trình C++ nâng cao",
  "description": "Con trỏ, OOP, STL và bài tập thực hành",
  "thumbnail": "https://res.cloudinary.com/example/cpp.png",
  "facultyId": null
}
```

Template có `isTemplate=true`, không có mã tham gia, sĩ số, ngày bắt đầu/kết thúc hoặc học viên. Không cho đặt deadline hay ngày mở khóa cố định cho chủ đề mẫu. Danh sách lớp thông thường mặc định loại template; Admin dùng `isTemplate=true` để lọc template.

`GET /curriculum-templates/{id}` trả `{ template, chapters, topics }`; mỗi topic có draft/published content và metadata. Sửa nội dung template cần quyền sở hữu hoặc Admin. `DELETE` trả lỗi `CONFLICT` khi đã có chương/chủ đề kế thừa; gửi `?confirm=true` để xác nhận xóa mềm. Bản sao trong các lớp được giữ nguyên.

Gửi `POST /api/classes/{classId}/apply-template`, kèm header `Idempotency-Key` (chuỗi duy nhất, tối đa 100 ký tự):

```json
{
  "templateId": "8a7c2b3e-1234-4567-89ab-cdef01234567",
  "chapterIds": null,
  "copyProblems": false,
  "afterChapterId": null
}
```

- `chapterIds=null`: lấy tất cả chương, kể cả chương trống. Mảng được chỉ định phải không rỗng, không trùng và thuộc template.
- Chỉ nội dung topic đã xuất bản và đến thời điểm xuất bản được sao chép. Topic draft trong nguồn không được áp dụng.
- Bản sao topic luôn ở trạng thái Draft để giáo viên rà soát; không mang deadline/ngày mở khóa của nguồn. Có `sourceTopicId`, `sourcePublishedVersion`, `sourceChapterId`; slug và block ID được tạo mới.
- `copyProblems=false`: giữ liên kết bài tập đã xuất bản. Học viên được đọc bài tập qua chủ đề của lớp mình đã xuất bản và mở khóa; quyền chỉnh sửa bài gốc không thay đổi.
- `copyProblems=true`: sao chép bài tập cùng test case/starter code sang lớp đích, trạng thái Draft. Giáo viên cần xuất bản bài tập trước khi học viên làm bài.
- `afterChapterId=null`: chèn cuối danh sách; nếu có ID thì chèn sau chương đó trong lớp đích.
- Response là mảng ID topic mới. Gửi lại cùng key và payload trả lại kết quả cũ; cùng key nhưng khác payload trả `CONFLICT`.

Việc clone nội dung và bài tập dùng chung transaction trên cùng MySQL database. API cũ `clone-from` cũng áp dụng kiểm tra quyền sở hữu template.

## Upload tài liệu trước khi tạo chủ đề

1. `POST /api/assets/sign-upload` với `{ "kind": "Pdf", "mimeType": "application/pdf", "sizeBytes": 12345 }`. Response `{ id, upload }` chứa ID asset và thông tin upload của storage.
2. Upload trực tiếp lên storage theo URL/chữ ký trong `upload`.
3. `POST /api/assets/{id}/complete`. Backend kiểm tra MIME, kích thước thực tế, magic bytes và số trang PDF, rồi chuyển sang Ready.
4. Gửi `assetId` trong `lectureContent.documents` khi tạo chủ đề.

PDF tối đa 50 MiB; ảnh tối đa 10 MiB. Số trang lấy từ metadata đã kiểm tra phía server. Không đưa file nhị phân vào request tạo topic. Upload storage diễn ra trước và không thuộc transaction tạo topic; khi tạo topic thất bại, asset vẫn tồn tại để tái sử dụng. Giáo viên được xem tài liệu được tham chiếu trong template/lớp mà họ có quyền truy cập.

## Tạo chủ đề trọn gói

```json
{
  "chapterId": "4a7c2b3e-1111-2222-3333-cdef01234567",
  "title": "Stack & Queue",
  "slug": "stack-queue",
  "summary": "Nguyên lý LIFO/FIFO",
  "order": 1,
  "estimatedMinutes": 90,
  "unlockRule": "None",
  "unlockAt": null,
  "dueAt": null,
  "lectureContent": {
    "markdown": "# Stack\n\nStack hoạt động theo LIFO.",
    "video": {
      "provider": "YouTube",
      "url": "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
      "durationSec": 1820,
      "title": "Bài giảng Stack"
    },
    "documents": []
  },
  "exercises": [],
  "completionRule": {
    "requireVideoPercent": 80,
    "requireDocumentPercent": null,
    "requireExercises": "none",
    "allowManualMarkRead": false
  },
  "publishImmediately": true
}
```

`documents`: mảng `{ assetId, title }`. `exercises`: mảng `{ problemId, order, points, isRequired }`, tối đa 200 bài, ID không trùng, điểm từ 0 đến 10000, thứ tự không âm. `lectureContent`, `exercises` và `completionRule` là bắt buộc; có thể dùng `{}`, `[]`, `{}` nếu chưa có nội dung.

Markdown được làm sạch HTML, video chỉ chấp nhận HTTPS/provider/hostname hợp lệ và thời lượng 1–86400 giây, PDF phải Ready và người tạo có quyền sử dụng. Các thời điểm phải có UTC (`Z`). Nếu một bước thất bại, topic/exercises/search index/audit không được lưu một phần. `publishImmediately=true` tạo search index và phiên bản xuất bản 1. Response `{ id, rowVersion, status, publishedVersion }`.

Tìm bài tập trả `{ items, total, page, pageSize }`; độ khó `Easy|Medium|Hard`, `pageSize` tối đa 100. Giáo viên nhìn thấy bài của lớp, bài do mình tạo, và bài Published không thuộc lớp/course nào. Schema hiện chưa có trạng thái phê duyệt riêng; Published là điều kiện của ngân hàng công khai. Không trả test case ẩn. Cùng quy tắc áp dụng khi gắn bài tập vào topic.

## Kiểm thử

```powershell
dotnet build CodePath.sln
dotnet test tests/CodePath.Unit.Tests/CodePath.Unit.Tests.csproj
dotnet test tests/CodePath.Architecture.Tests/CodePath.Architecture.Tests.csproj
dotnet test tests/CodePath.Integration.Tests/CodePath.Integration.Tests.csproj --filter FullyQualifiedName~CurriculumSpecifications
```

Test tích hợp cần Docker chạy được Linux containers (MySQL 8.4 và Redis). Bao phủ RBAC, template cá nhân/chia sẻ, tạo topic, PDF Pending, URL video, điểm không hợp lệ, ngân hàng bài tập, clone/link/copy, idempotency và rollback sau khi DB đã thực thi SaveChanges nhưng chưa commit. Không thêm migration: dùng các cột/bảng đã có trong `LectureDelivery` và `ClassManagement`.
