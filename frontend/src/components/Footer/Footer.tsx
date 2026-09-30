import React from 'react'
import { Code2, MapPin, Phone, Mail, ShieldCheck } from 'lucide-react'
import { FOOTER_DATA } from '../../data/landingData'

export const Footer: React.FC = () => {
  return (
    <footer id="contact" className="bg-white border-t border-slate-200 pt-16 pb-12">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-12 gap-10 lg:gap-8 pb-12 border-b border-slate-200">
          {/* Brand Col */}
          <div className="lg:col-span-4">
            <div className="flex items-center gap-2.5 mb-4">
              <div className="w-8 h-8 bg-blue-600 rounded-lg flex items-center justify-center text-white shadow-sm">
                <Code2 className="w-4 h-4 stroke-[2.5]" />
              </div>
              <div className="flex items-baseline gap-1">
                <span className="text-xl font-extrabold tracking-tight text-slate-900">CodePath</span>
                <span className="text-xs font-bold text-blue-600 tracking-wider uppercase">Academy</span>
              </div>
            </div>

            <p className="text-sm text-slate-600 leading-relaxed mb-5 max-w-sm">
              {FOOTER_DATA.about}
            </p>

            <div className="inline-flex items-center gap-2 px-3 py-1.5 bg-blue-50 border border-blue-200 rounded-lg text-blue-700 text-xs font-bold">
              <ShieldCheck className="w-4 h-4 text-blue-600" />
              <span>{FOOTER_DATA.standardBadge}</span>
            </div>
          </div>

          {/* Col 1: Chương trình đào tạo */}
          <div className="lg:col-span-3">
            <h4 className="text-xs font-bold uppercase tracking-wider text-slate-900 mb-4">
              Chương trình đào tạo
            </h4>
            <ul className="space-y-2.5">
              {FOOTER_DATA.curriculums.map((item, idx) => (
                <li key={idx}>
                  <a href="#courses" className="text-sm text-slate-600 hover:text-blue-600 transition">
                    {item}
                  </a>
                </li>
              ))}
            </ul>
          </div>

          {/* Col 2: Hệ thống & Quy trình */}
          <div className="lg:col-span-2">
            <h4 className="text-xs font-bold uppercase tracking-wider text-slate-900 mb-4">
              Hệ thống & Quy trình
            </h4>
            <ul className="space-y-2.5">
              {FOOTER_DATA.systemProcesses.map((item, idx) => (
                <li key={idx}>
                  <a href="#workflow" className="text-sm text-slate-600 hover:text-blue-600 transition">
                    {item}
                  </a>
                </li>
              ))}
            </ul>
          </div>

          {/* Col 3: Liên hệ hỗ trợ */}
          <div className="lg:col-span-3">
            <h4 className="text-xs font-bold uppercase tracking-wider text-slate-900 mb-4">
              Liên hệ hỗ trợ
            </h4>
            <ul className="space-y-3">
              <li className="flex items-start gap-2.5 text-sm text-slate-600">
                <MapPin className="w-4 h-4 text-blue-600 shrink-0 mt-0.5" />
                <span>{FOOTER_DATA.contact.address}</span>
              </li>
              <li className="flex items-center gap-2.5 text-sm text-slate-600">
                <Phone className="w-4 h-4 text-blue-600 shrink-0" />
                <a href={`tel:${FOOTER_DATA.contact.hotline.replace(/\s+/g, '')}`} className="hover:text-blue-600">
                  {FOOTER_DATA.contact.hotline}
                </a>
              </li>
              <li className="flex items-center gap-2.5 text-sm text-slate-600">
                <Mail className="w-4 h-4 text-blue-600 shrink-0" />
                <a href={`mailto:${FOOTER_DATA.contact.email}`} className="hover:text-blue-600">
                  {FOOTER_DATA.contact.email}
                </a>
              </li>
            </ul>
          </div>
        </div>

        {/* Bottom copyright bar */}
        <div className="pt-8 flex flex-col sm:flex-row items-center justify-between gap-4 text-xs text-slate-500">
          <p>© 2026 CodePath Academy. Toàn bộ bản quyền được bảo lưu.</p>
          <div className="flex items-center gap-6">
            <a href="#terms" className="hover:text-blue-600 transition">Điều khoản dịch vụ</a>
            <a href="#privacy" className="hover:text-blue-600 transition">Chính sách bảo mật</a>
            <a href="#rules" className="hover:text-blue-600 transition">Quy định nộp bài</a>
          </div>
        </div>
      </div>
    </footer>
  )
}
