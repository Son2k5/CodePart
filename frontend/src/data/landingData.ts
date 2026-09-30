import type { NavItem, WorkflowStep, CourseDetail, Testimonial } from '../types/landing'

export const NAVIGATION_ITEMS: NavItem[] = [
  { id: 'home', label: 'Trang chủ', href: '#home' },
  { id: 'courses', label: 'Lớp thực hành', href: '#courses' },
  { id: 'workflow', label: 'Quy trình học', href: '#workflow' },
  { id: 'demo', label: 'Trải nghiệm', href: '#interactive-demo' },
  { id: 'testimonials', label: 'Đánh giá', href: '#testimonials' },
  { id: 'contact', label: 'Liên hệ', href: '#contact' },
]

export const WORKFLOW_STEPS: WorkflowStep[] = [
  {
    id: 'step-1',
    stepNumber: '1',
    title: 'Nhận bài tập theo học phần',
    description:
      'Bài tập thực hành được giao bám sát đề cương môn học (Python, Java, JS). Sinh viên mở web là có thể code ngay lập tức, không mất thời gian setup môi trường.',
    badgeText: 'Môi trường Web IDE',
    iconName: 'folder-code',
  },
  {
    id: 'step-2',
    stepNumber: '2',
    title: 'Làm bài & nộp mã nguồn',
    description:
      'Trình soạn thảo mã nguồn tích hợp sẵn hệ thống kiểm thử tự động, lập tức biên dịch, chạy test case và chỉ ra lỗi logic, lỗi cú pháp.',
    badgeText: 'Chấm test tự động',
    iconName: 'play-code',
  },
  {
    id: 'step-3',
    stepNumber: '3',
    title: 'Giảng viên & Trợ giảng review',
    description:
      'Ngoài điểm tự động, giảng viên hoặc trợ giảng thực hành sẽ review cách triển khai thuật toán, chuẩn clean code và thiết kế hướng đối tượng (OOP).',
    badgeText: 'Review Code 1:1',
    iconName: 'mentor-help',
  },
]

export const COURSES_DATA: CourseDetail[] = [
  {
    id: 'course-python',
    tag: 'CƠ SỞ NGÀNH',
    tagVariant: 'basic',
    seatsAvailableText: 'Đang mở đăng ký',
    seatsUrgent: false,
    title: 'Nhập môn Lập trình Python',
    description:
      'Học phần cơ sở cho sinh viên năm 1. Làm quen với tư duy lập trình, biến, cấu trúc điều khiển, hàm và các cấu trúc dữ liệu cơ bản trong Python.',
    exerciseCount: '80+ bài tập thuật toán cơ bản',
    supportType: 'Chấm tự động & Trợ giảng review',
    scheduleType: 'Lịch thực hành',
    scheduleText: 'Theo thời khóa biểu khoa',
    levelBadge: 'Năm 1',
    recommendedAge: 'Sinh viên Năm 1',
  },
  {
    id: 'course-java-core',
    tag: 'CƠ CHUYÊN NGÀNH',
    tagVariant: 'advanced',
    seatsAvailableText: 'Sắp đóng đăng ký',
    seatsUrgent: true,
    title: 'Lập trình Java Cơ bản',
    description:
      'Trang bị kiến thức cốt lõi về ngôn ngữ Java. Cú pháp, các thư viện chuẩn, xử lý ngoại lệ và luồng I/O. Tiền đề quan trọng cho OOP và Web Backend.',
    exerciseCount: '120+ bài tập Java Core',
    supportType: 'Chấm test case nghiêm ngặt',
    scheduleType: 'Tiến độ',
    scheduleText: 'Kiểm tra tiến độ hàng tuần',
    levelBadge: 'Năm 2',
    recommendedAge: 'Sinh viên Năm 1, 2',
  },
  {
    id: 'course-java-oop',
    tag: 'CHUYÊN SÂU',
    tagVariant: 'advanced',
    seatsAvailableText: 'Đang mở đăng ký',
    seatsUrgent: false,
    title: 'Lập trình Hướng đối tượng (Java OOP)',
    description:
      'Tập trung vào 4 tính chất OOP (Đóng gói, Kế thừa, Đa hình, Trừu tượng), các design pattern cơ bản và SOLID principles bằng ngôn ngữ Java.',
    exerciseCount: '15+ Mini Project & Bài tập thiết kế',
    supportType: 'Review kiến trúc class & Code',
    scheduleType: 'Đồ án',
    scheduleText: 'Bảo vệ bài tập lớn cuối kỳ',
    levelBadge: 'Năm 2',
    recommendedAge: 'Sinh viên Năm 2',
  },
  {
    id: 'course-js-basic',
    tag: 'THỰC HÀNH ỨNG DỤNG',
    tagVariant: 'applied',
    seatsAvailableText: 'Mở lớp 15/10',
    seatsUrgent: false,
    title: 'Lập trình JavaScript Cơ bản',
    description:
      'Nắm vững JavaScript Core, ES6+, xử lý bất đồng bộ (Promises, Async/Await), thao tác DOM. Nền tảng vững chắc để tiếp cận React, Node.js sau này.',
    exerciseCount: '60+ bài tập xử lý logic JS',
    supportType: 'Chấm tự động qua Jest/Mocha',
    scheduleType: 'Lịch nộp',
    scheduleText: 'Hạn chót cuối tuần',
    levelBadge: 'Năm 2, 3',
    recommendedAge: 'Sinh viên Năm 2, 3',
  },
]

