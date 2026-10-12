import { GraduationCap, Presentation, ArrowUpRight } from 'lucide-react'
import { SectionHeading } from './SectionHeading'

export function AudienceSection({ onStart }: { onStart: () => void }) {
  return (
    <section className="landing-section landing-audience">
      <div className="landing-container">
        <SectionHeading eyebrow="HỌC CÙNG NHAU" title="Hai góc nhìn." accent="Một mục tiêu tiến bộ." />
        <div className="audience-grid">
          <article data-reveal>
            <GraduationCap size={34} strokeWidth={1.4} aria-hidden="true" />
            <h3>Chủ động hơn khi học.</h3>
            <p className="landing-eyebrow">DÀNH CHO SINH VIÊN</p>
            <p>Tập trung vào bài làm, xem lại kết quả và nhận ra điều cần luyện tập. Mỗi lần nộp bài là một cơ hội để hiểu sâu hơn.</p>
            <button className="landing-text-link" onClick={onStart}>Bắt đầu thực hành <ArrowUpRight size={18} aria-hidden="true" /></button>
          </article>
          <article data-reveal>
            <Presentation size={34} strokeWidth={1.4} aria-hidden="true" />
            <h3>Gần hơn với từng tiến bộ.</h3>
            <p className="landing-eyebrow">DÀNH CHO GIẢNG VIÊN</p>
            <p>Tổ chức bài tập theo lớp, theo dõi bài nộp và dành thời gian cho những nhận xét có ích. Đồng hành cùng sinh viên trong quá trình học.</p>
            <a className="landing-text-link" href="#system-features">Tìm hiểu hệ thống <ArrowUpRight size={18} aria-hidden="true" /></a>
          </article>
        </div>
      </div>
    </section>
  )
}
