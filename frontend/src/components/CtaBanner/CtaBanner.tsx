import React, { useState } from 'react'
import { Check, Send } from 'lucide-react'

interface CtaBannerProps {
  onRegisterSubmit: (contact: string) => Promise<void> | void
}

export const CtaBanner: React.FC<CtaBannerProps> = ({ onRegisterSubmit }) => {
  const [contact, setContact] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!contact.trim()) return

    setIsSubmitting(true)
    try {
      await onRegisterSubmit(contact)
      setContact('')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <section className="py-16 sm:py-20 bg-white border-t border-slate-200">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="max-w-4xl mx-auto bg-white border border-slate-200 rounded-2xl p-8 sm:p-14 text-center shadow-lg">
          {/* Eyebrow */}
          <span className="inline-block text-xs font-bold uppercase tracking-wider text-blue-700 mb-3">
            TRẢI NGHIỆM MIỄN PHÍ DÀNH CHO SINH VIÊN
          </span>

          {/* Heading */}
          <h2 className="text-2xl sm:text-3xl lg:text-4xl font-extrabold text-slate-900 tracking-tight mb-4">
            Bắt đầu nhận bài tập và luyện code cùng hệ thống ngay hôm nay
          </h2>

          {/* Subtitle */}
          <p className="text-sm sm:text-base text-slate-600 max-w-2xl mx-auto mb-8 leading-relaxed">
            Đăng ký tài khoản để làm thử 10 bài tập lập trình cơ bản có trợ giảng hướng dẫn và chấm điểm tự động.
          </p>

          {/* Form */}
          <form onSubmit={handleSubmit} className="max-w-lg mx-auto flex flex-col sm:flex-row gap-3 mb-8">
            <input
              type="text"
              required
              value={contact}
              onChange={(e) => setContact(e.target.value)}
              placeholder="Nhập mã sinh viên hoặc email..."
              className="flex-1 px-4 py-3 bg-white border border-slate-300 rounded-xl text-sm text-slate-900 placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-600 focus:border-transparent transition"
            />
            <button
              type="submit"
              disabled={isSubmitting}
              className="inline-flex items-center justify-center gap-2 px-6 py-3 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-400 text-white font-semibold text-sm rounded-xl shadow-sm transition shrink-0"
            >
              <span>{isSubmitting ? 'Đang gửi...' : 'Đăng ký tài khoản'}</span>
              <Send className="w-4 h-4" />
            </button>
          </form>

          {/* Guarantees / Checklist */}
          <div className="flex flex-wrap items-center justify-center gap-6 sm:gap-8 pt-2">
            <div className="flex items-center gap-2 text-xs sm:text-sm font-semibold text-slate-700">
              <span className="flex items-center justify-center w-5 h-5 rounded-full bg-blue-50 text-blue-600">
                <Check className="w-3.5 h-3.5 stroke-[2.5]" />
              </span>
              <span>Không thu phí dùng thử</span>
            </div>

            <div className="flex items-center gap-2 text-xs sm:text-sm font-semibold text-slate-700">
              <span className="flex items-center justify-center w-5 h-5 rounded-full bg-blue-50 text-blue-600">
                <Check className="w-3.5 h-3.5 stroke-[2.5]" />
              </span>
              <span>Nhận kết quả ngay sau khi nộp</span>
            </div>

            <div className="flex items-center gap-2 text-xs sm:text-sm font-semibold text-slate-700">
              <span className="flex items-center justify-center w-5 h-5 rounded-full bg-blue-50 text-blue-600">
                <Check className="w-3.5 h-3.5 stroke-[2.5]" />
              </span>
              <span>Hỗ trợ hỏi đáp trực tiếp</span>
            </div>
          </div>
        </div>
      </div>
    </section>
  )
}
