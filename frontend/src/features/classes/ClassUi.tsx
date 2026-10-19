import { useEffect, useRef, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { adminTeacherApi, teacherError } from '../admin/api/adminTeacherApi';
import { useDebounced, type Teacher } from './api';

export function QueryState({ loading, error, empty, retry }: { loading: boolean; error: unknown; empty?: boolean; retry: () => unknown }) {
  if (loading) return <p role="status">Đang tải dữ liệu…</p>;
  if (error) return <div role="alert">{teacherError(error)} <button onClick={retry}>Thử lại</button></div>;
  if (empty) return <p>Chưa có dữ liệu phù hợp.</p>;
  return null;
}
export function Pagination({ page, totalPages, totalCount, onChange }: { page: number; totalPages: number; totalCount: number; onChange: (n: number) => void }) {
  return <div className="class-toolbar"><span>{totalCount} kết quả · Trang {page}/{Math.max(1, totalPages)}</span>
    <button disabled={page <= 1} onClick={() => onChange(page - 1)}>Trước</button>
    <button disabled={page >= totalPages} onClick={() => onChange(page + 1)}>Sau</button></div>;
}
export function ConfirmDialog({ title, message, pending, onClose, onConfirm }: { title: string; message: string; pending: boolean; onClose: () => void; onConfirm: () => void }) {
  const ref = useRef<HTMLDialogElement>(null);
  useEffect(() => { ref.current?.showModal(); }, []);
  return <dialog className="class-dialog classes-page" ref={ref} onCancel={event => { event.preventDefault(); if (!pending) onClose(); }} aria-labelledby="class-confirm-title">
    <h2 id="class-confirm-title">{title}</h2><p>{message}</p><div className="class-toolbar">
      <button disabled={pending} onClick={onClose}>Hủy</button><button className="danger" disabled={pending} onClick={onConfirm}>{pending ? 'Đang xử lý…' : 'Xác nhận'}</button>
    </div></dialog>;
}
export function TeacherPicker({ value, onChange }: { value: Teacher[]; onChange: (teachers: Teacher[]) => void }) {
  const [search, setSearch] = useState(''); const [page, setPage] = useState(1); const debounced = useDebounced(search);
  const query = useQuery({ queryKey: ['admin-teachers', 'class-picker', debounced, page], queryFn: ({ signal }) => adminTeacherApi.list({ search: debounced, page, pageSize: 10 }, signal) });
  return <fieldset><legend>Giáo viên (không bắt buộc)</legend>
    <div className="class-toolbar">{value.map(t => <button type="button" key={t.id} onClick={() => onChange(value.filter(x => x.id !== t.id))}>{t.fullName} ×</button>)}</div>
    <input aria-label="Tìm giáo viên" placeholder="Tìm tên hoặc email giáo viên" value={search} onChange={e => { setSearch(e.target.value); setPage(1); }} />
    <QueryState loading={query.isLoading} error={query.error} empty={query.data?.items.length === 0} retry={() => query.refetch()} />
    <div className="teacher-options">{query.data?.items.map(t => <label key={t.id}>
      <input type="checkbox" checked={value.some(x => x.id === t.id)} onChange={e => onChange(e.target.checked ? [...value, { id: t.id, fullName: t.fullName, email: t.email }] : value.filter(x => x.id !== t.id))} /> {t.fullName} — {t.email}
    </label>)}</div>
    <div className="class-toolbar"><button type="button" disabled={page === 1} onClick={() => setPage(page - 1)}>Trước</button><span>Trang {page}</span><button type="button" disabled={!query.data || page >= query.data.totalPages} onClick={() => setPage(page + 1)}>Sau</button></div>
  </fieldset>;
}
