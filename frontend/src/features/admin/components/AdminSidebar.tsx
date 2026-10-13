import { useEffect, useRef, useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { ChevronDown, GraduationCap, LayoutDashboard, LogOut, UserCheck, Users, X } from 'lucide-react';
import { useAuthStore } from '../../auth/store/authStore';
import { useLogout } from '../../auth/hooks/useAuth';

export const adminNavigation = [
  { label: 'Tổng quan', to: '/admin/dashboard', icon: LayoutDashboard },
  { label: 'Quản lí sinh viên', to: '/admin/students', icon: GraduationCap },
  { label: 'Quản lí giáo viên', to: '/admin/teachers', icon: UserCheck },
];
interface Props { isCollapsed: boolean; isMobileOpen: boolean; onCloseMobile: () => void }
export function AdminSidebar({ isCollapsed, isMobileOpen, onCloseMobile }: Props) {
  const location = useLocation();
  const user = useAuthStore((state) => state.user);
  const logout = useLogout();
  const dialogRef = useRef<HTMLDialogElement>(null);
  const [personnelOpen, setPersonnelOpen] = useState(() => ['/admin/students', '/admin/teachers'].includes(location.pathname));
  useEffect(() => {
    if (isMobileOpen) dialogRef.current?.showModal();
    else dialogRef.current?.close();
  }, [isMobileOpen]);
  useEffect(() => {
    if (!isMobileOpen) return;
    const previous = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => { document.body.style.overflow = previous; };
  }, [isMobileOpen]);
  const content = (collapsed: boolean, mobile = false) => (
    <div className={`flex h-full flex-col bg-[#f5f5f7] ${collapsed ? 'px-3' : 'px-4'}`}>
      <div className="relative flex h-18 shrink-0 items-center justify-center">
        <Link to="/admin/dashboard" onClick={onCloseMobile} className="inline-flex min-h-11 items-center rounded-md font-bold leading-none text-neutral-900 no-underline transition-opacity hover:opacity-75" aria-label="CodePart — Tổng quan">
          <span className={collapsed ? 'text-lg tracking-[-0.5px]' : 'text-2xl tracking-[-0.8px]'}>{collapsed ? 'CP' : 'CodePart'}</span>
        </Link>
        {mobile && <button type="button" className="absolute right-0 inline-flex size-8 shrink-0 items-center justify-center rounded-md border-0 bg-transparent text-zinc-600 hover:bg-[#f1f1f3]" onClick={onCloseMobile} aria-label="Đóng menu"><X size={18} /></button>}
      </div>
      <nav aria-label="Điều hướng quản trị" className="flex-1 overflow-y-auto pt-[15px]">
        {!collapsed && <p className="mx-3 mb-3.5 text-[10px] tracking-[.1em] text-[#82828a]">QUẢN TRỊ HỆ THỐNG</p>}
        <Link to="/admin/dashboard" onClick={onCloseMobile} title="Tổng quan" aria-label="Tổng quan" aria-current={location.pathname === '/admin/dashboard' ? 'page' : undefined} className={`mb-1 flex min-h-[42px] items-center gap-3 rounded-lg p-3 text-[13px] text-neutral-700 hover:bg-zinc-200 ${collapsed ? 'justify-center' : ''} ${location.pathname === '/admin/dashboard' ? 'bg-zinc-200 font-semibold' : ''}`}><LayoutDashboard size={18} />{!collapsed && 'Tổng quan'}</Link>
        <button type="button" title="Quản lí nhân sự" aria-label="Quản lí nhân sự" aria-expanded={personnelOpen} aria-controls={mobile ? 'mobile-personnel-menu' : 'desktop-personnel-menu'} onClick={() => setPersonnelOpen((open) => !open)} className={`flex min-h-[42px] w-full items-center gap-3 rounded-lg p-3 text-left text-[13px] text-neutral-700 hover:bg-zinc-200 ${collapsed ? 'justify-center' : ''}`}><Users size={18} className="shrink-0" />{!collapsed && <><span className="flex-1">Quản lí nhân sự</span><ChevronDown size={15} className={personnelOpen ? 'rotate-180' : ''} /></>}</button>
        {personnelOpen && <div id={mobile ? 'mobile-personnel-menu' : 'desktop-personnel-menu'} className={collapsed ? 'mt-1' : 'ml-5 mt-1 border-l border-zinc-200 pl-2'}>
          {adminNavigation.slice(1).map(({ label, to }) => <Link key={to} to={to} onClick={onCloseMobile} title={label} aria-label={label} aria-current={location.pathname === to ? 'page' : undefined} className={`mb-1 flex min-h-[42px] items-center rounded-lg p-2.5 text-xs text-neutral-600 hover:bg-zinc-200 ${collapsed ? 'justify-center' : ''} ${location.pathname === to ? 'bg-zinc-200 font-semibold text-neutral-900' : ''}`}>{collapsed ? (to === '/admin/students' ? 'SV' : 'GV') : label}</Link>)}
        </div>}
      </nav>
      <div className={`flex min-h-21 items-center gap-[9px] ${collapsed ? 'justify-center' : ''}`}>
        <span className="grid size-8 shrink-0 place-items-center rounded-full bg-[#e8e3db] text-xs font-bold text-[#655947]" title={user?.fullName}>{user?.fullName?.trim().charAt(0).toUpperCase() || 'A'}</span>
        {!collapsed && <div className="min-w-0 flex-1 [&_strong]:block [&_strong]:truncate [&_strong]:text-xs [&_strong]:font-medium [&_span]:mt-[3px] [&_span]:block [&_span]:truncate [&_span]:text-[10px] [&_span]:font-medium [&_span]:text-neutral-500"><strong>{user?.fullName || 'Quản trị viên'}</strong><span>{user?.email}</span></div>}
        {!collapsed && <button type="button" className="inline-flex size-8 shrink-0 items-center justify-center rounded-md border-0 bg-transparent text-zinc-600 hover:bg-[#f1f1f3]" onClick={() => logout.mutate()} disabled={logout.isPending} aria-label="Đăng xuất" title="Đăng xuất"><LogOut size={17} /></button>}
      </div>
    </div>
  );
  return <>
    <aside id="admin-desktop-sidebar" className={`admin-sidebar-motion sticky top-0 hidden h-dvh shrink-0 lg:block ${isCollapsed ? 'w-19' : 'w-64'}`}>{content(isCollapsed)}</aside>
    <dialog ref={dialogRef} id="admin-mobile-sidebar" className="m-0 h-dvh max-h-dvh w-70 max-w-[85vw] border-0 p-0 text-neutral-800 backdrop:bg-zinc-900/40" aria-label="Menu quản trị" onCancel={onCloseMobile} onClose={onCloseMobile} onClick={(event) => { if (event.target === event.currentTarget) onCloseMobile(); }}>{content(false, true)}</dialog>
  </>;
}
