import { useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { BookOpen, Code2, GraduationCap, Menu, Home, LogOut, Search, Users, X } from 'lucide-react';
import { useLogout } from '../auth/hooks/useAuth';

const apps = [
  { to: '/home', label: 'Bàn làm việc', icon: Home, color: 'from-blue-400 to-blue-600', group: 'Không gian của bạn' },
  { to: '/courses', label: 'Khóa học của tôi', icon: BookOpen, color: 'from-emerald-400 to-emerald-600', group: 'Học tập' },
  { to: '/classes', label: 'Khám phá lớp học', icon: Users, color: 'from-cyan-400 to-sky-600', group: 'Học tập' },
  { to: '/problems', label: 'Luyện tập', icon: Code2, color: 'from-violet-400 to-indigo-600', group: 'Học tập' },
];
const normalize = (value: string) => value.toLocaleLowerCase('vi').normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd').trim();

export function AppSidebar() {
  const dialog = useRef<HTMLDialogElement>(null);
  const [query, setQuery] = useState('');
  const [open, setOpen] = useState(false);
  const logout = useLogout();
  const filtered = apps.filter(app => normalize(app.label).includes(normalize(query)));
  const close = () => dialog.current?.close();

  return <>
    <button type="button" aria-label="Mở danh sách ứng dụng" aria-haspopup="dialog" aria-expanded={open} aria-controls="app-sidebar" onClick={() => { setQuery(''); dialog.current?.showModal(); setOpen(true); }} className="grid min-h-11 min-w-9 shrink-0 place-items-center rounded-lg p-[5px] text-[#8b8f8e] transition-colors hover:bg-[#edf9fa] hover:text-[#3e656b] focus-visible:outline-[#3e656b]">
      <Menu size={27} />
    </button>
    <dialog ref={dialog} id="app-sidebar" aria-labelledby="app-sidebar-title" onClose={() => setOpen(false)} onClick={event => { if (event.target === event.currentTarget) close(); }} className="fixed inset-y-0 left-0 m-0 h-dvh max-h-dvh w-full max-w-[748px] overflow-hidden border-0 bg-transparent p-0 text-slate-800 shadow-2xl backdrop:bg-slate-950/30">
      <div className="flex h-full bg-gradient-to-b from-[#e7ebf3] via-[#f3f3f3] to-[#f3f3f3] font-['Be_Vietnam_Pro',sans-serif]">
        <aside className="flex w-14 shrink-0 flex-col items-center gap-3 py-3 sm:w-16" aria-label="Thao tác nhanh">
          <button type="button" onClick={close} aria-label="Đóng danh sách ứng dụng" className="grid size-10 place-items-center rounded-full bg-white text-red-500 hover:bg-red-50"><X size={24} /></button>
          <Link to="/home" onClick={close} aria-label="Về trang chủ" title="Trang chủ" className="mt-auto grid size-10 place-items-center rounded-full bg-white/70 text-slate-600 hover:bg-white"><Home size={23} /></Link>
          <button type="button" aria-label="Đăng xuất" title="Đăng xuất" disabled={logout.isPending} onClick={() => { close(); logout.mutate(); }} className="grid size-10 place-items-center rounded-full bg-white/70 text-slate-600 hover:bg-white disabled:opacity-50"><LogOut size={22} /></button>
        </aside>
        <div className="flex min-w-0 flex-1 flex-col pr-4 sm:pr-6">
          <div className="relative my-2 shrink-0">
            <Search size={19} className="pointer-events-none absolute top-3 left-3 text-slate-400" />
            <input autoFocus type="search" value={query} onChange={event => setQuery(event.target.value)} aria-label="Tìm kiếm ứng dụng" placeholder="Tìm kiếm..." className="h-11 w-full rounded-full border-2 border-blue-500 bg-white pr-4 pl-9 text-sm outline-none placeholder:text-slate-400 focus:border-blue-600 focus:ring-2 focus:ring-blue-200" />
          </div>
          <div className="min-h-0 flex-1 overflow-y-auto overscroll-contain pb-8">
            <h2 id="app-sidebar-title" className="mt-3 mb-4 text-base font-semibold">{query.trim() ? 'Kết quả tìm kiếm' : 'Truy cập nhanh'}</h2>
            <nav aria-label="Ứng dụng truy cập nhanh" className="grid grid-cols-2 gap-2 sm:grid-cols-4">
              {filtered.map(({ to, label, icon: Icon, color }) => <Link key={to} to={to} onClick={close} className="flex min-h-32 flex-col items-center gap-3 rounded-2xl bg-white/65 px-2 py-5 text-center text-[13px] leading-5 transition-colors hover:bg-white focus-visible:outline-blue-600">
                <span className={`grid size-14 place-items-center rounded-[20px] bg-gradient-to-br ${color} text-white shadow-sm`}><Icon size={31} strokeWidth={2.4} /></span>{label}
              </Link>)}
            </nav>
            {filtered.length === 0 && <p role="status" className="rounded-2xl bg-white/60 p-6 text-sm leading-6 text-slate-500">Không tìm thấy ứng dụng. Thử tìm “khóa học”, “lớp học” hoặc “luyện tập” nhé.</p>}
            {['Không gian của bạn', 'Học tập'].map(group => {
              const items = filtered.filter(app => app.group === group);
              if (!items.length) return null;
              return <section key={group} className="mt-9" aria-label={group}>
                <h3 className="mb-4 text-base font-semibold">{group}</h3>
                <nav aria-label={group} className="grid gap-3 sm:grid-cols-2">
                  {items.map(({ to, label, icon: Icon, color }) => <Link key={to} to={to} onClick={close} className="flex items-center gap-3 rounded-xl p-2 text-[13px] transition-colors hover:bg-white/80">
                    <span className={`grid size-9 shrink-0 place-items-center rounded-xl bg-gradient-to-br ${color} text-white shadow-sm`}><Icon size={22} /></span>{label}
                  </Link>)}
                </nav>
              </section>;
            })}
            <div className="mt-10 flex items-start gap-3 rounded-2xl border border-white bg-white/40 p-4 text-xs leading-6 text-slate-500"><GraduationCap size={25} className="shrink-0 text-blue-500" /><p>CodePart — Không gian học tập của bạn.<br />Chọn một ứng dụng để bắt đầu nhé!</p></div>
          </div>
        </div>
      </div>
    </dialog>
  </>;
}
