import { useEffect, useRef, useState, type FormEvent } from 'react';
import { ChevronDown, LogOut, UserRoundPen, X } from 'lucide-react';
import { toast } from 'sonner';
import { useAuthStore } from '../auth/store/authStore';
import { useLogout } from '../auth/hooks/useAuth';
import { axiosInstance } from '../../lib/axios';
import { getErrorMessage } from '../../lib/errorService';
import type { User } from '../auth/types/auth.types';

export function AccountMenu() {
  const user = useAuthStore(state => state.user);
  const logout = useLogout();
  const menu = useRef<HTMLDetailsElement>(null);
  const trigger = useRef<HTMLElement>(null);
  const dialog = useRef<HTMLDialogElement>(null);
  const [fullName, setFullName] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const name = user?.fullName.trim() || 'Tài khoản';
  const initials = name.split(/\s+/).slice(-2).map(part => part[0]).join('').toLocaleUpperCase('vi');

  useEffect(() => {
    const dismiss = (event: PointerEvent) => {
      if (menu.current && !menu.current.contains(event.target as Node)) menu.current.open = false;
    };
    document.addEventListener('pointerdown', dismiss);
    return () => document.removeEventListener('pointerdown', dismiss);
  }, []);

  const edit = () => {
    if (menu.current) menu.current.open = false;
    setFullName(user?.fullName || '');
    setError('');
    dialog.current?.showModal();
  };
  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (saving) return;
    if (!fullName.trim()) { setError('Vui lòng nhập họ và tên.'); return; }
    setSaving(true);
    setError('');
    try {
      const { data } = await axiosInstance.put<User>('/auth/me', { fullName: fullName.trim() });
      const session = useAuthStore.getState();
      if (session.user?.id === data.id && session.accessToken) {
        session.setAuth({ ...session.user, ...data }, session.accessToken);
        toast.success('Đã cập nhật thông tin.');
        dialog.current?.close();
      }
    } catch (cause) { setError(getErrorMessage(cause)); }
    finally { setSaving(false); }
  }

  return <div className="relative ml-auto shrink-0 min-[761px]:ml-0">
    <details ref={menu} className="relative" onBlur={event => {
      if (!event.currentTarget.contains(event.relatedTarget)) event.currentTarget.open = false;
    }} onKeyDown={event => {
      if (event.key === 'Escape' && menu.current?.open) { menu.current.open = false; trigger.current?.focus(); }
    }}>
      <summary ref={trigger} aria-label={`Tài khoản của ${name}`} className="flex min-h-11 cursor-pointer list-none items-center gap-2 rounded-full p-1 pr-3 text-[#3e656b] hover:bg-white/60 [&::-webkit-details-marker]:hidden">
        <span className="grid size-9 shrink-0 place-items-center rounded-full border-2 border-white bg-[#edf9fa] text-xs font-bold" aria-hidden="true">{initials}</span>
        <strong className="hidden max-w-28 truncate text-xs min-[1101px]:block" title={name}>{name}</strong><ChevronDown size={16} />
      </summary>
      <div className="absolute top-full right-0 z-30 mt-3 w-60 max-w-[calc(100vw-2rem)] overflow-hidden rounded-2xl border border-[#dfecee] bg-white p-2 shadow-xl shadow-slate-900/10">
        <div className="border-b border-slate-100 px-3 py-3"><p className="truncate text-sm font-semibold text-[#3e656b]">{name}</p><p className="mt-1 truncate text-xs text-slate-500">{user?.email}</p></div>
        <button type="button" onClick={edit} className="mt-1 flex w-full items-center gap-3 rounded-xl px-3 py-3 text-left text-sm text-[#3e656b] hover:bg-[#edf9fa]"><UserRoundPen size={18} />Chỉnh sửa thông tin</button>
        <button type="button" disabled={logout.isPending} onClick={() => logout.mutate()} className="flex w-full items-center gap-3 rounded-xl px-3 py-3 text-left text-sm text-red-600 hover:bg-red-50 disabled:opacity-50"><LogOut size={18} />{logout.isPending ? 'Đang đăng xuất…' : 'Đăng xuất'}</button>
      </div>
    </details>
    <dialog ref={dialog} aria-labelledby="profile-title" onClose={() => trigger.current?.focus()} onCancel={event => { if (saving) event.preventDefault(); }} className="fixed inset-0 m-auto max-h-[90dvh] w-[calc(100%-2rem)] max-w-md overflow-y-auto rounded-3xl border-0 bg-white p-6 text-[#3e656b] shadow-2xl backdrop:bg-slate-900/30 sm:p-8">
      <div className="mb-6 flex items-center justify-between gap-3"><h2 id="profile-title" className="text-xl font-semibold">Chỉnh sửa thông tin</h2><button type="button" aria-label="Đóng" disabled={saving} onClick={() => dialog.current?.close()} className="grid size-10 shrink-0 place-items-center rounded-full hover:bg-slate-100 disabled:opacity-50"><X size={20} /></button></div>
      <form onSubmit={save} className="grid gap-5">
        <label className="grid gap-2 text-sm font-medium">Họ và tên<input autoFocus required maxLength={200} autoComplete="name" value={fullName} disabled={saving} onChange={event => setFullName(event.target.value)} className="w-full rounded-xl border border-[#cddfe3] px-4 py-3 font-normal outline-none focus:border-[#397c8c] focus:ring-2 focus:ring-[#edf9fa]" /></label>
        <label className="grid gap-2 text-sm font-medium">Email<input type="email" readOnly value={user?.email || ''} className="w-full rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 font-normal text-slate-500" /><span className="text-xs font-normal text-slate-500">Email đăng nhập không thay đổi tại đây.</span></label>
        {error && <p role="alert" className="text-sm text-red-600">{error}</p>}
        <div className="mt-1 flex justify-end gap-3"><button type="button" disabled={saving} onClick={() => dialog.current?.close()} className="rounded-full border border-[#cddfe3] px-5 py-2.5 text-sm hover:bg-slate-50 disabled:opacity-50">Hủy</button><button type="submit" disabled={saving} className="rounded-full bg-[#397955] px-5 py-2.5 text-sm font-semibold text-white hover:bg-[#2f6547] disabled:cursor-wait disabled:opacity-50">{saving ? 'Đang lưu…' : 'Lưu thay đổi'}</button></div>
      </form>
    </dialog>
  </div>;
}
