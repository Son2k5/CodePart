import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { ArrowLeft, Sparkles } from 'lucide-react';
interface BrightAuthLayoutProps {
  children: ReactNode;
  title: string;
  subtitle: string;
  variant: 'login' | 'register' | 'forgot' | 'reset' | 'verify';
}

const messages = {
  reset: ['Luôn có cách để tiếp tục.', 'Một mật khẩu mới.', 'Tiếp tục hành trình.', 'Bảo vệ tài khoản và trở lại học tập cùng CodePart.'],
  verify: ['Chỉ còn một bước nữa.', 'Xác thực email.', 'Sẵn sàng khám phá.', 'Hoàn tất xác thực để bắt đầu hành trình học lập trình cùng CodePart.'],
  login: ['Mỗi ngày một bước tiến.', 'Tiếp tục hành trình', 'chinh phục lập trình.', 'Thực hành, khám phá và phát triển kỹ năng của bạn cùng CodePart.'],
  register: ['Một khởi đầu, nhiều khả năng.', 'Bắt đầu hôm nay.', 'Tự tin viết ngày mai.', 'Cùng học hỏi và biến những ý tưởng đầu tiên thành dòng code của riêng bạn.'],
  forgot: ['Luôn có cách để tiếp tục.', 'Một bước nhỏ để', 'trở lại hành trình.', 'Khôi phục mật khẩu và tiếp tục khám phá thế giới lập trình cùng CodePart.'],
};

export function BrightAuthLayout({ children, title, subtitle, variant }: BrightAuthLayoutProps) {
  const [eyebrow, headline, emphasis, description] = messages[variant];
  return (
    <div className="min-h-screen bg-[radial-gradient(ellipse_at_5%_55%,#eff9e9_0%,transparent_36%),radial-gradient(ellipse_at_95%_0%,#d9f2fb_0%,transparent_48%),#fbfdfd] text-[#40565b] font-sans">
      <header className="max-w-[1240px] mx-auto px-5 sm:px-10 py-[30px] flex items-center justify-between gap-5">
        <Link to="/" className="inline-flex items-center gap-2.5 text-[#596963] text-[20px] min-[381px]:text-[23px] font-[650] tracking-[-0.7px]" aria-label="CodePart — Trang chủ">CodePart</Link>
        <Link to="/" className="inline-flex items-center gap-2 text-[#59757b] text-[11px] min-[381px]:text-[13px] hover:text-[#2e626d] transition-colors"><ArrowLeft size={16} /> Về trang chủ</Link>
      </header>
      <main className="w-[min(500px,calc(100%-40px))] min-[801px]:w-[min(1120px,calc(100%-80px))] min-h-auto min-[801px]:min-h-[calc(100vh-168px)] mx-auto py-7 min-[801px]:py-[28px_40px] grid grid-cols-1 min-[801px]:grid-cols-2 items-center gap-7 min-[801px]:gap-[clamp(32px,6vw,90px)]">
        <section className="hidden min-[801px]:block min-w-0 py-[14px]" aria-label="Học lập trình cùng CodePart">
          <div className="inline-flex gap-2 items-center text-[#567a81] text-xs [&>svg]:text-[#a88618]"><Sparkles size={15} /> {eyebrow}</div>
          <h2 className="mt-6 text-[clamp(30px,3.2vw,43px)] font-[350] leading-[1.3] tracking-[-1.5px] text-[#686a68]">{headline}<strong className="block font-[650] text-[#558b93]">{emphasis}</strong></h2>
          <p className="max-w-[390px] mt-[18px] text-sm leading-[1.9] text-[#687d81]">{description}</p>
          <div className="relative isolate mx-[-24px] mt-2.5 before:content-[''] before:absolute before:-z-10 before:inset-[10%_8%_4%] before:rounded-[49%_51%_61%_39%] before:bg-linear-[140deg,#e2f4fa,#c9edf5] [&>img]:w-full [&>img]:h-auto [&>img]:max-h-[310px] [&>img]:object-contain"><img src="/education-hero.svg" alt="Laptop, sách và bóng đèn ý tưởng cùng cây xanh" width="900" height="650" /></div>
          <div className="flex items-center justify-center gap-3 mt-3.5 text-xs text-[#687d81]"><span className="inline-flex gap-[5px] [&>i]:w-[7px] [&>i]:h-[7px] [&>i]:rounded-full [&>i]:bg-[#a9dfe9] [&>i:nth-child(2)]:bg-[#ffd642] [&>i:nth-child(3)]:bg-[#7bd14e]" aria-hidden="true"><i /><i /><i /></span> Học từng bước. Tiến xa hơn.</div>
        </section>
        <section className="min-w-0 w-full p-6 min-[381px]:p-[30px] min-[801px]:p-[clamp(26px,3.5vw,44px)] bg-white border border-[#e5eff0] rounded-[26px] shadow-[0_18px_65px_#446b760a]" aria-labelledby="auth-title">
          <div className="mb-7"><span className="text-[10px] tracking-[2px] text-[#558b93] font-[650] block">CÙNG CODEPART</span><h1 id="auth-title" className="mt-3 text-[clamp(26px,2.5vw,32px)] text-[#455b55] font-[650] tracking-[-1px] leading-[1.3]">{title}</h1><p className="mt-2.5 text-[#6b7f83] text-[13px] leading-[1.8] [overflow-wrap:anywhere]">{subtitle}</p></div>
          {children}
        </section>
      </main>
      <footer className="text-center p-4 min-[801px]:p-[16px_24px_24px] text-[11px] text-[#6b7f83]">CodePart <span aria-hidden="true" className="mx-2">·</span> Không gian học tập, khởi nguồn ý tưởng.</footer>
    </div>
  );
}
