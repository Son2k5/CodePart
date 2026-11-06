import { useEffect, useRef } from 'react';
import { Link } from 'react-router-dom';
import { ChevronDown, LogOut, UserRound } from 'lucide-react';
import { useAuthStore } from '../auth/store/authStore';
import { useLogout } from '../auth/hooks/useAuth';

export function AccountMenu() {
  const user = useAuthStore((state) => state.user);
  const logout = useLogout();
  const menu = useRef<HTMLDetailsElement>(null);
  const trigger = useRef<HTMLElement>(null);
  const name = user?.fullName.trim() || 'Tài khoản';
  const initials = name
    .split(/\s+/)
    .slice(-2)
    .map((part) => part[0])
    .join('')
    .toLocaleUpperCase('vi');

  useEffect(() => {
    const dismiss = (event: PointerEvent) => {
      if (menu.current && !menu.current.contains(event.target as Node)) menu.current.open = false;
    };
    document.addEventListener('pointerdown', dismiss);
    return () => document.removeEventListener('pointerdown', dismiss);
  }, []);

  const closeMenu = () => {
    if (menu.current) menu.current.open = false;
  };

  return (
    <div className="relative ml-auto shrink-0 min-[761px]:ml-0">
      <details
        ref={menu}
        className="relative"
        onBlur={(event) => {
          if (!event.currentTarget.contains(event.relatedTarget)) event.currentTarget.open = false;
        }}
        onKeyDown={(event) => {
          if (event.key === 'Escape' && menu.current?.open) {
            menu.current.open = false;
            trigger.current?.focus();
          }
        }}
      >
        <summary
          ref={trigger}
          aria-label={`Tài khoản của ${name}`}
          className="flex min-h-11 cursor-pointer list-none items-center gap-2 rounded-full p-1 pr-3 text-[#3e656b] hover:bg-white/60 [&::-webkit-details-marker]:hidden"
        >
          <span className="grid size-9 shrink-0 place-items-center rounded-full border-2 border-white bg-[#edf9fa] text-xs font-bold" aria-hidden="true">
            {initials}
          </span>
          <strong className="hidden max-w-28 truncate text-xs min-[1101px]:block" title={name}>
            {name}
          </strong>
          <ChevronDown size={16} />
        </summary>
        <div className="absolute top-full right-0 z-30 mt-3 w-60 max-w-[calc(100vw-2rem)] overflow-hidden rounded-2xl border border-[#dfecee] bg-white p-2 shadow-xl shadow-slate-900/10">
          <div className="border-b border-slate-100 px-3 py-3">
            <p className="truncate text-sm font-semibold text-[#3e656b]">{name}</p>
            <p className="mt-1 truncate text-xs text-slate-500">{user?.email}</p>
          </div>
          <Link
            to="/profile"
            onClick={closeMenu}
            className="mt-1 flex w-full items-center gap-3 rounded-xl px-3 py-3 text-left text-sm text-[#3e656b] hover:bg-[#edf9fa]"
          >
            <UserRound size={18} />
            Hồ sơ của tôi
          </Link>
          <button
            type="button"
            disabled={logout.isPending}
            onClick={() => logout.mutate()}
            className="flex w-full items-center gap-3 rounded-xl px-3 py-3 text-left text-sm text-red-600 hover:bg-red-50 disabled:opacity-50"
          >
            <LogOut size={18} />
            {logout.isPending ? 'Đang đăng xuất…' : 'Đăng xuất'}
          </button>
        </div>
      </details>
    </div>
  );
}
