export interface NavItem {
  id: string
  label: string
  href: string
  isExternal?: boolean
}

export interface WorkflowStep {
  id: string
  stepNumber: string
  title: string
  description: string
  badgeText?: string
  iconName: 'folder-code' | 'play-code' | 'mentor-help'
}

export interface CourseDetail {
  id: string
  tag: string
  tagVariant: 'basic' | 'advanced' | 'applied'
  seatsAvailableText: string
  seatsUrgent?: boolean
  title: string
  description: string
  exerciseCount: string
  supportType: string
  scheduleText: string
  scheduleType: 'Lịch giao bài' | 'Chấm điểm' | 'Lịch nộp' | 'Lịch thực hành' | 'Tiến độ' | 'Đồ án'
  levelBadge: string
  recommendedAge: string
}

export interface Testimonial {
  id: string
  quote: string
  authorName: string
  authorRole: string
  avatarInitials: string
  authorType: 'student' | 'parent'
}

export interface TrialRegistrationForm {
  contact: string
  studentName?: string
  gradeLevel?: string
  selectedCourseId?: string
}

export interface ApiResponse<T> {
  success: boolean
  data?: T
  message?: string
}
