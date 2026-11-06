import { useEffect, useRef, useState } from 'react';
import { Search, ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight, ChevronDown, AlertCircle, X, RotateCcw } from 'lucide-react';
import { useQuery } from '@tanstack/react-query';
import { adminTeacherApi, teacherError } from '../admin/api/adminTeacherApi';
import { useDebounced, type Teacher } from './api';
import '../admin/styles/personnel.css';

export function QueryState({ loading, error, empty, retry }: { loading: boolean; error: unknown; empty?: boolean; retry: () => unknown }) {
  if (loading) {
    return (
      <div className="py-6 flex items-center justify-center gap-2 text-slate-500 text-sm font-medium" role="status">
        <span className="inline-block w-4 h-4 border-2 border-slate-400 border-t-transparent rounded-full animate-spin" />
        <span>Đang tải dữ liệu…</span>
      </div>
    );
  }
  if (error) {
    return (
      <div className="my-3 p-3.5 border border-rose-200 bg-rose-50 text-rose-800 rounded-md flex items-center justify-between gap-3 text-sm" role="alert">
        <div className="flex items-center gap-2">
          <AlertCircle size={16} className="text-rose-600 shrink-0" />
          <span>{teacherError(error)}</span>
        </div>
        <button
          type="button"
          onClick={retry}
          className="px-3 py-1 bg-white border border-rose-300 text-rose-700 rounded-md text-xs font-semibold hover:bg-rose-50 transition-colors cursor-pointer"
        >
          Thử lại
        </button>
      </div>
    );
  }
  if (empty) {
    return (
      <div className="py-8 text-center text-slate-500 text-sm border border-dashed border-slate-200 rounded-md bg-slate-50/50 my-3">
        Chưa có dữ liệu phù hợp.
      </div>
    );
  }
  return null;
}

export function Pagination({ page, totalPages, totalCount, onChange }: { page: number; totalPages: number; totalCount: number; onChange: (n: number) => void }) {
  return (
    <div className="flex items-center justify-between flex-wrap gap-3 py-3 px-1 border-t border-slate-200 text-xs text-slate-600">
      <span>
        Tổng số: <strong className="font-semibold text-slate-900">{totalCount}</strong> kết quả · Trang {page}/{Math.max(1, totalPages)}
      </span>
      <div className="flex items-center gap-2">
        <button
          type="button"
          disabled={page <= 1}
          onClick={() => onChange(page - 1)}
          className="px-3 py-1.5 border border-slate-300 rounded-md bg-white text-slate-700 hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed font-medium text-xs transition-colors cursor-pointer"
        >
          Trước
        </button>
        <button
          type="button"
          disabled={page >= totalPages}
          onClick={() => onChange(page + 1)}
          className="px-3 py-1.5 border border-slate-300 rounded-md bg-white text-slate-700 hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed font-medium text-xs transition-colors cursor-pointer"
        >
          Sau
        </button>
      </div>
    </div>
  );
}

export function ConfirmDialog({ title, message, pending, onClose, onConfirm }: { title: string; message: string; pending: boolean; onClose: () => void; onConfirm: () => void }) {
  const ref = useRef<HTMLDialogElement>(null);
  useEffect(() => { ref.current?.showModal(); }, []);

  return (
    <dialog
      ref={ref}
      onCancel={event => { event.preventDefault(); if (!pending) onClose(); }}
      aria-labelledby="class-confirm-title"
      className="fixed inset-0 m-auto z-50 w-full max-w-md p-6 bg-white border border-slate-200 rounded-md shadow-xl backdrop:bg-slate-900/30"
    >
      <h2 id="class-confirm-title" className="text-base font-bold text-slate-900 mb-2">
        {title}
      </h2>
      <p className="text-sm text-slate-600 mb-6 leading-relaxed">
        {message}
      </p>
      <div className="flex justify-end gap-2.5">
        <button
          type="button"
          disabled={pending}
          onClick={onClose}
          className="px-3.5 py-2 text-xs font-semibold border border-slate-300 rounded-md bg-white text-slate-700 hover:bg-slate-50 disabled:opacity-50 transition-colors cursor-pointer"
        >
          Hủy
        </button>
        <button
          type="button"
          disabled={pending}
          onClick={onConfirm}
          className="px-3.5 py-2 text-xs font-semibold rounded-md bg-rose-600 hover:bg-rose-700 text-white disabled:opacity-50 transition-colors cursor-pointer"
        >
          {pending ? 'Đang xử lý…' : 'Xác nhận'}
        </button>
      </div>
    </dialog>
  );
}

