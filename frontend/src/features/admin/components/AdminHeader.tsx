import { AppSidebar } from '../../home/AppSidebar';
import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { Bell, ChevronDown, LogOut, PanelLeft, Search, Settings, Moon, Sun } from 'lucide-react';
import { useAuthStore } from '../../auth/store/authStore';
import { useLogout } from '../../auth/hooks/useAuth';
import { adminNavigation } from './AdminSidebar';
interface Props { onToggleSidebar: () => void; isMobileSidebarOpen: boolean; theme: 'light' | 'dark'; onToggleTheme: () => void }
export function AdminHeader({ onToggleSidebar, isMobileSidebarOpen, theme, onToggleTheme }: Props) {
  const user = useAuthStore((state) => state.user);
  const logout = useLogout();
  const [panel, setPanel] = useState<'search' | 'notifications' | 'profile' | 'settings' | null>(null);
  const [query, setQuery] = useState('');
  const headerRef = useRef<HTMLElement>(null);
  const searchRef = useRef<HTMLInputElement>(null);
  const profileRef = useRef<HTMLButtonElement>(null);
  const settingsRef = useRef<HTMLButtonElement>(null);
  const bellRef = useRef<HTMLButtonElement>(null);
  const normalize = (value: string) => value.toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd');
  const results = adminNavigation.filter((item) => normalize(item.label).includes(normalize(query.trim())));
  useEffect(() => {
    const outside = (event: PointerEvent) => { if (!headerRef.current?.contains(event.target as Node)) setPanel(null); };
    const keyboard = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') { event.preventDefault(); searchRef.current?.focus(); setPanel('search'); }
      if (event.key === 'Escape') {
        if (panel === 'settings') settingsRef.current?.focus();
        if (panel === 'profile') profileRef.current?.focus();
        if (panel === 'notifications') bellRef.current?.focus();
        setPanel(null);
      }
    };
    document.addEventListener('pointerdown', outside);
    document.addEventListener('keydown', keyboard);
    return () => { document.removeEventListener('pointerdown', outside); document.removeEventListener('keydown', keyboard); };
  }, [panel]);
  return <header ref={headerRef} className="sticky top-0 z-30 flex min-h-14 flex-wrap items-center py-1 sm:h-14 sm:flex-nowrap gap-2 border-b border-[#e8e8eb] bg-white px-3 sm:gap-[15px] sm:px-5 lg:gap-6" onBlur={(event) => { if (!event.currentTarget.contains(event.relatedTarget)) setPanel(null); }}>
    <div className="flex shrink-0 items-center gap-3 lg:w-[236px]">
      <AppSidebar />
    <Link to="/admin/dashboard" className="inline-flex h-11 shrink-0 items-center text-[22px] font-bold leading-none tracking-[-0.7px] text-neutral-900" aria-label="CodePart — Tổng quan">CodePart</Link>
    </div>

    <button type="button" className="inline-flex size-8 shrink-0 items-center justify-center rounded-md text-zinc-600 hover:bg-zinc-100 lg:hidden" onClick={onToggleSidebar} aria-label="Mở sidebar quản trị" aria-controls="admin-mobile-sidebar" aria-expanded={isMobileSidebarOpen}><PanelLeft size={18} /></button>
    <div className="relative flex h-[35px] min-w-0 flex-1 items-center rounded-md border border-[#dedee3] text-[#808087] shadow-[0_1px_2px_#00000005] focus-within:outline-2 focus-within:outline-offset-2 focus-within:outline-zinc-400 sm:max-w-96 max-sm:order-last max-sm:basis-full max-sm:grow-0">
      <input className="w-full min-w-0 border-0 bg-transparent px-2.5 py-2 text-xs text-neutral-800 focus-visible:outline-none" ref={searchRef} value={query} onFocus={() => setPanel('search')} onChange={(event) => { setQuery(event.target.value); setPanel('search'); }} placeholder="Tìm chức năng..." aria-label="Tìm chức năng quản trị" aria-controls="admin-search-results" aria-expanded={panel === 'search'} />
      <button type="button" className="mr-1 inline-flex size-8 shrink-0 items-center justify-center rounded-md hover:bg-zinc-100" aria-label="Tìm kiếm" onClick={() => { searchRef.current?.focus(); setPanel('search'); }}><Search size={17} /></button>
      {panel === 'search' && <div id="admin-search-results" className="absolute top-[calc(100%+12px)] z-40 rounded-[10px] border border-zinc-200 bg-white text-neutral-800 shadow-[0_10px_30px_#00000012] left-0 w-[max(100%,240px)] max-w-[calc(100vw-100px)] p-[7px]"><p className="m-0 px-[9px] py-1.5 text-[10px] tracking-[.06em] text-[#85858c]">ĐIỀU HƯỚNG NHANH</p>{results.map(({ label, to, icon: Icon }) => <Link className="flex w-full items-center gap-2.5 rounded-[5px] border-0 bg-transparent px-[9px] py-[11px] text-left text-xs text-neutral-700 no-underline hover:bg-zinc-100" key={to} to={to} onClick={() => { setPanel(null); setQuery(''); }}><Icon size={17} />{label}</Link>)}{!results.length && <p className="m-0 p-2 text-xs leading-[1.7] text-neutral-500">Không tìm thấy chức năng phù hợp.</p>}</div>}
    </div>
    <div className="ml-auto flex items-center gap-[5px] sm:gap-2.5">
      <div className="relative">
        <button ref={settingsRef} type="button" className="admin-header-icon" title="Cài đặt" aria-label="Cài đặt" aria-expanded={panel === 'settings'} aria-controls="admin-settings" onClick={() => setPanel(panel === 'settings' ? null : 'settings')}><Settings size={18} /></button>
        {panel === 'settings' && <div id="admin-settings" className="absolute -right-24 top-11 w-60 rounded-xl border border-zinc-200 bg-white p-4 text-neutral-800 shadow-lg sm:right-0"><strong className="text-sm">Cài đặt giao diện</strong><button type="button" className="mt-3 flex w-full items-center justify-between rounded-lg bg-zinc-100 p-3 text-xs" onClick={onToggleTheme}><span>Chế độ {theme === 'dark' ? 'tối' : 'sáng'}</span>{theme === 'dark' ? <Moon size={16} /> : <Sun size={16} />}</button></div>}
      </div>
      <button type="button" className="admin-header-icon" title={theme === 'dark' ? 'Chuyển sang giao diện sáng' : 'Chuyển sang giao diện tối'} aria-label={theme === 'dark' ? 'Chuyển sang giao diện sáng' : 'Chuyển sang giao diện tối'} aria-pressed={theme === 'dark'} onClick={onToggleTheme}>{theme === 'dark' ? <Sun size={18} /> : <Moon size={18} />}</button>
      <div className="relative">
        <button ref={bellRef} type="button" className="inline-flex size-8 shrink-0 items-center justify-center rounded-md border-0 bg-transparent text-zinc-600 hover:bg-[#f1f1f3]" aria-label="Thông báo" aria-expanded={panel === 'notifications'} aria-controls="admin-notifications" onClick={() => setPanel(panel === 'notifications' ? null : 'notifications')}><Bell size={18} /></button>
        {panel === 'notifications' && <div id="admin-notifications" className="absolute top-[calc(100%+12px)] z-40 rounded-[10px] border border-zinc-200 bg-white text-neutral-800 shadow-[0_10px_30px_#00000012] -right-[43px] w-[270px] max-w-[calc(100vw-32px)] p-[17px] sm:right-0"><strong>Thông báo</strong><p className="m-0 p-2 text-xs leading-[1.7] text-neutral-500">Thông báo hệ thống chưa được kết nối.</p></div>}
      </div>
      <div className="relative">
        <button ref={profileRef} type="button" className="flex items-center gap-[7px] rounded-[7px] border-0 bg-transparent p-[3px] text-neutral-500 hover:bg-[#f1f1f3] [&>svg]:hidden sm:[&>svg]:block" aria-label="Tài khoản quản trị" aria-expanded={panel === 'profile'} aria-controls="admin-profile" onClick={() => setPanel(panel === 'profile' ? null : 'profile')}><span className="grid size-8 shrink-0 place-items-center rounded-full bg-[#e8e3db] text-xs font-bold text-[#655947]">{user?.fullName?.trim().charAt(0).toUpperCase() || 'A'}</span><span className="hidden max-w-24 truncate text-xs font-medium text-neutral-700 sm:block xl:max-w-40">{user?.fullName || user?.email || 'Tài khoản'}</span><ChevronDown size={14} /></button>
        {panel === 'profile' && <div id="admin-profile" className="absolute top-[calc(100%+12px)] z-40 rounded-[10px] border border-zinc-200 bg-white text-neutral-800 shadow-[0_10px_30px_#00000012] right-0 w-[270px] max-w-[calc(100vw-32px)] p-[7px]"><div className="mb-[5px] flex flex-col gap-[5px] border-b border-[#ededed] p-2.5 [overflow-wrap:anywhere] [&_span]:text-xs [&_span]:text-neutral-500 [&_small]:text-xs [&_small]:text-neutral-500"><strong>{user?.fullName || 'Quản trị viên'}</strong><span>{user?.email}</span><small>Quản trị viên</small></div><button type="button" className="flex w-full items-center gap-2.5 rounded-[5px] border-0 bg-transparent px-[9px] py-[11px] text-left text-xs text-neutral-700 no-underline hover:bg-zinc-100" onClick={() => logout.mutate()} disabled={logout.isPending}><LogOut size={16} />{logout.isPending ? 'Đang đăng xuất...' : 'Đăng xuất'}</button></div>}
      </div>
    </div>
  </header>;
}
