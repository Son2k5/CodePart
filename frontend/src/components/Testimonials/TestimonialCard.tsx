import React from 'react'
import { Quote } from 'lucide-react'
import type { Testimonial } from '../../types/landing'

interface TestimonialCardProps {
  testimonial: Testimonial
}

export const TestimonialCard: React.FC<TestimonialCardProps> = ({ testimonial }) => {
  return (
    <div className="flex flex-col justify-between bg-white border border-slate-200 rounded-xl p-6 sm:p-8 hover:shadow-md transition duration-200">
      <div>
        <Quote className="w-8 h-8 text-blue-600/30 mb-4" />
        <p className="text-sm text-slate-700 leading-relaxed italic mb-8">
          {testimonial.quote}
        </p>
      </div>

      <div className="flex items-center gap-3.5 pt-4 border-t border-slate-100">
        <div className="w-10 h-10 rounded-lg bg-blue-50 border border-blue-200 text-blue-700 font-bold text-sm flex items-center justify-center shrink-0">
          {testimonial.avatarInitials}
        </div>
        <div>
          <h4 className="text-sm font-bold text-slate-900 leading-tight">
            {testimonial.authorName}
          </h4>
          <p className="text-xs text-slate-500 mt-0.5">
            {testimonial.authorRole}
          </p>
        </div>
      </div>
    </div>
  )
}
