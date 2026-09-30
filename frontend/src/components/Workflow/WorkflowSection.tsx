import React from 'react'
import type { WorkflowStep } from '../../types/landing'
import { WorkflowCard } from './WorkflowCard'

interface WorkflowSectionProps {
  steps: WorkflowStep[]
}

export const WorkflowSection: React.FC<WorkflowSectionProps> = ({ steps }) => {
  return (
    <section id="workflow" className="py-16 sm:py-24 bg-white border-t border-slate-200">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        {/* Section Header */}
        <div className="max-w-3xl mb-12 sm:mb-16">
          <span className="text-xs font-bold uppercase tracking-wider text-blue-700 block mb-2">
            QUY TRÌNH HỌC
          </span>
          <h2 className="text-2xl sm:text-3xl lg:text-4xl font-extrabold text-slate-900 tracking-tight mb-4">
            Sinh viên nộp bài thực hành thế nào?
          </h2>
          <p className="text-base text-slate-600 leading-relaxed">
            Mỗi bài tập bám sát đề cương môn học, sinh viên làm trực tiếp trên trình duyệt và nhận phản hồi chi tiết từ hệ thống cũng như giảng viên.
          </p>
        </div>

        {/* 3 Step Cards Grid */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6 lg:gap-8">
          {steps.map((step) => (
            <WorkflowCard key={step.id} step={step} />
          ))}
        </div>
      </div>
    </section>
  )
}
