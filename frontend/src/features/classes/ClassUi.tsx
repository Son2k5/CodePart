import { useEffect, useRef, useState } from 'react';
import { Search, ChevronLeft, ChevronRight } from 'lucide-react';
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
  const selectPageRef = useRef<HTMLInputElement>(null);
  const teachers = query.data?.items ?? [];
  const selectedOnPage = teachers.filter(t => value.some(selected => selected.id === t.id)).length;
  useEffect(() => {
    if (selectPageRef.current) selectPageRef.current.indeterminate = selectedOnPage > 0 && selectedOnPage < teachers.length;
  }, [selectedOnPage, teachers.length]);
  const selectPage = (checked: boolean) => {
    if (checked) {
      onChange([...value, ...teachers.filter(t => !value.some(selected => selected.id === t.id)).map(t => ({ id: t.id, fullName: t.fullName, email: t.email }))]);
    } else {
      onChange(value.filter(selected => !teachers.some(t => t.id === selected.id)));
    }
  };
  return <fieldset className="class-teacher-picker"><legend>Phân công giáo viên</legend>
    <div className="teacher-picker-selected">{value.map(t => <button type="button" key={t.id} onClick={() => onChange(value.filter(x => x.id !== t.id))}>{t.fullName} ×</button>)}</div>
    <div className="teacher-picker-toolbar"><label className="teacher-picker-search"><Search size={16} aria-hidden="true" /><input aria-label="Tìm giáo viên" placeholder="Tìm tên hoặc email..." value={search} onChange={e => { setSearch(e.target.value); setPage(1); }} /></label><span>Đã chọn <strong>{value.length}</strong> giáo viên</span></div>
    <div className="teacher-picker-table"><table><colgroup><col style={{ width: 48 }} /><col style={{ width: 64 }} /><col style={{ width: '30%' }} /><col /><col style={{ width: 130 }} /></colgroup><thead><tr><th scope="col"><input ref={selectPageRef} type="checkbox" aria-label="Chọn tất cả giáo viên trên trang" checked={teachers.length > 0 && selectedOnPage === teachers.length} disabled={!teachers.length || query.isFetching} onChange={e => selectPage(e.target.checked)} /></th><th scope="col" className="teacher-picker-number">STT</th><th scope="col">Giáo viên</th><th scope="col">Email trường</th><th scope="col">Phân công</th></tr></thead><tbody>{(query.isLoading || query.isError || query.data?.items.length === 0) && <tr><td colSpan={5} className="teacher-picker-empty">{query.isLoading ? <span role="status">Đang tải giáo viên…</span> : query.isError ? <span role="alert">{teacherError(query.error)} <button type="button" onClick={() => void query.refetch()}>Thử lại</button></span> : 'Không tìm thấy giáo viên phù hợp.'}</td></tr>}{query.data?.items.map((t, index) => <tr key={t.id} className={value.some(x => x.id === t.id) ? 'is-selected' : ''}>
      <td><input type="checkbox" aria-label={`Chọn ${t.fullName}`} checked={value.some(x => x.id === t.id)} onChange={e => onChange(e.target.checked ? [...value, { id: t.id, fullName: t.fullName, email: t.email }] : value.filter(x => x.id !== t.id))} /></td><td className="teacher-picker-number">{(page - 1) * 10 + index + 1}</td><td title={t.fullName}><strong>{t.fullName}</strong></td><td title={t.email}>{t.email}</td><td><span className={`teacher-picker-status ${value.some(x => x.id === t.id) ? 'is-selected' : ''}`}>{value.some(x => x.id === t.id) ? 'Đã chọn' : 'Chưa chọn'}</span></td>
    </tr>)}</tbody></table></div>
    <div className="teacher-picker-pagination"><span>Tổng số: <strong>{query.data?.totalCount ?? 0}</strong> giáo viên</span><div><span aria-live="polite">Trang {page}/{Math.max(1, query.data?.totalPages ?? 1)}</span><button type="button" aria-label="Trang giáo viên trước" disabled={page === 1 || query.isFetching} onClick={() => setPage(page - 1)}><ChevronLeft size={16} /></button><button type="button" aria-label="Trang giáo viên sau" disabled={!query.data || page >= query.data.totalPages || query.isFetching} onClick={() => setPage(page + 1)}><ChevronRight size={16} /></button></div></div>
  </fieldset>;
}
