import React from 'react'
import type { Testimonial } from '../../types/landing'
import { TestimonialCard } from './TestimonialCard'

interface TestimonialsSectionProps {
  testimonials: Testimonial[]
}

export const TestimonialsSection: React.FC<TestimonialsSectionProps> = ({ testimonials }) => {
  return (
    <section id="testimonials" className="py-16 sm:py-24 bg-white border-t border-slate-200">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        {/* Section Header */}
        <div className="max-w-3xl mb-12 sm:mb-16">
          <span className="text-xs font-bold uppercase tracking-wider text-blue-700 block mb-2">
            CẢM NHẬN TỪ SINH VIÊN
          </span>
          <h2 className="text-2xl sm:text-3xl lg:text-4xl font-extrabold text-slate-900 tracking-tight mb-4">
            Đánh giá từ sinh viên và giảng viên
          </h2>
          <p className="text-base text-slate-600 leading-relaxed">
            Hàng ngàn sinh viên công nghệ thông tin đã cải thiện kỹ năng lập trình nhờ hệ thống bài tập thực tế và sự hỗ trợ sát sao.
          </p>
        </div>

        {/* Testimonials 3 Columns Grid */}
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 lg:gap-8">
          {testimonials.map((item) => (
            <TestimonialCard key={item.id} testimonial={item} />
          ))}
        </div>
      </div>
    </section>
  )
}
