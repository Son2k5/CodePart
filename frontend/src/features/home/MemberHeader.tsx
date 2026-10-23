import { Link, NavLink } from 'react-router-dom';
import { AppSidebar } from './AppSidebar';
import { AccountMenu } from './AccountMenu';
import { useAuthStore } from '../auth/store/authStore';

export function MemberHeader() {
  const isTeacher = useAuthStore(state => state.user?.role === 'Teacher');
  return (
    <header className="relative z-20 bg-linear-[90deg,#fff_0%,#fff_49%,#d9f2fb_68%,#b6eaf5_100%] font-sans">
      <div className="relative mx-auto flex min-h-[72px] max-w-[1360px] flex-wrap items-center justify-between gap-3 px-4 py-3 min-[400px]:px-6 min-[761px]:h-[80px] min-[761px]:flex-nowrap min-[761px]:gap-6 min-[761px]:px-8 min-[761px]:py-0 min-[1101px]:h-[92px] min-[1101px]:gap-9 min-[1101px]:px-14">
        <div className="flex shrink-0 items-center gap-[15px] text-[#777] min-[761px]:gap-[25px]">
          <AppSidebar />
          <Link to={isTeacher ? '/teacher' : '/home'} className="text-[21px] font-[650] tracking-[-0.7px] min-[761px]:text-[23px]" aria-label="CodePart — Trang chủ">CodePart</Link>
        </div>
        <nav className="ml-auto hidden items-center gap-[22px] min-[761px]:flex min-[1101px]:gap-[38px]" aria-label="Điều hướng chính">
          {[
            { to: isTeacher ? '/teacher' : '/home', label: 'Trang chủ' },
            { to: isTeacher ? '/teacher/courses' : '/courses', label: 'Khóa học' },
            { to: isTeacher ? '/teacher/classes' : '/classes', label: 'Lớp học' },
            ...(!isTeacher ? [{ to: '/problems', label: 'Luyện tập' }] : []),
          ].map(({ to, label }) => <NavLink key={to} to={to} end={to === "/teacher"} className={({ isActive }) => `whitespace-nowrap py-[7px] text-[10px] tracking-[1px] text-[#727c80] uppercase hover:text-[#3e656b] min-[1101px]:text-xs min-[1101px]:tracking-[1.7px] ${isActive ? 'underline decoration-1 underline-offset-[9px]' : ''}`}>{label}</NavLink>)}
        </nav>
        <AccountMenu />
      </div>
    </header>
  );
}
