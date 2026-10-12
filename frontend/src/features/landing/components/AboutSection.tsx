import { ArrowUpRight, Check, Sprout } from 'lucide-react'
import { SectionHeading } from './SectionHeading'

export function AboutSection() {
  return (
    <section id="about" className="landing-section landing-about">
      <div className="landing-container landing-split">
        <figure className="about-photo" data-reveal>
          <img src="/images/codepart-study-group.jpg" alt="Nhóm sinh viên cùng trao đổi và thực hành trên laptop trong không gian học tập sáng sủa" width="1448" height="1086" loading="lazy" decoding="async" />
          <figcaption className="about-photo__caption"><span><Sprout size={24} aria-hidden="true" /></span><div><strong>Học. Thử. Hiểu.</strong><p>Kiến thức lớn lên từ thực hành.</p></div></figcaption>
        </figure>
        <div>
          <SectionHeading align="left" eyebrow="VỀ CODEPART" title="Không chỉ viết code." accent="Học cách tư duy." description="Lập trình bắt đầu từ sự tò mò và tiến bộ qua từng lần thử. CodePart tạo một không gian tập trung để bạn thực hành, hiểu lỗi sai và tự tin giải quyết bài toán tiếp theo." />
          <ul className="landing-checklist">
            <li><Check size={18} aria-hidden="true" /> Học gắn với bài tập và mục tiêu rõ ràng.</li>
            <li><Check size={18} aria-hidden="true" /> Phản hồi giúp bạn hiểu, không chỉ biết điểm.</li>
            <li><Check size={18} aria-hidden="true" /> Kết nối việc tự học với hướng dẫn từ giảng viên.</li>
          </ul>
          <a className="landing-text-link" href="#workflow">Khám phá cách học <ArrowUpRight size={18} aria-hidden="true" /></a>
        </div>
      </div>
    </section>
  )
}
