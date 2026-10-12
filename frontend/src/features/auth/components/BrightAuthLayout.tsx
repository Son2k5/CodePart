import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { ArrowLeft, Sparkles } from 'lucide-react';
import '../styles/auth.css';

interface BrightAuthLayoutProps {
  children: ReactNode;
  title: string;
  subtitle: string;
  variant: 'login' | 'register' | 'forgot';
}

const messages = {
  login: ['Mỗi ngày một bước tiến.', 'Tiếp tục hành trình', 'chinh phục lập trình.', 'Thực hành, khám phá và phát triển kỹ năng của bạn cùng CodePart.'],
  register: ['Một khởi đầu, nhiều khả năng.', 'Bắt đầu hôm nay.', 'Tự tin viết ngày mai.', 'Cùng học hỏi và biến những ý tưởng đầu tiên thành dòng code của riêng bạn.'],
  forgot: ['Luôn có cách để tiếp tục.', 'Một bước nhỏ để', 'trở lại hành trình.', 'Khôi phục mật khẩu và tiếp tục khám phá thế giới lập trình cùng CodePart.'],
};

export function BrightAuthLayout({ children, title, subtitle, variant }: BrightAuthLayoutProps) {
  const [eyebrow, headline, emphasis, description] = messages[variant];
  return (
    <div className={`auth-page auth-page--${variant}`}>
      <header className="auth-header">
        <Link to="/" className="auth-brand" aria-label="CodePart — Trang chủ">CodePart</Link>
        <Link to="/" className="auth-home"><ArrowLeft size={16} /> Về trang chủ</Link>
      </header>
      <main className="auth-main">
        <section className="auth-story" aria-label="Học lập trình cùng CodePart">
          <div className="auth-eyebrow"><Sparkles size={15} /> {eyebrow}</div>
          <h2>{headline}<strong>{emphasis}</strong></h2>
          <p>{description}</p>
          <div className="auth-illustration"><img src="/education-hero.svg" alt="Laptop, sách và bóng đèn ý tưởng cùng cây xanh" width="900" height="650" /></div>
          <div className="auth-story-footer"><span className="auth-dots" aria-hidden="true"><i /><i /><i /></span> Học từng bước. Tiến xa hơn.</div>
        </section>
        <section className="auth-card" aria-labelledby="auth-title">
          <div className="auth-card-heading"><span className="auth-kicker">CÙNG CODEPART</span><h1 id="auth-title">{title}</h1><p>{subtitle}</p></div>
          {children}
        </section>
      </main>
      <footer className="auth-footer">CodePart <span aria-hidden="true">·</span> Không gian học tập, khởi nguồn ý tưởng.</footer>
    </div>
  );
}
