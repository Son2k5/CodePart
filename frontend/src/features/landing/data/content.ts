import type { LearningPath, NavItem } from '../types'

export const navigation: NavItem[] = [
  { id: 'home', label: 'Trang chủ', href: '#home' },
  { id: 'system-features', label: 'Tính năng', href: '#system-features' },
  { id: 'about', label: 'Giới thiệu', href: '#about' },
  { id: 'workflow', label: 'Cách học', href: '#workflow' },
  { id: 'courses', label: 'Học phần', href: '#courses' },
  { id: 'faq', label: 'Câu hỏi thường gặp', href: '#faq' },
  { id: 'contact', label: 'Bắt đầu', href: '#contact' },
]

export const heroSlides = [
  { label: 'Học lập trình trực tuyến', title: 'Bắt đầu học code', emphasis: 'ngay tại nhà', action: 'Bắt đầu hành trình của bạn' },
  { label: 'Thực hành cùng CodePart', title: 'Viết từng dòng code', emphasis: 'vững từng kỹ năng', action: 'Khám phá hệ thống CodePart' },
  { label: 'Phản hồi ngay sau mỗi lần nộp', title: 'Thử sức mỗi ngày', emphasis: 'tiến bộ từng bước', action: 'Tìm hiểu tính năng chấm điểm' },
  { label: 'Python · Java · C++ · JavaScript', title: 'Từ những điều nhỏ', emphasis: 'đến ý tưởng lớn', action: 'Khám phá các học phần' },
  { label: 'Không gian học tập của bạn', title: 'Học ở bất cứ đâu', emphasis: 'bắt đầu từ đây', action: 'Tạo tài khoản CodePart' },
]

export const systemFeatures = [
  { title: 'Bài tập theo từng học phần', description: 'Tiếp cận bài tập lập trình theo môn học và cấp độ. Xem yêu cầu, theo dõi hạn nộp và quản lý bài làm trong cùng một không gian.', icon: 'assignments', tone: 'blue' },
  { title: 'Thực hành & chấm điểm tự động', description: 'Viết và chạy code trực tiếp trên trình duyệt. Nộp bài, nhận kết quả từng test case và tìm ra lỗi để hoàn thiện lời giải.', icon: 'code', tone: 'yellow' },
  { title: 'Kết nối lớp học & giảng viên', description: 'Giảng viên giao bài, theo dõi tiến độ và nhận xét bài làm. Sinh viên xem kết quả, tiếp nhận góp ý và cải thiện kỹ năng mỗi ngày.', icon: 'people', tone: 'green' },
] as const

export const workflowSteps = [
  { number: '01', title: 'Chọn bài để bắt đầu', description: 'Vào lớp học, đọc đề bài và chọn một thử thách phù hợp với tiến độ của bạn.' },
  { number: '02', title: 'Viết code, thử ý tưởng', description: 'Tập trung vào lời giải trong trình soạn thảo. Chạy thử và điều chỉnh từng bước.' },
  { number: '03', title: 'Nhận phản hồi, tiến xa hơn', description: 'Xem kết quả kiểm thử và góp ý từ giảng viên để hiểu rõ điều mình cần cải thiện.' },
]

export const learningPaths: LearningPath[] = [
  { id: 'python', name: 'Nền tảng Python', mark: 'Py', category: 'foundation', level: 'Nhập môn', description: 'Bắt đầu với những dòng code đầu tiên và xây dựng tư duy giải quyết vấn đề.', topics: ['Biến & kiểu dữ liệu', 'Vòng lặp', 'Hàm'], tone: 'blue' },
  { id: 'java', name: 'Lập trình Java', mark: 'Ja', category: 'foundation', level: 'Cơ sở', description: 'Nắm vững cú pháp và cách tổ chức chương trình qua từng bài thực hành.', topics: ['Cú pháp Java', 'Mảng & chuỗi', 'Xử lý ngoại lệ'], tone: 'yellow' },
  { id: 'oop', name: 'Tư duy hướng đối tượng', mark: '{ }', category: 'advanced', level: 'Chuyên sâu', description: 'Biến yêu cầu thực tế thành các lớp, đối tượng và một thiết kế dễ mở rộng.', topics: ['Đóng gói', 'Kế thừa', 'Đa hình'], tone: 'green' },
  { id: 'algorithms', name: 'Cấu trúc dữ liệu & giải thuật', mark: '++', category: 'advanced', level: 'Nâng cao', description: 'Rèn cách chọn cấu trúc dữ liệu và tìm lời giải hiệu quả cho mỗi bài toán.', topics: ['Tìm kiếm', 'Sắp xếp', 'Độ phức tạp'], tone: 'blue' },
]

export const faqs = [
  { question: 'Mới bắt đầu học lập trình có thể sử dụng CodePart không?', answer: 'Có. Bạn có thể bắt đầu từ các bài tập nền tảng của học phần, đọc yêu cầu và thử từng lời giải nhỏ. Hãy chọn bài theo hướng dẫn của giảng viên để tiến độ phù hợp với kiến thức hiện tại.' },
  { question: 'Tôi có cần cài đặt môi trường lập trình không?', answer: 'Với bài tập trên hệ thống, bạn viết, chạy thử và nộp code ngay trong trình duyệt. Bạn cần kết nối Internet để tải bài tập và nhận kết quả xử lý.' },
  { question: 'Kết quả chấm bài cho biết những gì?', answer: 'Kết quả kiểm thử cho biết lời giải đã vượt qua các test case nào và phản hồi về quá trình chạy. Hãy kết hợp kết quả này với yêu cầu đề bài và nhận xét của giảng viên để cải thiện lời giải.' },
  { question: 'Làm thế nào để tham gia lớp học?', answer: 'Tạo tài khoản hoặc đăng nhập vào CodePart, sau đó làm theo hướng dẫn tham gia lớp của giảng viên. Các bài tập được tổ chức theo lớp và học phần của bạn.' },
]