const avatarColors = ['#35b6cf', '#55bb70', '#9a7cf0', '#f58b53', '#ed7099', '#5b92d9'];
const getInitials = (name: string) => {
  const parts = name.trim().split(' ').filter(Boolean);
  if (parts.length >= 2) {
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  }
  return name.slice(0, 2).toUpperCase();
};

export function TeacherPicker({ value, onChange }: { value: Teacher[]; onChange: (teachers: Teacher[]) => void }) {
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const debounced = useDebounced(search);
  const query = useQuery({
    queryKey: ['admin-teachers', 'class-picker', debounced, page, pageSize],
    queryFn: ({ signal }) => adminTeacherApi.list({ search: debounced, page, pageSize }, signal),
  });
  const selectPageRef = useRef<HTMLInputElement>(null);
  const teachers = query.data?.items ?? [];
  const totalCount = query.data?.totalCount ?? 0;
  const totalPages = Math.max(1, query.data?.totalPages ?? 1);
  const start = (page - 1) * pageSize;
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

  return (
    <div className="personnel-page personnel-management class-teacher-picker text-slate-800 !min-h-0 !p-0 !bg-transparent">
      <section className="personnel-grid bg-white border border-slate-200/90 rounded-none overflow-hidden shadow-none" aria-label="Bảng chọn giảng viên">
        <div className="personnel-toolbar">
          <label className="personnel-search">
            <Search size={14} />
            <input
              aria-label="Tìm giáo viên"
              placeholder="Tìm theo tên hoặc email giáo viên..."
              value={search}
              onChange={e => { setSearch(e.target.value); setPage(1); }}
            />
            {search && (
              <button type="button" aria-label="Xóa tìm kiếm" onClick={() => { setSearch(''); setPage(1); }}>
                <X size={13} />
              </button>
            )}
          </label>

          {value.length > 0 && (
            <button
              type="button"
              className="personnel-selection-badge"
              onClick={() => onChange([])}
            >
              Đã chọn {value.length} giáo viên
              <X size={12} />
            </button>
          )}

          <div className="personnel-toolbar-actions">
            {value.length > 0 && (
              <button
                type="button"
                onClick={() => onChange([])}
                className="px-2.5 py-1 text-xs text-slate-600 hover:text-slate-900 hover:bg-slate-100 rounded-none transition-colors cursor-pointer"
              >
                Bỏ chọn tất cả
              </button>
            )}
            <button
              type="button"
              className="personnel-icon class-tool-button"
              title="Làm mới"
              onClick={() => void query.refetch()}
            >
              <RotateCcw size={14} />
              <span>Làm mới</span>
            </button>
          </div>
        </div>

        {/* Selected chips row */}
        {value.length > 0 && (
          <div className="px-3.5 py-2 bg-blue-50/50 border-b border-blue-100 flex flex-wrap gap-1.5 items-center">
            <span className="text-[11px] font-semibold text-blue-900 mr-1">Đang chọn:</span>
            {value.map(t => (
              <span
                key={t.id}
                className="inline-flex items-center gap-1.5 px-2 py-0.5 text-xs font-semibold bg-white border border-blue-200 text-blue-700 rounded-none"
              >
                {t.fullName}
                <button
                  type="button"
                  aria-label={`Bỏ chọn ${t.fullName}`}
                  onClick={() => onChange(value.filter(x => x.id !== t.id))}
                  className="personnel-chip-remove text-slate-400 hover:text-rose-600 font-bold ml-0.5 cursor-pointer leading-none"
                >
                  ×
                </button>
              </span>
            ))}
          </div>
        )}

        <div className="personnel-table-scroll !min-h-0 !h-auto max-h-[300px]">
          <table style={{ width: '100%', minWidth: 600 }}>
            <colgroup>
              <col style={{ width: 44 }} />
              <col style={{ width: 56 }} />
              <col style={{ minWidth: 200 }} />
              <col style={{ minWidth: 220 }} />
              <col style={{ width: 140 }} />
              <col style={{ width: 120 }} />
            </colgroup>
            <thead>
              <tr>
                <th className="personnel-checkbox-cell">
                  <input
                    ref={selectPageRef}
                    type="checkbox"
                    aria-label="Chọn tất cả giáo viên trên trang"
                    checked={teachers.length > 0 && selectedOnPage === teachers.length}
                    disabled={!teachers.length || query.isFetching}
                    onChange={e => selectPage(e.target.checked)}
                  />
                </th>
                <th scope="col" className="class-number-cell">STT</th>
                <th scope="col"><span className="personnel-column-title">Giáo viên</span></th>
                <th scope="col"><span className="personnel-column-title">Email trường</span></th>
                <th scope="col"><span className="personnel-column-title">Khoa / Đơn vị</span></th>
                <th scope="col" className="personnel-status-cell"><span className="personnel-column-title">Trạng thái</span></th>
              </tr>
            </thead>
            <tbody>
              {query.isPending && (
                <tr>
                  <td colSpan={6} className="p-8 text-center text-slate-500">
                    Đang tải danh sách giáo viên…
                  </td>
                </tr>
              )}
              {query.isError && (
                <tr>
                  <td colSpan={6} className="p-6 text-center text-rose-600">
                    {teacherError(query.error)}{' '}
                    <button type="button" onClick={() => void query.refetch()} className="underline ml-2 cursor-pointer">Thử lại</button>
                  </td>
                </tr>
              )}
              {!query.isPending && !query.isError && teachers.length === 0 && (
                <tr>
                  <td colSpan={6}>
                    <div className="personnel-empty">
                      <Search size={28} />
                      <strong>Không tìm thấy giáo viên</strong>
                      <p>Thử tìm kiếm với từ khóa khác.</p>
                      {search && (
                        <button type="button" onClick={() => { setSearch(''); setPage(1); }}>Đặt lại tìm kiếm</button>
                      )}
                    </div>
                  </td>
                </tr>
              )}
              {!query.isPending && !query.isError && teachers.map((t, index) => {
                const isSelected = value.some(x => x.id === t.id);
                const initials = getInitials(t.fullName);
                const avatarBg = avatarColors[(start + index) % avatarColors.length];
                return (
                  <tr
                    key={t.id}
                    className={isSelected ? 'is-selected' : ''}
                    onClick={() => {
                      if (isSelected) {
                        onChange(value.filter(x => x.id !== t.id));
                      } else {
                        onChange([...value, { id: t.id, fullName: t.fullName, email: t.email }]);
                      }
                    }}
                    style={{ cursor: 'pointer' }}
                  >
                    <td className="personnel-checkbox-cell" onClick={e => e.stopPropagation()}>
                      <input
                        type="checkbox"
                        aria-label={`Chọn ${t.fullName}`}
                        checked={isSelected}
                        onChange={e => {
                          if (e.target.checked) {
                            onChange([...value, { id: t.id, fullName: t.fullName, email: t.email }]);
                          } else {
                            onChange(value.filter(x => x.id !== t.id));
                          }
                        }}
                      />
                    </td>
                    <td className="class-number-cell">{start + index + 1}</td>
                    <td>
                      <div className="personnel-name">
                        <span className="personnel-avatar" style={{ background: avatarBg }}>
                          {initials}
                        </span>
                        <span>{t.fullName}</span>
                      </div>
                    </td>
                    <td>
                      <a className="personnel-email" href={`mailto:${t.email}`} onClick={e => e.stopPropagation()}>{t.email}</a>
                    </td>
                    <td>
                      <span className="text-slate-600 text-xs">{(t as any).department || '–'}</span>
                    </td>
                    <td className="personnel-status-cell">
                      {isSelected ? (
                        <span className="personnel-status status-Active">
                          <i />
                          <span>Đã chọn</span>
                        </span>
                      ) : (
                        <span className="personnel-status status-Disabled">
                          <i />
                          <span>Chưa chọn</span>
                        </span>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>

        <footer className="personnel-pagination">
          <span>Tổng số: <strong>{totalCount}</strong> giáo viên</span>
          <div>
            <label htmlFor="teacher-picker-page-size">Số dòng/trang</label>
            <div className="personnel-select">
              <select
                id="teacher-picker-page-size"
                value={pageSize}
                onChange={e => { setPageSize(Number(e.target.value)); setPage(1); }}
              >
                {[5, 10, 20].map(sz => (
                  <option key={sz} value={sz}>{sz}</option>
                ))}
              </select>
              <ChevronDown size={12} />
            </div>
            <strong aria-live="polite">
              {totalCount ? start + 1 : 0}–{Math.min(start + pageSize, totalCount)}
            </strong>
            <button
              type="button"
              className="personnel-icon"
              aria-label="Trang đầu"
              disabled={page === 1}
              onClick={() => setPage(1)}
            >
              <ChevronsLeft size={14} />
            </button>
            <button
              type="button"
              className="personnel-icon"
              aria-label="Trang trước"
              disabled={page === 1}
              onClick={() => setPage(page - 1)}
            >
              <ChevronLeft size={14} />
            </button>
            <button
              type="button"
              className="personnel-icon"
              aria-label="Trang sau"
              disabled={page >= totalPages}
              onClick={() => setPage(page + 1)}
            >
              <ChevronRight size={14} />
            </button>
            <button
              type="button"
              className="personnel-icon"
              aria-label="Trang cuối"
              disabled={page >= totalPages}
              onClick={() => setPage(totalPages)}
            >
              <ChevronsRight size={14} />
            </button>
          </div>
        </footer>
      </section>
    </div>
  );
}
