import { ArrowRight } from 'lucide-react'

export function StartSection({ onStart, onLogin }: { onStart: () => void; onLogin: () => void }) {
  return (
    <section id="contact" className="landing-start">
      <div className="landing-container" data-reveal>
        <p className="landing-eyebrow">BƯỚC TIẾP THEO LÀ CỦA BẠN</p>
        <h2>Một dòng code hôm nay.<br /><strong>Một bước tiến ngày mai.</strong></h2>
        <p>Mở không gian học tập của bạn và bắt đầu cùng CodePart.</p>
        <button className="landing-primary-button" onClick={onStart}>Tạo tài khoản <ArrowRight size={18} aria-hidden="true" /></button>
        <span className="landing-start__login">Đã có tài khoản? <button onClick={onLogin}>Đăng nhập</button></span>
      </div>
    </section>
  )
}
