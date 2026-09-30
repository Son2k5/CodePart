import React, { useState } from 'react'
import { ArrowRight } from 'lucide-react'
import type { CourseDetail } from '../../types/landing'
import { CourseCard } from './CourseCard'

interface CoursesSectionProps {
  courses: CourseDetail[]
  onRegisterCourse: (course: CourseDetail) => void
  onConsultClick: () => void
}

export const CoursesSection: React.FC<CoursesSectionProps> = ({
  courses,
  onRegisterCourse,
  onConsultClick,
}) => {
  const [filter, setFilter] = useState<'all' | 'basic' | 'advanced' | 'applied'>('all')

  const filteredCourses =
    filter === 'all'
      ? courses
      : courses.filter((c) => c.tagVariant === filter)

  return (
    <section id="courses" className="py-16 sm:py-24 bg-white border-t border-slate-200">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        {/* Section Header with Consultation Link */}
        <div className="flex flex-col md:flex-row md:items-end justify-between mb-8 sm:mb-12 gap-6">
          <div className="max-w-2xl">
            <span className="text-xs font-bold uppercase tracking-wider text-blue-700 block mb-2">
              HỌC PHẦN THỰC HÀNH
            </span>
            <h2 className="text-2xl sm:text-3xl lg:text-4xl font-extrabold text-slate-900 tracking-tight mb-3">
              Các lớp thực hành đang mở
            </h2>
            <p className="text-base text-slate-600 leading-relaxed">
              Hệ thống bài tập phân loại theo từng cấp độ kỹ năng, đảm bảo sinh viên nắm vững nền tảng trước khi bước vào các học phần chuyên sâu.
            </p>
          </div>

          <button
            onClick={onConsultClick}
            className="inline-flex items-center gap-1.5 text-sm font-bold text-blue-600 hover:text-blue-700 transition group self-start md:self-auto shrink-0"
          >
            <span>Tư vấn đăng ký lớp</span>
            <ArrowRight className="w-4 h-4 group-hover:translate-x-1 transition-transform" />
          </button>
        </div>

        {/* Filter Pills */}
        <div className="flex items-center gap-2 mb-8 overflow-x-auto pb-2">
          <button
            onClick={() => setFilter('all')}
            className={`px-4 py-2 text-xs sm:text-sm font-semibold rounded-lg transition whitespace-nowrap ${
              filter === 'all'
                ? 'bg-blue-600 text-white shadow-sm'
                : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
            }`}
          >
            Tất cả học phần
          </button>
          <button
            onClick={() => setFilter('basic')}
            className={`px-4 py-2 text-xs sm:text-sm font-semibold rounded-lg transition whitespace-nowrap ${
              filter === 'basic'
                ? 'bg-blue-600 text-white shadow-sm'
                : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
            }`}
          >
            Cơ sở ngành
          </button>
          <button
            onClick={() => setFilter('advanced')}
            className={`px-4 py-2 text-xs sm:text-sm font-semibold rounded-lg transition whitespace-nowrap ${
              filter === 'advanced'
                ? 'bg-blue-600 text-white shadow-sm'
                : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
            }`}
          >
            Chuyên ngành & Chuyên sâu
          </button>
          <button
            onClick={() => setFilter('applied')}
            className={`px-4 py-2 text-xs sm:text-sm font-semibold rounded-lg transition whitespace-nowrap ${
              filter === 'applied'
                ? 'bg-blue-600 text-white shadow-sm'
                : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
            }`}
          >
            Thực hành Ứng dụng
          </button>
        </div>

        {/* Courses Grid */}
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 lg:gap-8">
          {filteredCourses.map((course) => (
            <CourseCard
              key={course.id}
              course={course}
              onRegisterCourse={onRegisterCourse}
            />
          ))}
        </div>
      </div>
    </section>
  )
}
