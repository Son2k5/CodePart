import { useEffect, useMemo, useRef, useState } from 'react';
import type { PointerEvent as ReactPointerEvent } from 'react';
import { useSearchParams } from 'react-router-dom';
import { ChevronDown, ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight, Columns2, Eye, EyeOff, FileOutput, Filter, List, Megaphone, MoreHorizontal, Plus, RotateCcw, Search, Settings2, Users, UserCheck, Clock, Lock, X } from 'lucide-react';
import { toast } from 'sonner';
import { DEPARTMENTS, INITIAL_MOCK_USERS, calculateMockStats } from '../data/mockAdminData';
import type { AdminUserItem } from '../types/admin.types';
import { UserDetailModal } from '../components/UserDetailModal';
import { UserFormModal } from '../components/UserFormModal';
import '../styles/personnel.css';

type PersonnelRole = 'Student' | 'Teacher';
type ColumnId = 'code' | 'name' | 'gender' | 'birthday' | 'email' | 'department' | 'classes' | 'courses' | 'created' | 'phone' | 'status';
type Column = { id: ColumnId; label: string; width: number; min: number };
type Panel = 'scope' | 'columns' | 'filters' | 'more' | 'add' | null;
const statusLabels: Record<AdminUserItem['status'], string> = { Active: 'Đang hoạt động', Pending: 'Chờ phê duyệt', Disabled: 'Đã khóa', Rejected: 'Đã từ chối' };
const normalize = (value: string) => value.toLocaleLowerCase('vi').normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd');
const dateLabel = (value: string) => new Date(value).toLocaleDateString('vi-VN');
const avatarColors = ['#35b6cf', '#55bb70', '#9a7cf0', '#f58b53', '#ed7099', '#5b92d9'];
const getColumns = (role: PersonnelRole): Column[] => [
  { id: 'code', label: role === 'Student' ? 'Mã sinh viên' : 'Mã giáo viên', width: 155, min: 110 },
  { id: 'name', label: 'Họ và tên', width: 245, min: 150 },
  { id: 'gender', label: 'Giới tính', width: 110, min: 85 },
  { id: 'birthday', label: 'Ngày sinh', width: 130, min: 105 },
  { id: 'email', label: 'Email trường', width: 250, min: 150 },
  { id: 'department', label: 'Khoa / Đơn vị', width: 245, min: 140 },
  { id: 'classes', label: role === 'Student' ? 'Số lớp tham gia' : 'Số lớp giảng dạy', width: 165, min: 130 },
  { id: 'courses', label: role === 'Student' ? 'Học phần đăng ký' : 'Học phần phụ trách', width: 175, min: 135 },
  { id: 'created', label: 'Ngày tạo hồ sơ', width: 155, min: 120 },
  { id: 'phone', label: 'Số điện thoại', width: 155, min: 110 },
  { id: 'status', label: 'Trạng thái', width: 175, min: 135 },
];
const valueFor = (user: AdminUserItem, id: ColumnId): string | number => {
  switch (id) {
    case 'code': return user.studentId || user.teacherId || '';
    case 'name': return user.fullName;
    case 'gender': case 'birthday': return '';
    case 'email': return user.email;
    case 'department': return user.department || '';
    case 'classes': return user.classesCount ?? 0;
    case 'courses': return user.coursesCount ?? 0;
    case 'created': return user.createdAt;
    case 'phone': return user.phone || '';
    case 'status': return statusLabels[user.status];
  }
};

