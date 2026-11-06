import { useEffect, useId, useRef, useState } from 'react';
import { Bell, CheckCheck, Trash2, X } from 'lucide-react';
import { useAuthStore } from '../auth/store/authStore';
import { useNotifications } from './api';

export function NotificationBell() {
  const user = useAuthStore(state => state.user);
  return user?.status === 'Active' ? <NotificationInbox key={user.id} userId={user.id} /> : null;
}

function NotificationInbox({ userId }: { userId: string }) {
  const [open, setOpen] = useState(false);
  const [unreadOnly, setUnreadOnly] = useState(false);
  const root = useRef<HTMLDivElement>(null);
  const trigger = useRef<HTMLButtonElement>(null);
  const panelId = useId();
  const { count, list, update } = useNotifications(userId, open, unreadOnly);
  const items = list.data?.pages.flatMap(page => page.items) ?? [];

  useEffect(() => {
    if (!open) return;
    const outside = (event: PointerEvent) => {
      if (!root.current?.contains(event.target as Node)) setOpen(false);
    };
    const escape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') { setOpen(false); trigger.current?.focus(); }
    };
    document.addEventListener('pointerdown', outside);
    document.addEventListener('keydown', escape);
    return () => {
      document.removeEventListener('pointerdown', outside);
      document.removeEventListener('keydown', escape);
    };
  }, [open]);

  return <div ref={root} className="relative shrink-0" onBlur={event => {
    if (event.relatedTarget && !event.currentTarget.contains(event.relatedTarget)) setOpen(false);
  }}>
    <button ref={trigger} type="button" onClick={() => setOpen(value => !value)}
      className="relative inline-flex size-10 items-center justify-center rounded-full text-slate-600 transition hover:bg-white/70 focus-visible:outline-2 focus-visible:outline-sky-600"
      aria-label={count.data ? `Thông báo, ${count.data} chưa đọc` : 'Thông báo'} aria-expanded={open} aria-controls={panelId}>
      <Bell size={21} />
      {!!count.data && <span className="absolute -right-1 -top-0.5 min-w-4 rounded-full bg-red-500 px-1 text-center text-[10px] font-bold leading-4 text-white">{count.data > 99 ? '99+' : count.data}</span>}
      {count.isError && <span className="absolute right-0 top-0 size-2 rounded-full bg-amber-500" title="Chưa tải được số thông báo" />}
    </button>
    {open && <section id={panelId} aria-label="Danh sách thông báo" className="fixed right-3 top-20 z-50 w-[380px] max-w-[calc(100vw-24px)] overflow-hidden rounded-2xl border border-slate-200 bg-white text-left text-slate-800 shadow-xl sm:absolute sm:-right-12 sm:top-[calc(100%+12px)]">
      <div className="flex items-center justify-between border-b border-slate-100 px-4 py-3">
        <h2 className="text-base font-semibold">Thông báo</h2>
        <button type="button" aria-label="Đóng thông báo" className="rounded p-1 hover:bg-slate-100" onClick={() => { setOpen(false); trigger.current?.focus(); }}><X size={18} /></button>
      </div>
      <div className="flex items-center justify-between gap-2 border-b border-slate-100 p-3 text-xs">
        <div className="flex gap-1">
          {[false, true].map(value => <button key={String(value)} type="button" aria-pressed={unreadOnly === value} onClick={() => setUnreadOnly(value)} className={`rounded-full px-3 py-1.5 ${unreadOnly === value ? 'bg-sky-100 text-sky-800' : 'hover:bg-slate-100'}`}>{value ? 'Chưa đọc' : 'Tất cả'}</button>)}
        </div>
        <button type="button" className="inline-flex items-center gap-1 text-sky-700 disabled:opacity-40" disabled={!count.data || update.isPending} onClick={() => update.mutate({ type: 'read-all' })}><CheckCheck size={15} />Đọc tất cả</button>
      </div>
      {update.isError && <p role="alert" className="px-4 py-2 text-xs text-red-600">Không thể cập nhật thông báo. Vui lòng thử lại.</p>}
      {count.isError && <p role="alert" className="px-4 py-2 text-xs text-amber-700">Chưa tải được số chưa đọc. <button type="button" className="underline" onClick={() => void count.refetch()}>Thử lại</button></p>}
      <div className="max-h-[min(420px,60dvh)] overflow-y-auto overscroll-contain">
        {list.isPending && <p role="status" className="p-6 text-center text-sm text-slate-500">Đang tải thông báo...</p>}
        {list.isError && <div role="alert" className="p-4 text-center text-sm text-red-600">Không tải được thông báo. <button type="button" className="underline" onClick={() => void list.refetch()}>Thử lại</button></div>}
        {list.isSuccess && items.length === 0 && <div className="px-4 py-10 text-center text-sm text-slate-500"><Bell className="mx-auto mb-3 text-slate-300" size={30} />{unreadOnly ? 'Bạn đã đọc hết thông báo.' : 'Bạn chưa có thông báo nào.'}</div>}
        {items.map(item => <article key={item.id} className={`border-b border-slate-100 p-4 ${item.isRead ? '' : 'bg-sky-50/70'}`}>
          <div className="flex items-start gap-2">
            {!item.isRead && <span className="mt-1.5 size-2 shrink-0 rounded-full bg-sky-500" aria-label="Chưa đọc" />}
            <div className="min-w-0 flex-1">
              <h3 className="break-words text-sm font-semibold">{item.title}</h3>
              <p className="mt-1 whitespace-pre-wrap break-words text-xs leading-relaxed text-slate-600">{item.body}</p>
              <time dateTime={item.createdAt} className="mt-2 block text-[11px] text-slate-400">{new Date(item.createdAt).toLocaleString('vi-VN')}</time>
              {!item.isRead && <button type="button" disabled={update.isPending} className="mt-2 text-xs text-sky-700 disabled:opacity-40" onClick={() => update.mutate({ type: 'read', id: item.id })}>Đánh dấu đã đọc</button>}
            </div>
            <button type="button" aria-label={`Xóa thông báo: ${item.title}`} disabled={update.isPending} className="rounded p-1 text-slate-400 hover:bg-red-50 hover:text-red-600 disabled:opacity-40" onClick={() => update.mutate({ type: 'delete', id: item.id })}><Trash2 size={15} /></button>
          </div>
        </article>)}
        {list.hasNextPage && <button type="button" disabled={list.isFetching} className="w-full p-3 text-sm text-sky-700 disabled:opacity-40" onClick={() => void list.fetchNextPage()}>{list.isFetchingNextPage ? 'Đang tải...' : 'Xem thêm'}</button>}
      </div>
    </section>}
  </div>;
}
