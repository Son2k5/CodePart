# CodePath workspace

Workspace mới thay màn hình `/problems/:id`, giữ nguyên danh sách bài và API hiện tại.
Thiết kế light theme dùng xanh trời, xanh lá, vàng nhẹ như landing page, với mascot SVG **Mầm**.

## Chạy độc lập bằng mock

```powershell
cd frontend
npm install
npm run dev:workspace
```

- Mở `http://localhost:5174/problems/demo-sum`.
- Route có ngữ cảnh lớp: `http://localhost:5174/classes/demo-class/topics/nhap-xuat/problems/demo-sum`.
- Bộ màu, mascot và trạng thái: `http://localhost:5174/design`.
- Không cần backend, chứng chỉ HTTPS hay tài khoản. MSW cung cấp phiên học sinh giả chỉ trong chế độ development này.
- Mở **Kịch bản demo** ở góc dưới để chọn verdict, hàng đợi dài, rate limit hoặc tình huống AI.
- Mock mô phỏng kết quả theo kịch bản đã chọn, **không thực thi code**. Custom input demo hỗ trợ tính tổng hai số.
- `.env.workspace` đặt `VITE_USE_MOCK=true`. Biến cũ `VITE_ENABLE_MOCKS` vẫn được giữ cho các mock xác thực trước đây.
- Bài nộp demo nằm trong bộ nhớ worker của trang, mất khi reload; nháp code vẫn lưu trên thiết bị.

## Chuyển sang backend thật

Chạy `npm run dev` theo cấu hình HTTPS hiện có. Không đặt `VITE_USE_MOCK=true` trong môi trường này.
Đăng nhập học sinh và mở `/problems/<GUID>` hoặc `/classes/<classId>/topics/<slug>/problems/<GUID>`.
Build production không bật MSW, kể cả khi đặt cờ mock.

| Điểm nối | Thực hiện |
| --- | --- |
| Đề bài, starter code, sample | `GET /api/problems/{id}` |
| Chạy sample/custom input | `POST /api/problems/{id}/run` với `language`, `sourceCode`, `customInput` tùy chọn |
| Nộp bài | `POST /api/problems/{id}/submit`, truyền `topicId` khi có ngữ cảnh bài học |
| Tiến độ | `GET /api/submissions/{id}`, polling 1,5 giây; dừng khi hoàn thành hoặc lỗi sau retry |
| Lịch sử | `GET /api/problems/{id}/submissions?page=...` |
| Bài trước/sau trong topic | `GET /api/classes/{classId}/topics/{slug}` → `exercises` |
| Trợ giảng | `POST /api/ai/assist`, SSE `delta`/`result`; dùng panel hiện có |
| Phản hồi AI | `POST /api/ai/{interactionId}/feedback` |
| Xác thực | Tái sử dụng `src/lib/axios.ts`, cookie và access token trong memory, refresh single-flight |

### Khác biệt đã chủ động giữ tương thích

- Chấm bài dùng polling; không tự giả định backend có hub `submission-status`. AI tiếp tục dùng SSE hiện có, không thêm SignalR hoặc cơ chế retry sinh lại gợi ý làm tiêu quota hai lần.
- Backend hiện trả `maxRuntimeMs`/`maxMemoryKb`; UI đọc đúng hai trường này.
- `queuePosition`, `dueAt`, `latePenalty`, `line`/`column` là dữ liệu tùy chọn: chỉ hiển thị khi API cung cấp, không dựng điểm/hạn nộp giả. Mock có queue position và lỗi theo dòng để kiểm tra UI. Backend hiện trả compile output dạng chuỗi nên chưa bảo đảm click tới dòng cho lỗi thực tế.
- Hidden test chỉ render số thứ tự + verdict. Không render input/output, error hay timing từ payload hidden.
- Dùng Sonner sẵn có cho toast; Radix Tabs/Dialog cho bàn phím và focus. Không thêm một hệ thống toast thứ hai.
- Theme token nằm trong CSS, phù hợp cấu trúc Tailwind 4 hiện tại; không tạo cấu hình Tailwind cũ.
- Màn hình không hiển thị streak hay điểm phạt khi chưa có nguồn dữ liệu thật.
- Bài trước/sau ngoài topic lấy danh sách trang đầu hiện có; trong topic dùng thứ tự `exercises` từ API.

## Cấu trúc

```text
features/workspace/
  WorkspacePage.tsx       # Điều phối API, panel, Run/Submit, lịch sử và mobile
  CodeEditor.tsx          # Lazy Monaco, local worker, theme, markers/decorations
  store.ts               # Zustand draft + khóa trạng thái chạy
  Results.tsx            # VerdictBanner, TestGrid, ResultCard
  ui.tsx                 # Mascot, Loading, Empty, Modal, PanelBoundary
  tokens.css             # Font local và bảng màu theo landing page
  workspace.css          # Desktop/mobile, reduced motion, focus
  DesignSystemPage.tsx    # /design
  mocks/                 # MSW handlers, fixtures, chọn kịch bản
features/ai-assistant/
  HintStepper.tsx         # Cấp hint khóa/mở và quota
```

Code editor nằm trong Monaco model và Zustand, không nằm trong React state của workspace.
Nháp debounce 800ms; flush khi đổi ngôn ngữ, đổi bài, Ctrl/Cmd+S, pagehide hoặc tab bị ẩn.
Key nháp: `codepath:draft:v2:<userId>:<problemId>:<language>`. Không lưu token.
Nếu localStorage bị chặn/hết dung lượng, code vẫn ở memory và thanh trạng thái báo chưa lưu được.
Tỉ lệ panel được lưu riêng, hỗ trợ resize bằng bàn phím. Lỗi theo dòng chỉ áp dụng cho snapshot đã gửi.

## Kiểm tra

```powershell
npm run build
npm run test:workspace
npm run test:workspace:e2e
node --test tests/assistant-stream.test.mjs
```

E2E dùng Chrome đã cài trên máy (`channel: chrome`), tự khởi động mock server cổng 5174 nếu cần.
Ảnh chụp và trace nằm ở `test-results/` (đã gitignore).

### Checklist thủ công

- [ ] 360 / 768 / 1280 px: không cuộn ngang, nút chạy/nộp luôn truy cập được.
- [ ] Kéo cả hai thanh resize; focus thanh và dùng phím mũi tên; reload giữ tỉ lệ.
- [ ] Gõ Python, chuyển C++, trở lại Python; reload vẫn còn bản nháp.
- [ ] Reset hỏi xác nhận; hủy giữ code; đồng ý chỉ thay ngôn ngữ hiện tại.
- [ ] Chạy sample và custom input; mobile mở Kết quả → Testcase để nhập input.
- [ ] Accepted, WrongAnswer, CE, RE, TLE, MLE, PartialPass, LongQueue, các lỗi rate limit.
- [ ] CE mock: bấm dòng lỗi; sửa code thì marker cũ biến mất.
- [ ] Trong khi đang nộp/chấm, bấm nút và phím tắt liên tục không gửi trùng.
- [ ] Mất kết nối khi polling: thông báo lỗi, retry; không tự cho nộp thêm khi chưa biết bài cũ kết thúc.
- [ ] Hint khóa/mở, SSE, feedback, quota, highlight dòng; đổi code không dùng hint cũ.
- [ ] Hidden test không lộ dữ liệu dù payload có trường nhạy cảm.
- [ ] Bật reduced motion: không confetti, không loading nhảy; dùng bàn phím cho tabs và dialog.

Việc kiểm tra mock không thay thế kiểm thử tích hợp backend thật với tài khoản và dữ liệu lớp học.