export function UserManagementPage({ role }: { role: PersonnelRole }) {
  const roleLabel = role === 'Student' ? 'sinh viên' : 'giáo viên';
  const initialUsers = useMemo(() => INITIAL_MOCK_USERS.filter((user) => user.role === role), [role]);
  const columns = useMemo(() => getColumns(role), [role]);
  const [users, setUsers] = useState(() => initialUsers.map((user) => ({ ...user })));
  const [searchParams, setSearchParams] = useSearchParams();
  const [query, setQuery] = useState('');
  const [department, setDepartment] = useState('');
  const [status, setStatus] = useState(() => searchParams.get('tab') === 'pending' ? 'Pending' : searchParams.get('tab') === 'disabled' ? 'Disabled' : 'ALL');
  const [showOverview, setShowOverview] = useState(false);
  const [showNotice, setShowNotice] = useState(true);
  const [panel, setPanel] = useState<Panel>(null);
  const [compact, setCompact] = useState(true);
  const [widths, setWidths] = useState<Partial<Record<ColumnId, number>>>({});
  const [hiddenColumns, setHiddenColumns] = useState<ColumnId[]>([]);
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [sort, setSort] = useState<{ id: ColumnId; direction: 1 | -1 } | null>(null);
  const [selectedUser, setSelectedUser] = useState<AdminUserItem | null>(null);
  const [editingUser, setEditingUser] = useState<AdminUserItem | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);
  const selectAllRef = useRef<HTMLInputElement>(null);
  const resizeRef = useRef<{ id: ColumnId; start: number; width: number; min: number } | null>(null);
  const panelTrigger = useRef<HTMLElement | null>(null);
  const stats = useMemo(() => calculateMockStats(users), [users]);
  const visibleColumns = columns.filter((column) => !hiddenColumns.includes(column.id));
  const filteredUsers = users.filter((user) => {
    const text = normalize([user.fullName, user.email, user.studentId, user.teacherId].join(' '));
    return (!query.trim() || text.includes(normalize(query.trim()))) && (!department || user.department === department) && (status === 'ALL' || user.status === status);
  }).sort((a, b) => {
    if (!sort) return 0;
    const first = valueFor(a, sort.id), second = valueFor(b, sort.id);
    return (typeof first === 'number' && typeof second === 'number' ? first - second : String(first).localeCompare(String(second), 'vi')) * sort.direction;
  });
  const totalPages = Math.max(1, Math.ceil(filteredUsers.length / pageSize));
  const currentPage = Math.min(page, totalPages);
  const start = (currentPage - 1) * pageSize;
  const pageUsers = filteredUsers.slice(start, start + pageSize);
  const pageSelected = pageUsers.filter((user) => selectedIds.includes(user.id)).length;
  useEffect(() => {
    if (selectAllRef.current) selectAllRef.current.indeterminate = pageSelected > 0 && pageSelected < pageUsers.length;
  }, [pageSelected, pageUsers.length]);
  useEffect(() => {
    if (!panel) return;
    const outside = (event: PointerEvent) => { if (!(event.target as Element).closest('[data-personnel-menu]')) setPanel(null); };
    const escape = (event: KeyboardEvent) => { if (event.key === 'Escape') { setPanel(null); panelTrigger.current?.focus(); } };
    document.addEventListener('pointerdown', outside);
    document.addEventListener('keydown', escape);
    return () => { document.removeEventListener('pointerdown', outside); document.removeEventListener('keydown', escape); };
  }, [panel]);
  const togglePanel = (next: Panel, trigger: HTMLElement) => { panelTrigger.current = trigger; setPanel((current) => current === next ? null : next); };
  const changeStatus = (next: string) => {
    setStatus(next); setPage(1); setPanel(null); setSelectedIds([]);
    setSearchParams(next === 'Pending' ? { tab: 'pending' } : next === 'Disabled' ? { tab: 'disabled' } : {}, { replace: true });
  };
  const resetFilters = () => { setQuery(''); setDepartment(''); changeStatus('ALL'); };
  const beginResize = (event: ReactPointerEvent<HTMLSpanElement>, column: Column) => {
    event.preventDefault(); event.stopPropagation();
    event.currentTarget.setPointerCapture(event.pointerId);
    resizeRef.current = { id: column.id, start: event.clientX, width: widths[column.id] ?? column.width, min: column.min };
    rootRef.current?.classList.add('personnel-resizing');
  };
  const endResize = () => { resizeRef.current = null; rootRef.current?.classList.remove('personnel-resizing'); };
  const exportCSV = () => {
    const rows = selectedIds.length ? filteredUsers.filter((user) => selectedIds.includes(user.id)) : filteredUsers;
    const escape = (value: string | number) => `"${String(value).replace(/^[=+@-]/, "'$&").replace(/"/g, '""')}"`;
    const csv = [visibleColumns.map((column) => column.label), ...rows.map((user) => visibleColumns.map((column) => column.id === 'created' ? dateLabel(user.createdAt) : valueFor(user, column.id)))].map((row) => row.map(escape).join(',')).join('\r\n');
    const url = URL.createObjectURL(new Blob(['\uFEFF', csv], { type: 'text/csv;charset=utf-8;' }));
    const link = document.createElement('a'); link.href = url; link.download = `${role === 'Student' ? 'sinh-vien' : 'giao-vien'}.csv`; link.click(); URL.revokeObjectURL(url); setPanel(null);
  };
  const openCreate = () => { setEditingUser(null); setFormOpen(true); setPanel(null); };
  const updateStatus = (user: AdminUserItem, next: AdminUserItem['status']) => {
    setUsers((current) => current.map((item) => item.id === user.id ? { ...item, status: next } : item));
    setSelectedUser((current) => current?.id === user.id ? { ...current, status: next } : current);
    toast.success('Đã cập nhật trạng thái trong bản xem thử');
  };

  return (
    <div ref={rootRef} className="personnel-page">
      {showNotice && (
        <div className="personnel-notice" role="status">
          <Megaphone size={16} />
          <p>
            Quản lý tập trung hồ sơ và thông tin {roleLabel} trong hệ thống CodePart. <strong>{stats.totalUsers} hồ sơ</strong> đang có trong danh sách.<br />
            <span>Bạn có thể kéo đường phân cách giữa các cột để thay đổi độ rộng hoặc tùy chỉnh các cột hiển thị.</span>
          </p>
          <button className="personnel-notice-close" aria-label="Đóng thông báo" onClick={() => setShowNotice(false)}>
            <X size={14} />
          </button>
        </div>
      )}

      <div className="personnel-heading">
        <div className="personnel-menu-anchor" data-personnel-menu>
          <button
            className="personnel-title"
            aria-expanded={panel === 'scope'}
            aria-controls="personnel-scope"
            onClick={(event) => togglePanel('scope', event.currentTarget)}
          >
            <h1>
              {status === 'ALL'
                ? `Tất cả ${roleLabel}`
                : `${role === 'Student' ? 'Sinh viên' : 'Giáo viên'} · ${statusLabels[status as AdminUserItem['status']]}`}
            </h1>
            <ChevronDown size={16} />
          </button>
          {panel === 'scope' && (
            <div id="personnel-scope" className="personnel-popover personnel-popover-left">
              <button onClick={() => changeStatus('ALL')}>
                Tất cả {roleLabel}<span>{users.length}</span>
              </button>
              {Object.entries(statusLabels).map(([key, label]) => (
                <button key={key} onClick={() => changeStatus(key)}>
                  {label}<span>{users.filter((user) => user.status === key).length}</span>
                </button>
              ))}
            </div>
          )}
        </div>

        <div className="personnel-actions">
          <button
            type="button"
            className="personnel-overview-toggle"
            aria-expanded={showOverview}
            aria-controls="personnel-overview"
            onClick={() => setShowOverview((show) => !show)}
          >
            {showOverview ? <EyeOff size={15} /> : <Eye size={15} />}
            <span>{showOverview ? 'Ẩn tổng quan' : 'Xem tổng quan'}</span>
          </button>
          <div className="personnel-density" aria-label="Mật độ bảng">
            <button
              type="button"
              className={compact ? 'is-active' : ''}
              aria-label="Dòng thu gọn"
              title="Dòng thu gọn"
              aria-pressed={compact}
              onClick={() => setCompact(true)}
            >
              <List size={15} />
            </button>
            <button
              type="button"
              className={!compact ? 'is-active' : ''}
              aria-label="Dòng rộng"
              title="Dòng rộng"
              aria-pressed={!compact}
              onClick={() => setCompact(false)}
            >
              <Columns2 size={15} />
            </button>
          </div>
          <div className="personnel-split-button" data-personnel-menu>
            <button onClick={openCreate}><Plus size={15} />Thêm</button>
            <button aria-label="Tùy chọn thêm" aria-expanded={panel === 'add'} onClick={(event) => togglePanel('add', event.currentTarget)}>
              <ChevronDown size={14} />
            </button>
            {panel === 'add' && (
              <div className="personnel-popover">
                <button onClick={openCreate}><Plus size={14} />Thêm {roleLabel}</button>
                <button onClick={() => { setShowNotice(true); setPanel(null); }}><Megaphone size={14} />Hướng dẫn thao tác</button>
              </div>
            )}
          </div>
          <div className="personnel-menu-anchor" data-personnel-menu>
            <button className="personnel-icon" title="Thao tác khác" aria-label="Thao tác khác" aria-expanded={panel === 'more'} onClick={(event) => togglePanel('more', event.currentTarget)}>
              <MoreHorizontal size={16} />
            </button>
            {panel === 'more' && (
              <div className="personnel-popover">
                <button onClick={() => { resetFilters(); setSort(null); setSelectedIds([]); setPanel(null); }}>
                  <RotateCcw size={14} />Đặt lại bộ lọc
                </button>
                <button onClick={() => { setWidths({}); setHiddenColumns([]); setPanel(null); }}>
                  Khôi phục các cột
                </button>
                <button onClick={() => { setUsers(initialUsers.map((user) => ({ ...user }))); setSelectedIds([]); setPanel(null); }}>
                  Làm mới dữ liệu mẫu
                </button>
              </div>
            )}
          </div>
        </div>
      </div>

      {showOverview && (
        <section id="personnel-overview" className="personnel-overview" aria-label={`Tổng quan ${roleLabel}`}>
          {[
            { label: `Tổng ${roleLabel}`, value: stats.totalUsers, icon: Users, status: 'ALL', color: 'blue' },
            { label: 'Đang hoạt động', value: stats.activeUsers, icon: UserCheck, status: 'Active', color: 'green' },
            { label: 'Chờ phê duyệt', value: stats.pendingApprovals, icon: Clock, status: 'Pending', color: 'amber' },
            { label: 'Đã khóa', value: stats.disabledUsers, icon: Lock, status: 'Disabled', color: 'gray' },
          ].map(({ label, value, icon: Icon, status: next, color }) => (
            <button key={next} onClick={() => changeStatus(next)} className={status === next ? 'is-selected' : ''}>
              <span className={`personnel-stat-icon ${color}`}><Icon size={18} /></span>
              <span><small>{label}</small><strong>{value}</strong></span>
            </button>
          ))}
        </section>
      )}

      <section className="personnel-grid" aria-label={`Danh sách ${roleLabel}`}>
        <div className="personnel-toolbar">
          <label className="personnel-search">
            <Search size={14} />
            <input
              value={query}
              onChange={(event) => { setQuery(event.target.value); setPage(1); setSelectedIds([]); }}
              placeholder={`Tìm theo tên, email, mã ${roleLabel}...`}
              aria-label={`Tìm kiếm ${roleLabel}`}
            />
            {query && (
              <button aria-label="Xóa tìm kiếm" onClick={() => { setQuery(''); setPage(1); }}>
                <X size={13} />
              </button>
            )}
          </label>
          <div className="personnel-select">
            <select
              value={department}
              aria-label="Lọc theo khoa hoặc đơn vị"
              onChange={(event) => { setDepartment(event.target.value); setPage(1); setSelectedIds([]); }}
            >
              <option value="">Tất cả đơn vị / khoa</option>
              {DEPARTMENTS.map((item) => <option key={item}>{item}</option>)}
            </select>
            <ChevronDown size={14} />
          </div>
          {selectedIds.length > 0 && (
            <button className="personnel-selection-badge" onClick={() => setSelectedIds([])}>
              Đã chọn {selectedIds.length}
              <X size={12} />
            </button>
          )}
          <div className="personnel-toolbar-actions">
            <div className="personnel-menu-anchor" data-personnel-menu>
              <button
                type="button"
                className={`personnel-icon ${status !== 'ALL' || panel === 'filters' ? 'is-active' : ''}`}
                aria-label="Lọc trạng thái hồ sơ"
                title="Bộ lọc trạng thái"
                aria-expanded={panel === 'filters'}
                onClick={(event) => togglePanel('filters', event.currentTarget)}
              >
                <Filter size={15} />
                {status !== 'ALL' && <span className="personnel-filter-dot" />}
              </button>
              {panel === 'filters' && (
                <div className="personnel-popover">
                  <strong>Trạng thái hồ sơ</strong>
                  <button
                    type="button"
                    className={status === 'ALL' ? 'is-selected' : ''}
                    onClick={() => changeStatus('ALL')}
                  >
                    <span>Tất cả trạng thái</span>
                    <span>{users.length}</span>
                  </button>
                  {Object.entries(statusLabels).map(([key, label]) => (
                    <button
                      key={key}
                      type="button"
                      className={status === key ? 'is-selected' : ''}
                      onClick={() => changeStatus(key)}
                    >
                      <span>{label}</span>
                      <span>{users.filter((user) => user.status === key).length}</span>
                    </button>
                  ))}
                </div>
              )}
            </div>

            <div className="personnel-menu-anchor" data-personnel-menu>
              <button
                type="button"
                className={`personnel-icon ${panel === 'columns' ? 'is-active' : ''}`}
                aria-label="Cài đặt cột hiển thị"
                title="Cài đặt cột"
                aria-expanded={panel === 'columns'}
                onClick={(event) => togglePanel('columns', event.currentTarget)}
              >
                <Settings2 size={15} />
              </button>
              {panel === 'columns' && (
                <div className="personnel-popover personnel-column-options">
                  <strong>Tùy chỉnh cột</strong>
                  <p>Kéo mép tiêu đề để đổi độ rộng.</p>
                  {columns.map((column) => (
                    <label key={column.id}>
                      <input
                        type="checkbox"
                        checked={!hiddenColumns.includes(column.id)}
                        disabled={column.id === 'name'}
                        onChange={() => setHiddenColumns((current) => current.includes(column.id) ? current.filter((id) => id !== column.id) : [...current, column.id])}
                      />
                      {column.label}
                    </label>
                  ))}
                  <button type="button" onClick={() => { setWidths({}); setHiddenColumns([]); }}>Khôi phục mặc định</button>
                </div>
              )}
            </div>

            <button
              type="button"
              className="personnel-icon"
              aria-label="Xuất danh sách CSV"
              title="Xuất danh sách CSV"
              onClick={exportCSV}
            >
              <FileOutput size={15} />
            </button>
          </div>
        </div>

        <div className={`personnel-table-scroll ${compact ? '' : 'personnel-comfortable'}`}>
          <table style={{ width: 48 + visibleColumns.reduce((total, column) => total + (widths[column.id] ?? column.width), 0) }}>
            <caption className="personnel-sr-only">Danh sách {roleLabel}. Kéo mép phải tiêu đề cột hoặc dùng phím mũi tên để thay đổi độ rộng.</caption>
            <colgroup><col style={{ width: 48 }} />{visibleColumns.map((column) => <col key={column.id} style={{ width: widths[column.id] ?? column.width }} />)}</colgroup>
            <thead><tr><th className="personnel-checkbox-cell"><input ref={selectAllRef} type="checkbox" aria-label="Chọn tất cả dòng trên trang" checked={pageUsers.length > 0 && pageSelected === pageUsers.length} onChange={(event) => setSelectedIds((current) => event.target.checked ? [...new Set([...current, ...pageUsers.map((user) => user.id)])] : current.filter((id) => !pageUsers.some((user) => user.id === id)))} /></th>{visibleColumns.map((column) => <th key={column.id} scope="col" aria-sort={sort?.id === column.id ? sort.direction === 1 ? 'ascending' : 'descending' : 'none'}><button className="personnel-column-title" onClick={() => { setPage(1); setSort((current) => ({ id: column.id, direction: current?.id === column.id && current.direction === 1 ? -1 : 1 })); }}><span>{column.label}</span>{sort?.id === column.id && <span>{sort.direction === 1 ? '↑' : '↓'}</span>}</button><span className="personnel-resize-handle" role="separator" aria-label={`Độ rộng cột ${column.label}`} aria-orientation="vertical" aria-valuemin={column.min} aria-valuemax={600} aria-valuenow={widths[column.id] ?? column.width} tabIndex={0} onPointerDown={(event) => beginResize(event, column)} onPointerMove={(event) => { const resizing = resizeRef.current; if (resizing?.id === column.id) setWidths((current) => ({ ...current, [column.id]: Math.max(resizing.min, Math.min(600, resizing.width + event.clientX - resizing.start)) })); }} onPointerUp={endResize} onPointerCancel={endResize} onLostPointerCapture={endResize} onDoubleClick={() => setWidths((current) => ({ ...current, [column.id]: column.width }))} onKeyDown={(event) => { if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') { event.preventDefault(); setWidths((current) => ({ ...current, [column.id]: Math.max(column.min, Math.min(600, (current[column.id] ?? column.width) + (event.key === 'ArrowRight' ? 16 : -16))) })); } }} /></th>)}</tr></thead>
            <tbody>{pageUsers.map((user, index) => <tr key={user.id} className={selectedIds.includes(user.id) ? 'is-selected' : ''} onDoubleClick={() => { setEditingUser(user); setFormOpen(true); }}><td className="personnel-checkbox-cell"><input type="checkbox" aria-label={`Chọn ${user.fullName}`} checked={selectedIds.includes(user.id)} onChange={(event) => setSelectedIds((current) => event.target.checked ? [...current, user.id] : current.filter((id) => id !== user.id))} /></td>{visibleColumns.map((column) => <td key={column.id} title={String(valueFor(user, column.id))}>{column.id === 'name' ? <button className="personnel-name" onClick={() => setSelectedUser(user)}><span className="personnel-avatar" style={{ background: avatarColors[(start + index) % avatarColors.length] }}>{user.fullName.split(' ').slice(-2).map((part) => part.charAt(0)).join('')}</span><span>{user.fullName}</span></button> : column.id === 'email' ? <a className="personnel-email" href={`mailto:${user.email}`}>{user.email}</a> : column.id === 'status' ? <span className={`personnel-status status-${user.status}`}><i /><span>{statusLabels[user.status]}</span></span> : column.id === 'created' ? dateLabel(user.createdAt) : valueFor(user, column.id) || (typeof valueFor(user, column.id) === 'number' ? 0 : '–')}</td>)}</tr>)}</tbody>
          </table>
          {!filteredUsers.length && <div className="personnel-empty"><Search size={28} /><strong>Không tìm thấy {roleLabel}</strong><p>Thử từ khóa khác hoặc đặt lại bộ lọc.</p><button onClick={resetFilters}>Đặt lại bộ lọc</button></div>}
        </div>

        <footer className="personnel-pagination">
          <span>Tổng số: <strong>{filteredUsers.length}</strong> {roleLabel}</span>
          <div>
            <label htmlFor="personnel-page-size">Số dòng/trang</label>
            <div className="personnel-select">
              <select id="personnel-page-size" value={pageSize} onChange={(event) => { setPageSize(Number(event.target.value)); setPage(1); }}>
                {[5, 10, 20, 50, 100].map((size) => <option key={size}>{size}</option>)}
              </select>
              <ChevronDown size={12} />
            </div>
            <strong aria-live="polite">{filteredUsers.length ? start + 1 : 0}–{Math.min(start + pageSize, filteredUsers.length)}</strong>
            <button type="button" className="personnel-icon" aria-label="Trang đầu" disabled={currentPage === 1} onClick={() => setPage(1)}><ChevronsLeft size={14} /></button>
            <button type="button" className="personnel-icon" aria-label="Trang trước" disabled={currentPage === 1} onClick={() => setPage(currentPage - 1)}><ChevronLeft size={14} /></button>
            <button type="button" className="personnel-icon" aria-label="Trang sau" disabled={currentPage === totalPages} onClick={() => setPage(currentPage + 1)}><ChevronRight size={14} /></button>
            <button type="button" className="personnel-icon" aria-label="Trang cuối" disabled={currentPage === totalPages} onClick={() => setPage(totalPages)}><ChevronsRight size={14} /></button>
          </div>
        </footer>
      </section>

      <UserDetailModal user={selectedUser} onClose={() => setSelectedUser(null)} onApproveTeacher={(user) => updateStatus(user, 'Active')} onRejectTeacher={(user) => updateStatus(user, 'Rejected')} onToggleStatus={(user) => updateStatus(user, user.status === 'Disabled' ? 'Active' : 'Disabled')} />
      <UserFormModal fixedRole={role} isOpen={formOpen} initialData={editingUser} onClose={() => { setFormOpen(false); setEditingUser(null); }} onSubmit={(data) => { if (editingUser) setUsers((current) => current.map((user) => user.id === editingUser.id ? { ...user, ...data, role } : user)); else setUsers((current) => [{ ...data, id: crypto.randomUUID(), fullName: data.fullName || '', email: data.email || '', role, status: data.status || 'Active', createdAt: new Date().toISOString(), lastLoginAt: null }, ...current]); toast.success('Đã lưu hồ sơ trong bản xem thử'); }} />
    </div>
  );
}
