import { Plus } from 'lucide-react'
import { faqs } from '../data/content'
import { SectionHeading } from './SectionHeading'

export function FaqSection() {
  return (
    <section id="faq" className="landing-section">
      <div className="landing-container landing-faq">
        <SectionHeading align="left" eyebrow="MỘT CHÚT GIẢI ĐÁP" title="Bạn hỏi." accent="CodePart trả lời." description="Những điều cần biết trước khi bắt đầu hành trình học tập của bạn." />
        <div className="faq-list" data-reveal>
          {faqs.map((faq) => (
            <details key={faq.question} name="landing-faq">
              <summary>{faq.question}<Plus size={19} aria-hidden="true" /></summary>
              <p>{faq.answer}</p>
            </details>
          ))}
        </div>
      </div>
    </section>
  )
}
