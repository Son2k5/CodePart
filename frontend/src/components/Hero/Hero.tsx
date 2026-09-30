import React from 'react'
import { ArrowRight, Check, PlayCircle, Sparkles, MessageCircleCheck } from 'lucide-react'

interface HeroProps {
  onStartCodingClick: () => void
  onExploreExamplesClick: () => void
}

export const Hero: React.FC<HeroProps> = ({
  onStartCodingClick,
  onExploreExamplesClick,
}) => {
  return (
    <section id="home" className="relative bg-white py-12 lg:py-20 overflow-hidden">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-12 items-center">
          {/* Left Column: Headline & Action */}
          <div className="lg:col-span-7 flex flex-col items-start text-left">
            {/* Tag / Eyebrow */}
            <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-blue-50 border border-blue-200 text-blue-700 text-xs font-bold tracking-wider uppercase mb-5">
              <Sparkles className="w-3.5 h-3.5 text-blue-600" />
              <span>KHOA CÔNG NGHỆ THÔNG TIN • CODEPATH</span>
            </div>

            {/* Main Headline */}
            <h1 className="text-3xl sm:text-4xl lg:text-5xl font-extrabold text-slate-900 tracking-tight leading-tight mb-6">
              Nền tảng thực hành và tự động chấm điểm code cho sinh viên CNTT
            </h1>

            {/* Subtitle */}
            <p className="text-base sm:text-lg text-slate-600 leading-relaxed mb-8 max-w-2xl">
              Sinh viên thực hành các học phần Java, OOP, Python, JS trực tiếp trên hệ thống. Tự động chấm điểm test case và được giảng viên/trợ giảng review code chi tiết.
            </p>

            {/* CTA Buttons */}
            <div className="flex flex-wrap items-center gap-4 mb-8 w-full sm:w-auto">
              <button
                onClick={onStartCodingClick}
                className="w-full sm:w-auto inline-flex items-center justify-center gap-2.5 px-6 py-3.5 bg-blue-600 hover:bg-blue-700 text-white font-semibold text-sm sm:text-base rounded-xl shadow-md hover:shadow-lg transition duration-200"
              >
                <span>Bắt đầu làm bài tập ngay</span>
                <ArrowRight className="w-4 h-4" />
              </button>

              <button
                onClick={onExploreExamplesClick}
                className="w-full sm:w-auto inline-flex items-center justify-center gap-2 px-6 py-3.5 bg-white hover:bg-slate-50 text-slate-800 font-semibold text-sm sm:text-base rounded-xl border border-slate-300 hover:border-slate-400 transition duration-200"
              >
                <PlayCircle className="w-4 h-4 text-blue-600" />
                <span>Xem kho bài tập mẫu</span>
              </button>
            </div>

            {/* Trust checkmarks */}
            <div className="flex flex-wrap items-center gap-6 sm:gap-8 pt-2">
              <div className="flex items-center gap-2 text-slate-700 text-sm font-medium">
                <span className="flex items-center justify-center w-5 h-5 rounded-full bg-blue-50 text-blue-600">
                  <Check className="w-3.5 h-3.5 stroke-[2.5]" />
                </span>
                <span>Chấm điểm tự động</span>
              </div>

              <div className="flex items-center gap-2 text-slate-700 text-sm font-medium">
                <span className="flex items-center justify-center w-5 h-5 rounded-full bg-blue-50 text-blue-600">
                  <Check className="w-3.5 h-3.5 stroke-[2.5]" />
                </span>
                <span>Trợ giảng kèm 1:1 khi lỗi</span>
              </div>

              <div className="flex items-center gap-2 text-slate-700 text-sm font-medium">
                <span className="flex items-center justify-center w-5 h-5 rounded-full bg-blue-50 text-blue-600">
                  <Check className="w-3.5 h-3.5 stroke-[2.5]" />
                </span>
                <span>Tiến độ học tập hàng tuần</span>
              </div>
            </div>
          </div>

          {/* Right Column: Hero Visual with Live Overlays */}
          <div className="lg:col-span-5 relative">
            <div className="relative rounded-2xl overflow-hidden border border-slate-200 shadow-xl bg-slate-50">
              <img
                src="/student-hero.jpg"
                alt="Sinh viên lập trình cùng CodePath"
                className="w-full h-auto object-cover aspect-4/3"
                loading="eager"
              />

              {/* Floating Top Badge - Auto Grader */}
              <div className="absolute top-4 left-4 right-4 sm:right-auto bg-white/95 backdrop-blur-md border border-slate-200 rounded-xl p-3 shadow-lg flex items-center gap-3">
                <div className="w-2.5 h-2.5 rounded-full bg-emerald-500 animate-pulse shrink-0" />
                <div className="text-xs">
                  <p className="font-bold text-slate-900">Chấm điểm tự động: 100/100 Điểm</p>
                  <p className="text-slate-500 font-medium">Đã vượt qua 5/5 test cases • 0.04s</p>
                </div>
              </div>

              {/* Floating Bottom Badge - Mentor Feedback */}
              <div className="absolute bottom-4 left-4 right-4 bg-white/95 backdrop-blur-md border border-slate-200 rounded-xl p-3 shadow-lg flex items-start gap-3">
                <div className="w-8 h-8 rounded-lg bg-blue-50 border border-blue-200 flex items-center justify-center text-blue-600 shrink-0">
                  <MessageCircleCheck className="w-4 h-4" />
                </div>
                <div className="text-xs">
                  <div className="flex items-center gap-2">
                    <span className="font-bold text-slate-900">Thầy Hoàng (Trợ giảng 1:1)</span>
                    <span className="text-slate-400">• Vừa xong</span>
                  </div>
                  <p className="text-slate-600 mt-0.5">
                    "Em sửa rất tốt logic điều kiện rẽ nhánh! Cố gắng hoàn thành nốt bài 4 nhé."
                  </p>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  )
}
