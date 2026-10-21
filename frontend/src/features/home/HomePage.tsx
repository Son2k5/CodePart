import { Link, NavLink } from 'react-router-dom';
import { ArrowRight, BookOpen, Code2, Heart, Users } from 'lucide-react';
import { WelcomeBanner } from './WelcomeBanner';
import { AccountMenu } from './AccountMenu';
import '@fontsource/be-vietnam-pro/400.css';
import '@fontsource/be-vietnam-pro/500.css';
import '@fontsource/be-vietnam-pro/600.css';
import '@fontsource/be-vietnam-pro/700.css';
import { AppSidebar } from './AppSidebar';

export function HomePage() {
  return <div className="min-h-screen bg-[#fafcf9] font-['Be_Vietnam_Pro',sans-serif] text-sm text-[#263f39]">
    <a className="fixed -top-24 left-4 z-[100] rounded-lg bg-white p-3 focus:top-3" href="#member-main">Đến nội dung chính</a>
    <header className="relative z-20 bg-linear-[90deg,#fff_0%,#fff_49%,#d9f2fb_68%,#b6eaf5_100%] font-sans">
      <div className="relative mx-auto flex min-h-[72px] max-w-[1360px] flex-wrap items-center justify-between gap-3 px-4 py-3 min-[400px]:px-6 min-[761px]:h-[80px] min-[761px]:flex-nowrap min-[761px]:gap-6 min-[761px]:px-8 min-[761px]:py-0 min-[1101px]:h-[92px] min-[1101px]:gap-9 min-[1101px]:px-14">
        <div className="flex shrink-0 items-center gap-[15px] text-[#777] min-[761px]:gap-[25px]">
          <AppSidebar />
          <Link to="/home" className="text-[21px] font-[650] tracking-[-0.7px] min-[761px]:text-[23px]" aria-label="CodePart — Trang chủ">CodePart</Link>
        </div>
        <nav className="ml-auto hidden items-center gap-[22px] min-[761px]:flex min-[1101px]:gap-[38px]" aria-label="Điều hướng chính">
          {[
            { to: '/home', label: 'Trang chủ' },
            { to: '/courses', label: 'Khóa học' },
            { to: '/classes', label: 'Lớp học' },
            { to: '/problems', label: 'Luyện tập' },
          ].map(({ to, label }) => <NavLink key={to} to={to} className={({ isActive }) => `whitespace-nowrap py-[7px] text-[10px] tracking-[1px] text-[#727c80] uppercase hover:text-[#3e656b] min-[1101px]:text-xs min-[1101px]:tracking-[1.7px] ${isActive ? 'underline decoration-1 underline-offset-[9px]' : ''}`}>{label}</NavLink>)}
        </nav>
        <AccountMenu />
      </div>
    </header>

    <main id="member-main">
      <WelcomeBanner />
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-10">
      <section className="pt-9" aria-labelledby="explore-title">
        <div className="mb-5 flex items-end justify-between gap-5 [&>div>span]:text-[9px] [&>div>span]:font-semibold [&>div>span]:tracking-[1.7px] [&>div>span]:text-[#718368] [&>span]:hidden [&>span]:text-[11px] [&>span]:text-[#6e786a] md:[&>span]:block"><div><span>BƯỚC TIẾP THEO</span><h2 id="explore-title" className="mt-2 text-xl font-semibold tracking-tight">Hôm nay bạn muốn làm gì?</h2></div><span>Một khởi đầu nhỏ cũng rất đáng tự hào <span aria-hidden="true">✨</span></span></div>
        <div className="grid gap-5 md:grid-cols-3">
          {[
            { to: '/courses', icon: BookOpen, tone: 'bg-[#eaf4e8] text-[#568457]', title: 'Khóa học của tôi', text: 'Mở lớp học phần và tiếp tục bài học của bạn.', action: 'Vào khóa học' },
            { to: '/classes', icon: Users, tone: 'bg-[#fff0e2] text-[#b37e4f]', title: 'Khám phá lớp học', text: 'Tìm lớp phù hợp hoặc tham gia cùng bạn bè.', action: 'Khám phá ngay' },
            { to: '/problems', icon: Code2, tone: 'bg-[#efecfa] text-[#8873b3]', title: 'Thử thách bản thân', text: 'Rèn tư duy với những bài tập lập trình.', action: 'Bắt đầu luyện tập' },
          ].map(({ to, icon: Icon, tone, title, text, action }) => <Link key={to} to={to} className="block rounded-[21px] border border-[#e4eae0] bg-white p-6 transition hover:-translate-y-1 hover:shadow-lg hover:shadow-green-950/5 motion-reduce:transform-none motion-reduce:transition-none">
            <span className={`grid size-12 place-items-center rounded-[14px] ${tone}`}><Icon size={25} /></span><h3 className="mt-4 mb-2 text-[15px] font-semibold">{title}</h3><p className="text-xs leading-[1.8] text-[#7a867c]">{text}</p><span className="mt-6 flex items-center justify-between text-[11px] font-semibold text-[#527357]">{action}<ArrowRight size={18} /></span>
          </Link>)}
        </div>
      </section>
      <footer className="flex flex-col justify-between gap-4 py-7 text-[10px] text-[#71806f] md:flex-row [&>span:last-child]:flex [&>span:last-child]:items-center [&>span:last-child]:gap-2"><span>CodePart · Cùng bạn học lập trình mỗi ngày</span><span>Học bằng sự tò mò, tiến bộ bằng thực hành <Heart size={14} /></span></footer>
      </div>
    </main>
  </div>;
}
