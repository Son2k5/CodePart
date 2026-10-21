import { useEffect, useMemo, useRef, useState } from 'react';
import type { PointerEvent as ReactPointerEvent } from 'react';
import { useSearchParams } from 'react-router-dom';
import { ChevronDown, ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight, Columns2, Eye, EyeOff, FileOutput, Filter, List, Megaphone, MoreHorizontal, Plus, RotateCcw, Search, Settings2, Users, UserCheck, Clock, Lock, X, Trash2, Edit3 } from 'lucide-react';
import { toast } from 'sonner';
import type { AdminUserItem } from '../types/admin.types';
import { UserDetailModal } from '../components/UserDetailModal';
import { UserFormModal } from '../components/UserFormModal';
import '../styles/personnel.css';
import { adminTeacherApi, teacherError, type TeacherFilter, type TeacherFormData } from '../api/adminTeacherApi';
import { useTeachers, useTeacherStats, useTeacherDetail, useFaculties, useTeacherMutations, useDebouncedSearch } from '../hooks/useTeachers';
import { adminStudentApi, studentError, type StudentFilter, type StudentFormData } from '../api/adminStudentApi';
import { useStudents, useStudentStats, useStudentDetail, useStudentMutations } from '../hooks/useStudents';

type PersonnelRole = 'Student' | 'Teacher';
type ColumnId = 'code' | 'name' | 'gender' | 'birthday' | 'email' | 'department' | 'classes' | 'courses' | 'created' | 'phone' | 'status';
type Column = { id: ColumnId; label: string; width: number; min: number };
type Panel = 'scope' | 'columns' | 'filters' | 'more' | 'add' | null;
const statusLabels: Record<AdminUserItem['status'], string> = { Active: 'Đang hoạt động', Pending: 'Chờ phê duyệt', Disabled: 'Đã khóa', Rejected: 'Đã từ chối' };
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
  const isStudent = role === 'Student';
  const roleLabel = isStudent ? 'sinh viên' : 'giáo viên';
  const columns = useMemo(() => getColumns(role), [role]);
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
  const changePage = (next: number) => { setPage(next); setSelectedIds([]); };
  const [pageSize, setPageSize] = useState(20);
  const [sort, setSort] = useState<{ id: ColumnId; direction: 1 | -1 } | null>(null);
  const [selectedUser, setSelectedUser] = useState<AdminUserItem | null>(null);
  const [editingUser, setEditingUser] = useState<AdminUserItem | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);
  const selectAllRef = useRef<HTMLInputElement>(null);
  const resizeRef = useRef<{ id: ColumnId; start: number; width: number; min: number } | null>(null);
  const panelTrigger = useRef<HTMLElement | null>(null);

  const search = useDebouncedSearch(query);
  const filter = {
    page,
    pageSize,
    search,
    facultyId: department || undefined,
    status: status === 'ALL' ? undefined : status as AdminUserItem['status'],
    sortBy: sort?.id === 'name' ? (sort.direction === 1 ? 'name_asc' : 'name_desc') : sort?.id === 'created' && sort.direction === 1 ? 'created_asc' : 'created_desc',
  } as TeacherFilter & StudentFilter;

  // React Query hooks for Teachers and Students
  const teachers = useTeachers(filter, !isStudent);
  const teacherStats = useTeacherStats(!isStudent);
  const teacherDetail = useTeacherDetail(!isStudent ? selectedUser?.id : undefined);
  const teacherMutations = useTeacherMutations();

  const students = useStudents(filter, isStudent);
  const studentStats = useStudentStats(isStudent);
  const studentDetail = useStudentDetail(isStudent ? selectedUser?.id : undefined);
  const studentMutations = useStudentMutations();

  const faculties = useFaculties(true);

  const currentQuery = isStudent ? students : teachers;
  const currentStats = isStudent ? studentStats : teacherStats;
  const currentDetail = isStudent ? studentDetail : teacherDetail;
  const mutations = isStudent ? studentMutations : teacherMutations;
  const getErrorMessage = isStudent ? studentError : teacherError;

  const users = currentQuery.data?.items ?? [];
  const stats = isStudent
    ? {
        totalUsers: studentStats.data?.totalStudents ?? 0,
        activeUsers: studentStats.data?.activeStudents ?? 0,
        pendingApprovals: studentStats.data?.pendingApprovals ?? 0,
        disabledUsers: studentStats.data?.disabledStudents ?? 0,
      }
    : {
        totalUsers: teacherStats.data?.totalTeachers ?? 0,
        activeUsers: teacherStats.data?.activeTeachers ?? 0,
        pendingApprovals: teacherStats.data?.pendingApprovals ?? 0,
        disabledUsers: teacherStats.data?.disabledTeachers ?? 0,
      };

  const statusCount = (key: string) => isStudent
    ? ({
        Active: studentStats.data?.activeStudents,
        Pending: studentStats.data?.pendingApprovals,
        Disabled: studentStats.data?.disabledStudents,
        Rejected: studentStats.data?.rejectedStudents,
      }[key] ?? 0)
    : ({
        Active: teacherStats.data?.activeTeachers,
        Pending: teacherStats.data?.pendingApprovals,
        Disabled: teacherStats.data?.disabledTeachers,
        Rejected: teacherStats.data?.rejectedTeachers,
      }[key] ?? 0);

  const visibleColumns = columns.filter((column) => !hiddenColumns.includes(column.id));
  const totalCount = currentQuery.data?.totalCount ?? 0;
  const totalPages = Math.max(1, currentQuery.data?.totalPages ?? 1);
  const currentPage = page;
  const start = (currentPage - 1) * pageSize;
  const pageUsers = users;

  useEffect(() => {
    if (currentQuery.data && page > totalPages) setPage(totalPages);
  }, [currentQuery.data, page, totalPages]);

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
    setStatus(next); changePage(1); setPanel(null); setSelectedIds([]);
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

  const exportCSV = async () => {
    try {
      if (isStudent) {
        await adminStudentApi.export(filter);
      } else {
        await adminTeacherApi.export(filter);
      }
    } catch (error) {
      toast.error(getErrorMessage(error));
    }
    setPanel(null);
  };

  const openCreate = () => { setEditingUser(null); setFormOpen(true); setPanel(null); };

  const updateStatus = async (user: AdminUserItem, next: AdminUserItem['status']) => {
    if (mutations.busy) return;
    try {
      await mutations.action.mutateAsync({
        id: user.id,
        action: next === 'Rejected' ? 'reject' : next === 'Disabled' ? 'disable' : user.status === 'Disabled' ? 'enable' : 'approve'
      });
      setSelectedUser(null);
      toast.success('Đã cập nhật trạng thái');
    } catch (error) {
      toast.error(getErrorMessage(error));
    }
  };

  const saveUser = async (data: TeacherFormData | StudentFormData) => {
    try {
      if (editingUser) await mutations.update.mutateAsync({ id: editingUser.id, data: data as any });
      else await mutations.create.mutateAsync(data as any);
      toast.success(`Đã lưu hồ sơ ${roleLabel}`);
    } catch (error) {
      toast.error(getErrorMessage(error));
    }
  };

  const bulkAction = async (action: 'approve' | 'disable') => {
    try {
      await mutations.bulk.mutateAsync({ ids: selectedIds, action });
      setSelectedIds([]);
      toast.success(`Đã cập nhật các ${roleLabel} đã chọn`);
    } catch (error) {
      toast.error(getErrorMessage(error));
    }
  };

  const [deleteConfirm, setDeleteConfirm] = useState<{
    ids: string[];
    title: string;
    description: string;
  } | null>(null);

  const deleteSingle = async (user: AdminUserItem) => {
    try {
      await mutations.remove.mutateAsync(user.id);
      setSelectedUser(null);
      setSelectedIds((prev) => prev.filter((id) => id !== user.id));
      toast.success(`Đã xóa tài khoản ${user.fullName}`);
    } catch (error) {
      toast.error(getErrorMessage(error));
    }
  };

  const executeDelete = async () => {
    if (!deleteConfirm || mutations.busy) return;
    try {
      if (deleteConfirm.ids.length === 1) {
        await mutations.remove.mutateAsync(deleteConfirm.ids[0]);
      } else {
        await mutations.bulkDelete.mutateAsync(deleteConfirm.ids);
      }
      setSelectedIds((prev) => prev.filter((id) => !deleteConfirm.ids.includes(id)));
      if (selectedUser && deleteConfirm.ids.includes(selectedUser.id)) {
        setSelectedUser(null);
      }
      toast.success(`Đã xóa thành công ${deleteConfirm.ids.length} tài khoản`);
      setDeleteConfirm(null);
    } catch (error) {
      toast.error(getErrorMessage(error));
    }
  };

  return (
    <div ref={rootRef} className="personnel-page">
      {currentQuery.isPending && <p role="status">Đang tải danh sách {roleLabel}…</p>}
      {(currentQuery.error || currentStats.error || faculties.error || currentDetail.error) && (
        <p role="alert">
          {getErrorMessage(currentQuery.error || currentStats.error || faculties.error || currentDetail.error)}{' '}
          <button onClick={() => { void currentQuery.refetch(); void currentStats.refetch(); void faculties.refetch(); if (selectedUser) void currentDetail.refetch(); }}>Thử lại</button>
        </p>
      )}
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
                : `${isStudent ? 'Sinh viên' : 'Giáo viên'} · ${statusLabels[status as AdminUserItem['status']]}`}
            </h1>
            <ChevronDown size={16} />
          </button>
          {panel === 'scope' && (
            <div id="personnel-scope" className="personnel-popover personnel-popover-left">
              <button onClick={() => changeStatus('ALL')}>
                Tất cả {roleLabel}<span>{stats.totalUsers}</span>
              </button>
              {Object.entries(statusLabels).map(([key, label]) => (
                <button key={key} onClick={() => changeStatus(key)}>
                  {label}<span>{statusCount(key)}</span>
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
                <button onClick={() => { void currentQuery.refetch(); void currentStats.refetch(); setSelectedIds([]); setPanel(null); }}>
                  Làm mới dữ liệu
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
              onChange={(event) => { setQuery(event.target.value); changePage(1); setSelectedIds([]); }}
              placeholder={`Tìm theo tên, email, mã ${roleLabel}...`}
              aria-label={`Tìm kiếm ${roleLabel}`}
            />
            {query && (
              <button aria-label="Xóa tìm kiếm" onClick={() => { setQuery(''); changePage(1); }}>
                <X size={13} />
              </button>
            )}
          </label>
          <div className="personnel-select">
            <select
              value={department}
              aria-label="Lọc theo khoa hoặc đơn vị"
              onChange={(event) => { setDepartment(event.target.value); changePage(1); setSelectedIds([]); }}
            >
              <option value="">Tất cả đơn vị / khoa</option>
              {faculties.data?.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}
            </select>
            <ChevronDown size={14} />
          </div>
          {selectedIds.length > 0 && <>
            <button disabled={mutations.busy || !pageUsers.filter(u => selectedIds.includes(u.id)).every(u => u.status === 'Pending' && (isStudent || u.emailVerifiedAt))} onClick={() => void bulkAction('approve')}>Duyệt đã chọn</button>
            <button disabled={mutations.busy || !pageUsers.filter(u => selectedIds.includes(u.id)).every(u => u.status === 'Active')} onClick={() => void bulkAction('disable')}>Khóa đã chọn</button>
            <button
              className="personnel-btn-danger"
              disabled={mutations.busy}
              onClick={() => setDeleteConfirm({
                ids: selectedIds,
                title: `Xóa ${selectedIds.length} ${roleLabel} đã chọn?`,
                description: `Hành động này sẽ xóa vĩnh viễn dữ liệu của ${selectedIds.length} tài khoản khỏi hệ thống và không thể hoàn tác.`
              })}
            >
              <Trash2 size={13} />
              Xóa đã chọn
            </button>
          </>}
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
                    <span>{stats.totalUsers}</span>
                  </button>
                  {Object.entries(statusLabels).map(([key, label]) => (
                    <button
                      key={key}
                      type="button"
                      className={status === key ? 'is-selected' : ''}
                      onClick={() => changeStatus(key)}
                    >
                      <span>{label}</span>
                      <span>{statusCount(key)}</span>
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
          <table style={{ width: 48 + 80 + visibleColumns.reduce((total, column) => total + (widths[column.id] ?? column.width), 0) }}>
            <caption className="personnel-sr-only">Danh sách {roleLabel}. Kéo mép phải tiêu đề cột hoặc dùng phím mũi tên để thay đổi độ rộng.</caption>
            <colgroup>
              <col style={{ width: 48 }} />
              {visibleColumns.map((column) => <col key={column.id} style={{ width: widths[column.id] ?? column.width }} />)}
              <col style={{ width: 80 }} />
            </colgroup>
            <thead>
              <tr>
                <th className="personnel-checkbox-cell">
                  <input ref={selectAllRef} type="checkbox" aria-label="Chọn tất cả dòng trên trang" checked={pageUsers.length > 0 && pageSelected === pageUsers.length} onChange={(event) => setSelectedIds((current) => event.target.checked ? [...new Set([...current, ...pageUsers.map((user) => user.id)])] : current.filter((id) => !pageUsers.some((user) => user.id === id)))} />
                </th>
                {visibleColumns.map((column) => (
                  <th key={column.id} scope="col" aria-sort={sort?.id === column.id ? sort.direction === 1 ? 'ascending' : 'descending' : 'none'}>
                    <button className="personnel-column-title" disabled={column.id !== 'name' && column.id !== 'created'} onClick={() => { changePage(1); setSort((current) => ({ id: column.id, direction: current?.id === column.id && current.direction === 1 ? -1 : 1 })); }}>
                      <span>{column.label}</span>
                      {sort?.id === column.id && <span>{sort.direction === 1 ? '↑' : '↓'}</span>}
                    </button>
                    <span className="personnel-resize-handle" role="separator" aria-label={`Độ rộng cột ${column.label}`} aria-orientation="vertical" aria-valuemin={column.min} aria-valuemax={600} aria-valuenow={widths[column.id] ?? column.width} tabIndex={0} onPointerDown={(event) => beginResize(event, column)} onPointerMove={(event) => { const resizing = resizeRef.current; if (resizing?.id === column.id) setWidths((current) => ({ ...current, [column.id]: Math.max(resizing.min, Math.min(600, resizing.width + event.clientX - resizing.start)) })); }} onPointerUp={endResize} onPointerCancel={endResize} onLostPointerCapture={endResize} onDoubleClick={() => setWidths((current) => ({ ...current, [column.id]: column.width }))} onKeyDown={(event) => { if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') { event.preventDefault(); setWidths((current) => ({ ...current, [column.id]: Math.max(column.min, Math.min(600, (current[column.id] ?? column.width) + (event.key === 'ArrowRight' ? 16 : -16))) })); } }} />
                  </th>
                ))}
                <th scope="col" style={{ width: 80, textAlign: 'center' }}>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {pageUsers.map((user, index) => (
                <tr key={user.id} className={selectedIds.includes(user.id) ? 'is-selected' : ''} onDoubleClick={() => { setEditingUser(user); setFormOpen(true); }}>
                  <td className="personnel-checkbox-cell">
                    <input type="checkbox" aria-label={`Chọn ${user.fullName}`} checked={selectedIds.includes(user.id)} onChange={(event) => setSelectedIds((current) => event.target.checked ? [...current, user.id] : current.filter((id) => id !== user.id))} />
                  </td>
                  {visibleColumns.map((column) => (
                    <td key={column.id} title={String(valueFor(user, column.id))}>
                      {column.id === 'name' ? (
                        <button className="personnel-name" onClick={() => setSelectedUser(user)}>
                          <span className="personnel-avatar" style={{ background: avatarColors[(start + index) % avatarColors.length] }}>
                            {user.fullName.split(' ').slice(-2).map((part) => part.charAt(0)).join('')}
                          </span>
                          <span>{user.fullName}</span>
                        </button>
                      ) : column.id === 'email' ? (
                        <a className="personnel-email" href={`mailto:${user.email}`}>{user.email}</a>
                      ) : column.id === 'status' ? (
                        <span className={`personnel-status status-${user.status}`}><i /><span>{statusLabels[user.status]}</span></span>
                      ) : column.id === 'created' ? (
                        dateLabel(user.createdAt)
                      ) : (
                        valueFor(user, column.id) || (typeof valueFor(user, column.id) === 'number' ? 0 : '–')
                      )}
                    </td>
                  ))}
                  <td className="personnel-actions-cell" onClick={(e) => e.stopPropagation()}>
                    <button
                      type="button"
                      className="personnel-row-action-btn edit"
                      title="Chỉnh sửa hồ sơ"
                      aria-label="Chỉnh sửa hồ sơ"
                      onClick={() => { setEditingUser(user); setFormOpen(true); }}
                    >
                      <Edit3 size={13} />
                    </button>
                    <button
                      type="button"
                      className="personnel-row-action-btn danger"
                      title="Xóa tài khoản"
                      aria-label="Xóa tài khoản"
                      onClick={() => {
                        setDeleteConfirm({
                          ids: [user.id],
                          title: `Xóa tài khoản ${user.fullName}?`,
                          description: `Hành động này sẽ xóa vĩnh viễn tài khoản ${user.email} (${user.studentId || user.teacherId || ''}) khỏi hệ thống.`
                        });
                      }}
                    >
                      <Trash2 size={13} />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {!users.length && !currentQuery.isPending && !currentQuery.isError && (
            <div className="personnel-empty"><Search size={28} /><strong>Không tìm thấy {roleLabel}</strong><p>Thử từ khóa khác hoặc đặt lại bộ lọc.</p><button onClick={resetFilters}>Đặt lại bộ lọc</button></div>
          )}
        </div>

        <footer className="personnel-pagination">
          <span>Tổng số: <strong>{totalCount}</strong> {roleLabel}</span>
          <div>
            <label htmlFor="personnel-page-size">Số dòng/trang</label>
            <div className="personnel-select">
              <select id="personnel-page-size" value={pageSize} onChange={(event) => { setPageSize(Number(event.target.value)); changePage(1); }}>
                {[5, 10, 20, 50, 100].map((size) => <option key={size}>{size}</option>)}
              </select>
              <ChevronDown size={12} />
            </div>
            <strong aria-live="polite">{totalCount ? start + 1 : 0}–{Math.min(start + pageSize, totalCount)}</strong>
            <button type="button" className="personnel-icon" aria-label="Trang đầu" disabled={currentPage === 1} onClick={() => changePage(1)}><ChevronsLeft size={14} /></button>
            <button type="button" className="personnel-icon" aria-label="Trang trước" disabled={currentPage === 1} onClick={() => changePage(currentPage - 1)}><ChevronLeft size={14} /></button>
            <button type="button" className="personnel-icon" aria-label="Trang sau" disabled={currentPage === totalPages} onClick={() => changePage(currentPage + 1)}><ChevronRight size={14} /></button>
            <button type="button" className="personnel-icon" aria-label="Trang cuối" disabled={currentPage === totalPages} onClick={() => changePage(totalPages)}><ChevronsRight size={14} /></button>
          </div>
        </footer>
      </section>

      <UserDetailModal
        detailError={currentDetail.error ? getErrorMessage(currentDetail.error) : undefined}
        busy={mutations.busy || currentDetail.isFetching}
        user={selectedUser ? currentDetail.data ?? selectedUser : null}
        onClose={() => setSelectedUser(null)}
        onApproveTeacher={(user) => updateStatus(user, 'Active')}
        onRejectTeacher={(user) => updateStatus(user, 'Rejected')}
        onToggleStatus={(user) => updateStatus(user, user.status === 'Disabled' ? 'Active' : 'Disabled')}
        onDeleteUser={deleteSingle}
      />
      <UserFormModal
        fixedRole={role}
        faculties={faculties.data ?? []}
        isOpen={formOpen}
        initialData={editingUser}
        onClose={() => { setFormOpen(false); setEditingUser(null); }}
        onSubmit={saveUser}
      />

      {deleteConfirm && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/40 backdrop-blur-xs animate-in fade-in duration-150">
          <div className="relative w-full max-w-md bg-white rounded-2xl shadow-2xl border border-slate-100 overflow-hidden p-6 space-y-4">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-xl bg-rose-100 text-rose-600 flex items-center justify-center shrink-0">
                <Trash2 className="w-5 h-5" />
              </div>
              <div>
                <h3 className="text-base font-bold text-slate-900">{deleteConfirm.title}</h3>
                <p className="text-xs text-slate-500 mt-0.5">Xác nhận thao tác xóa tài khoản</p>
              </div>
            </div>
            <p className="text-xs text-slate-600 leading-relaxed bg-rose-50/60 border border-rose-100 p-3 rounded-xl">
              {deleteConfirm.description}
            </p>
            <div className="flex items-center justify-end gap-2 pt-2">
              <button
                type="button"
                disabled={mutations.busy}
                onClick={() => setDeleteConfirm(null)}
                className="px-3.5 py-1.5 rounded-lg bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs font-medium cursor-pointer transition-colors"
              >
                Hủy
              </button>
              <button
                type="button"
                disabled={mutations.busy}
                onClick={executeDelete}
                className="px-3.5 py-1.5 rounded-lg bg-rose-600 hover:bg-rose-700 text-white text-xs font-medium cursor-pointer transition-colors shadow-xs"
              >
                {mutations.busy ? 'Đang xử lý...' : 'Xác nhận xóa'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}


