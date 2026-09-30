import React, { useState } from 'react'
import { X, CheckCircle2 } from 'lucide-react'
import type { CourseDetail } from '../../types/landing'

interface TrialModalProps {
  isOpen: boolean
  onClose: () => void
  courses: CourseDetail[]
  preselectedCourse?: CourseDetail | null
  onSubmit: (data: {
    studentName: string
    grade: string
    contact: string
    courseId: string
  }) => Promise<void> | void
}

export const TrialModal: React.FC<TrialModalProps> = ({
  isOpen,
  onClose,
  courses,
  preselectedCourse,
  onSubmit,
}) => {
  const [studentName, setStudentName] = useState('')
  const [grade, setGrade] = useState('Năm 1')
  const [contact, setContact] = useState('')
  const [courseId, setCourseId] = useState(preselectedCourse?.id ?? courses[0]?.id ?? '')
  const [isSubmitting, setIsSubmitting] = useState(false)

  if (!isOpen) return null

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!contact.trim()) return

    setIsSubmitting(true)
    try {
      await onSubmit({
        studentName,
        grade,
        contact,
        courseId: courseId || (courses[0]?.id ?? ''),
      })
      onClose()
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/40 backdrop-blur-sm animate-fadeIn">
      <div className="relative w-full max-w-lg bg-white rounded-2xl border border-slate-200 shadow-2xl p-6 sm:p-8">
        {/* Close Button */}
        <button
          onClick={onClose}
          className="absolute top-5 right-5 p-1.5 text-slate-400 hover:text-slate-700 hover:bg-slate-100 rounded-lg transition"
          aria-label="Đóng"
        >
          <X className="w-5 h-5" />
        </button>

        {/* Modal Header */}
        <div className="mb-6">
          <span className="text-xs font-bold uppercase tracking-wider text-blue-700 block mb-1">
            ĐĂNG KÝ TÀI KHOẢN THỰC HÀNH
          </span>
          <h3 className="text-2xl font-extrabold text-slate-900 tracking-tight">
            Truy cập hệ thống
          </h3>
          <p className="text-xs sm:text-sm text-slate-600 mt-1">
            Nhận ngay 10 bài tập rèn luyện đầu tiên kèm tài khoản thực hành trực tiếp.
          </p>
        </div>

        {/* Modal Form */}
        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-xs font-bold uppercase tracking-wider text-slate-700 mb-1.5">
              Họ tên sinh viên / Mã sinh viên
            </label>
            <input
              type="text"
              required
              value={studentName}
              onChange={(e) => setStudentName(e.target.value)}
              placeholder="Ví dụ: Nguyễn Minh Khang"
              className="w-full px-3.5 py-2.5 bg-white border border-slate-300 rounded-lg text-sm text-slate-900 placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-600 focus:border-transparent transition"
            />
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block text-xs font-bold uppercase tracking-wider text-slate-700 mb-1.5">
                Năm học hiện tại
              </label>
              <select
                value={grade}
                onChange={(e) => setGrade(e.target.value)}
                className="w-full px-3.5 py-2.5 bg-white border border-slate-300 rounded-lg text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-blue-600 focus:border-transparent transition"
              >
                <option value="Năm 1">Sinh viên Năm 1</option>
                <option value="Năm 2">Sinh viên Năm 2</option>
                <option value="Năm 3">Sinh viên Năm 3</option>
                <option value="Năm 4">Sinh viên Năm 4</option>
                <option value="Khác">Khác</option>
              </select>
            </div>

            <div>
              <label className="block text-xs font-bold uppercase tracking-wider text-slate-700 mb-1.5">
                Học phần quan tâm
              </label>
              <select
                value={courseId}
                onChange={(e) => setCourseId(e.target.value)}
                className="w-full px-3.5 py-2.5 bg-white border border-slate-300 rounded-lg text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-blue-600 focus:border-transparent transition"
              >
                {courses.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.title}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div>
            <label className="block text-xs font-bold uppercase tracking-wider text-slate-700 mb-1.5">
              Số điện thoại / Email sinh viên
            </label>
            <input
              type="text"
              required
              value={contact}
              onChange={(e) => setContact(e.target.value)}
              placeholder="098x xxx xxx hoặc email..."
              className="w-full px-3.5 py-2.5 bg-white border border-slate-300 rounded-lg text-sm text-slate-900 placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-600 focus:border-transparent transition"
            />
          </div>

          <div className="pt-2">
            <button
              type="submit"
              disabled={isSubmitting}
              className="w-full py-3 px-4 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-400 text-white font-semibold text-sm rounded-xl shadow-sm transition"
            >
              {isSubmitting ? 'Đang kích hoạt tài khoản...' : 'Kích hoạt tài khoản thực hành'}
            </button>
          </div>

          <div className="flex items-center justify-center gap-2 pt-2 text-xs text-slate-500">
            <CheckCircle2 className="w-3.5 h-3.5 text-emerald-600" />
            <span>Hoàn toàn miễn phí • Không phát sinh phụ phí</span>
          </div>
        </form>
      </div>
    </div>
  )
}