export const TESTIMONIALS_DATA: Testimonial[] = [
  {
    id: 'testimonial-1',
    quote:
      '“Môn Java OOP trước đây em rất sợ vì khó hình dung cấu trúc class. Khi làm bài trên hệ thống, test case chỉ rõ object trả về sai ở đâu, trợ giảng comment trực tiếp vào từng method nên em nắm bắt rất nhanh.”',
    authorName: 'Nguyễn Văn Nam',
    authorRole: 'Sinh viên K65 - Khoa CNTT',
    avatarInitials: 'VN',
    authorType: 'student',
  },
  {
    id: 'testimonial-2',
    quote:
      '“Nền tảng giúp sinh viên tiết kiệm thời gian setup môi trường. Em chỉ cần mở trình duyệt, code Python và nhận kết quả tức thì. Rất hữu ích cho các môn thực hành cơ sở ngành.”',
    authorName: 'Trần Thị Mai',
    authorRole: 'Sinh viên K66 - Chuyên ngành HTTT',
    avatarInitials: 'TM',
    authorType: 'student',
  },
  {
    id: 'testimonial-3',
    quote:
      '“Giao diện quản lý lớp giúp giảng viên theo dõi tiến độ thực hành của cả lớp dễ dàng. Việc hệ thống tự động chấm các test case cơ bản (JS, Java) giúp tôi có thêm thời gian review sâu về logic và design pattern cho các em.”',
    authorName: 'ThS. Lê Hoàng',
    authorRole: 'Giảng viên khoa CNTT',
    avatarInitials: 'LH',
    authorType: 'parent',
  },
]

export const FOOTER_DATA = {
  about:
    'Nền tảng thực hành và chấm điểm code tự động chuyên biệt dành cho sinh viên Khoa Công nghệ thông tin.',
  standardBadge: 'HỆ THỐNG THỰC HÀNH TIÊU CHUẨN',
  curriculums: [
    'Nhập môn Lập trình Python',
    'Lập trình Java Cơ bản',
    'Lập trình Hướng đối tượng (Java OOP)',
    'Lập trình JavaScript',
  ],
  systemProcesses: [
    'Quy trình nộp bài tập',
    'Chấm điểm tự động (Auto-Grader)',
    'Hỗ trợ giải đáp thực hành',
    'Nội quy phòng máy ảo',
  ],
  contact: {
    address: 'Văn phòng Khoa CNTT, Tòa nhà D',
    hotline: '0243 123 456',
    email: 'vpkhoa.cntt@university.edu.vn',
  },
}
