import React from 'react'
import { Layers, Bot, Calendar, ArrowRight } from 'lucide-react'
import type { CourseDetail } from '../../types/landing'

interface CourseCardProps {
  course: CourseDetail
  onRegisterCourse: (course: CourseDetail) => void
}

export const CourseCard: React.FC<CourseCardProps> = ({
  course,
  onRegisterCourse,
}) => {
  return (
    <div className="flex flex-col bg-white border border-slate-200 rounded-xl p-6 sm:p-7 hover:shadow-lg hover:border-slate-300 transition duration-200">
      {/* Top Badges */}
      <div className="flex items-center justify-between gap-2 mb-4">
        <span className="px-2.5 py-1 text-xs font-bold rounded-md bg-slate-100 text-slate-800 tracking-wider">
          {course.tag}
        </span>
        <span
          className={`text-xs font-semibold ${
            course.seatsUrgent ? 'text-rose-600 font-bold' : 'text-amber-600'
          }`}
        >
          {course.seatsAvailableText}
        </span>
      </div>

      {/* Course Title */}
      <h3 className="text-xl font-bold text-slate-900 tracking-tight mb-2 min-h-14">
        {course.title}
      </h3>

      {/* Description */}
      <p className="text-sm text-slate-600 leading-relaxed mb-6 min-h-18">
        {course.description}
      </p>

      {/* Specifications list */}
      <div className="space-y-3 pt-5 border-t border-slate-100 mb-8 flex-1">
        <div className="flex items-start gap-2.5 text-xs sm:text-sm text-slate-600">
          <Layers className="w-4 h-4 text-blue-600 shrink-0 mt-0.5" />
          <span>
            <strong className="font-semibold text-slate-800">Số lượng: </strong>
            {course.exerciseCount}
          </span>
        </div>

        <div className="flex items-start gap-2.5 text-xs sm:text-sm text-slate-600">
          <Bot className="w-4 h-4 text-blue-600 shrink-0 mt-0.5" />
          <span>
            <strong className="font-semibold text-slate-800">Hỗ trợ: </strong>
            {course.supportType}
          </span>
        </div>

        <div className="flex items-start gap-2.5 text-xs sm:text-sm text-slate-600">
          <Calendar className="w-4 h-4 text-blue-600 shrink-0 mt-0.5" />
          <span>
            <strong className="font-semibold text-slate-800">{course.scheduleType}: </strong>
            {course.scheduleText}
          </span>
        </div>
      </div>

      {/* Registration Button */}
      <button
        onClick={() => onRegisterCourse(course)}
        className="w-full inline-flex items-center justify-center gap-2 py-3 px-4 bg-blue-600 hover:bg-blue-700 text-white font-semibold text-sm rounded-lg shadow-sm hover:shadow transition duration-150"
      >
        <span>Đăng ký học phần</span>
        <ArrowRight className="w-4 h-4" />
      </button>
    </div>
  )
}
