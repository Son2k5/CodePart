import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { BookOpen, CheckCircle, Archive, Clock, ChevronDown, ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight, Columns2, Eye, EyeOff, FileOutput, Filter, List, Megaphone, MoreHorizontal, Plus, RotateCcw, Search, Settings2, X, Trash2, Edit3 } from 'lucide-react';
import { toast } from 'sonner';
import { axiosInstance } from '../../lib/axios';
import { teacherError } from '../admin/api/adminTeacherApi';
import { useClassesQuery, useClassMutation, useDebounced, type ClassItem, type Page } from './api';
import { ConfirmDialog, QueryState } from './ClassUi';
import './classes.css';
import { ClassFormModal } from './ClassFormPage';
import './class-management.css';
import '../admin/styles/personnel.css';

const statusLabels = { Draft: 'Bản nháp', Active: 'Đang hoạt động', Archived: 'Đã lưu trữ' };
const columns = [
  { id: 'name', label: 'Tên lớp học', width: 260, min: 160, sort: 'name' },
  { id: 'joinCode', label: 'Mã tham gia', width: 150, min: 110 },
  { id: 'accessType', label: 'Quyền tham gia', width: 160, min: 120 },
  { id: 'studentCount', label: 'Số sinh viên', width: 145, min: 110, sort: 'studentCount' },
  { id: 'teachers', label: 'Giáo viên phụ trách', width: 240, min: 150 },
  { id: 'startDate', label: 'Ngày bắt đầu', width: 145, min: 110 },
  { id: 'endDate', label: 'Ngày kết thúc', width: 145, min: 110 },
  { id: 'createdAt', label: 'Ngày tạo lớp', width: 145, min: 110, sort: 'createdAt' },
  { id: 'status', label: 'Trạng thái', width: 170, min: 130 },
] as const;
type ColumnId = typeof columns[number]['id'];
type Panel = 'scope' | 'filters' | 'columns' | 'more' | 'add' | null;
const dateLabel = (value: string | null) => value ? new Date(value).toLocaleDateString('vi-VN') : '–';
function valueFor(item: ClassItem, id: ColumnId) {
  switch (id) {
    case 'status': return statusLabels[item.status];
    case 'accessType': return item.accessType === 'Public' ? 'Công khai' : 'Cần mã tham gia';
    case 'studentCount': return `${item.studentCount}/${item.maxStudents ?? '∞'}`;
    case 'teachers': return item.teachers.map(t => t.fullName).join(', ') || 'Chưa phân công';
    case 'startDate': case 'endDate': case 'createdAt': return dateLabel(item[id]);
    default: return item[id] || '–';
  }
}

export function ClassListPage() {
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<ClassItem | undefined>();
  const openForm = (item?: ClassItem) => { setEditing(item); setPanel(null); setFormOpen(true); };
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [status, setStatus] = useState('');
  const [accessType, setAccess] = useState('');
  const [hasTeacher, setHasTeacher] = useState('');
  const [sort, setSort] = useState('createdAt');
  const [desc, setDesc] = useState(true);
  const [deleting, setDeleting] = useState<ClassItem | null>(null);
  const [showOverview, setShowOverview] = useState(false);
  const [showNotice, setShowNotice] = useState(true);
  const [compact, setCompact] = useState(true);
  const [panel, setPanel] = useState<Panel>(null);
  const [hiddenColumns, setHiddenColumns] = useState<ColumnId[]>(['startDate', 'endDate', 'createdAt']);
  const [widths, setWidths] = useState<Partial<Record<ColumnId, number>>>({});
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [exporting, setExporting] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);
  const tableScrollRef = useRef<HTMLDivElement>(null);
  const actionsHeadingRef = useRef<HTMLTableCellElement>(null);
  const [actionsInView, setActionsInView] = useState(false);
  const selectAllRef = useRef<HTMLInputElement>(null);
  const triggerRef = useRef<HTMLButtonElement | null>(null);
  const resizeRef = useRef<{ id: ColumnId; start: number; width: number } | null>(null);
  const debounced = useDebounced(search);
  const mutation = useClassMutation();
  const filters = { search: debounced, status: status || undefined, accessType: accessType || undefined, hasTeacher: hasTeacher || undefined, sort, desc };
  const query = useClassesQuery<Page<ClassItem>>('/admin/classes', { ...filters, page, pageSize });
  const all = useClassesQuery<Page<ClassItem>>('/admin/classes', { page: 1, pageSize: 1 });
  const active = useClassesQuery<Page<ClassItem>>('/admin/classes', { page: 1, pageSize: 1, status: 'Active' });
  const draft = useClassesQuery<Page<ClassItem>>('/admin/classes', { page: 1, pageSize: 1, status: 'Draft' });
  const archived = useClassesQuery<Page<ClassItem>>('/admin/classes', { page: 1, pageSize: 1, status: 'Archived' });
  const stats = [
    { status: '', label: 'Tổng lớp học', query: all, icon: BookOpen, color: 'blue' },
    { status: 'Active', label: statusLabels.Active, query: active, icon: CheckCircle, color: 'green' },
    { status: 'Draft', label: statusLabels.Draft, query: draft, icon: Clock, color: 'amber' },
    { status: 'Archived', label: statusLabels.Archived, query: archived, icon: Archive, color: 'gray' },
  ];
  const items = query.data?.items ?? [];
  const total = query.data?.totalCount ?? 0;
  const totalPages = Math.max(1, query.data?.totalPages ?? 1);
  const start = (page - 1) * pageSize;
  const visibleColumns = columns.filter(c => !hiddenColumns.includes(c.id));
  const selectedCount = items.filter(c => selectedIds.includes(c.id)).length;
  const changePage = (next: number) => { setPage(next); setSelectedIds([]); };
  const changeStatus = (next: string) => { setStatus(next); changePage(1); setPanel(null); };
  const resetFilters = () => { setSearch(''); setAccess(''); setHasTeacher(''); changeStatus(''); setSort('createdAt'); setDesc(true); };
  const refresh = () => { void query.refetch(); for (const stat of stats) void stat.query.refetch(); setPanel(null); };
  useEffect(() => {
    const root = tableScrollRef.current;
    const heading = actionsHeadingRef.current;
    if (!root || !heading) return;
    // The heading stays in the table flow, so it tracks the real action column.
    const observer = new IntersectionObserver(([entry]) => {
      setActionsInView(entry.isIntersecting && entry.intersectionRatio >= 0.99);
    }, { root, threshold: [0, 0.99, 1] });
    observer.observe(heading);
    return () => observer.disconnect();
  }, []);
  useEffect(() => { if (query.data && page > totalPages) { setPage(totalPages); setSelectedIds([]); } }, [query.data, page, totalPages]);
  useEffect(() => { if (selectAllRef.current) selectAllRef.current.indeterminate = selectedCount > 0 && selectedCount < items.length; }, [selectedCount, items.length]);
  useEffect(() => {
    if (!panel) return;
    const outside = (event: PointerEvent) => { if (!(event.target as Element).closest('[data-personnel-menu]')) setPanel(null); };
    const escape = (event: KeyboardEvent) => { if (event.key === 'Escape') { setPanel(null); triggerRef.current?.focus(); } };
    document.addEventListener('pointerdown', outside); document.addEventListener('keydown', escape);
    return () => { document.removeEventListener('pointerdown', outside); document.removeEventListener('keydown', escape); };
  }, [panel]);
  const togglePanel = (next: Panel, trigger: HTMLButtonElement) => { triggerRef.current = trigger; setPanel(panel === next ? null : next); };
  const endResize = () => { resizeRef.current = null; rootRef.current?.classList.remove('personnel-resizing'); };
  const exportCSV = async () => {
    if (exporting) return;
    setExporting(true);
    try {
      const rows: ClassItem[] = [];
      for (let next = 1, last = 1; next <= last; next++) {
        const { data } = await axiosInstance.get<Page<ClassItem>>('/admin/classes', { params: { ...filters, page: next, pageSize: 100 } });
        rows.push(...data.items); last = data.totalPages;
      }
      const escape = (value: string) => `"${(/^[=+\-@\t\r]/.test(value) ? "'" + value : value).replaceAll('"', '""')}"`;
      const csv = [columns.map(c => c.label), ...rows.filter(c => !selectedIds.length || selectedIds.includes(c.id)).map(c => columns.map(column => valueFor(c, column.id)))].map(row => row.map(escape).join(',')).join('\r\n');
      const url = URL.createObjectURL(new Blob(['\uFEFF', csv], { type: 'text/csv;charset=utf-8;' }));
      const link = document.createElement('a'); link.href = url; link.download = 'bao-cao-lop-hoc.csv'; link.click();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
      toast.success('Đã xuất báo cáo lớp học');
    } catch (error) { toast.error(teacherError(error)); }
    finally { setExporting(false); }
  };
  const statusMenu = (left = false) => <div className={`personnel-popover ${left ? 'personnel-popover-left' : ''}`}><strong>Trạng thái lớp học</strong>{stats.map(s => <button key={s.status} className={status === s.status ? 'is-selected' : ''} onClick={() => changeStatus(s.status)}>{s.status ? s.label : 'Tất cả lớp học'}<span>{s.query.data?.totalCount ?? '–'}</span></button>)}</div>;

  return <div ref={rootRef} className="personnel-page class-management">
    <QueryState loading={query.isLoading} error={query.error} retry={() => query.refetch()} />
    {stats.some(s => s.query.isError) && <p role="alert">Không tải được báo cáo tổng quan. <button onClick={refresh}>Thử lại</button></p>}
    {showNotice && <div className="personnel-notice" role="status"><Megaphone size={16} /><p>Quản lý tập trung lớp học và thông tin giảng dạy trong hệ thống CodePart. <strong>{all.data?.totalCount ?? '–'} lớp học</strong> đang có trong danh sách.<br /><span>Bạn có thể kéo đường phân cách giữa các cột để thay đổi độ rộng hoặc tùy chỉnh các cột hiển thị.</span></p><button className="personnel-notice-close" aria-label="Đóng thông báo" onClick={() => setShowNotice(false)}><X size={14} /></button></div>}
    <div className="personnel-heading">
      <div className="personnel-menu-anchor" data-personnel-menu><button className="personnel-title" aria-expanded={panel === 'scope'} onClick={e => togglePanel('scope', e.currentTarget)}><h1>{status ? `Lớp học · ${statusLabels[status as keyof typeof statusLabels]}` : 'Tất cả lớp học'}</h1><ChevronDown size={16} /></button>{panel === 'scope' && statusMenu(true)}</div>
      <div className="personnel-actions">
        <button className="personnel-overview-toggle" aria-expanded={showOverview} aria-controls="class-overview" onClick={() => setShowOverview(!showOverview)}>{showOverview ? <EyeOff size={15} /> : <Eye size={15} />}<span>{showOverview ? 'Ẩn tổng quan' : 'Xem tổng quan'}</span></button>
        <div className="personnel-density" aria-label="Mật độ bảng"><button className={compact ? 'is-active' : ''} aria-label="Dòng thu gọn" aria-pressed={compact} onClick={() => setCompact(true)}><List size={15} /></button><button className={!compact ? 'is-active' : ''} aria-label="Dòng rộng" aria-pressed={!compact} onClick={() => setCompact(false)}><Columns2 size={15} /></button></div>
        <div className="personnel-split-button" data-personnel-menu><button onClick={() => openForm()}><Plus size={15} />Thêm lớp học</button><button aria-label="Tùy chọn thêm" aria-expanded={panel === 'add'} onClick={e => togglePanel('add', e.currentTarget)}><ChevronDown size={14} /></button>{panel === 'add' && <div className="personnel-popover"><button onClick={() => openForm()}><Plus size={14} />Thêm lớp học</button><button onClick={() => { setShowNotice(true); setPanel(null); }}><Megaphone size={14} />Hướng dẫn thao tác</button></div>}</div>
        <div className="personnel-menu-anchor" data-personnel-menu><button className="personnel-icon" aria-label="Thao tác khác" aria-expanded={panel === 'more'} onClick={e => togglePanel('more', e.currentTarget)}><MoreHorizontal size={16} /></button>{panel === 'more' && <div className="personnel-popover"><button onClick={resetFilters}><RotateCcw size={14} />Đặt lại bộ lọc</button><button onClick={() => { setWidths({}); setHiddenColumns([]); setPanel(null); }}>Khôi phục các cột</button><button onClick={refresh}>Làm mới dữ liệu</button></div>}</div>
      </div>
    </div>
    {showOverview && <section id="class-overview" className="personnel-overview" aria-label="Báo cáo tổng quan lớp học">{stats.map(({ status: next, label, query: statQuery, icon: Icon, color }) => <button key={next} onClick={() => changeStatus(next)} className={status === next ? 'is-selected' : ''}><span className={`personnel-stat-icon ${color}`}><Icon size={18} /></span><span><small>{label}</small><strong>{statQuery.data?.totalCount ?? '–'}</strong></span></button>)}</section>}
    <section className="personnel-grid" aria-label="Danh sách lớp học">
      <div className="personnel-toolbar">
        <label className="personnel-search"><Search size={14} /><input aria-label="Tìm kiếm lớp học" placeholder="Tìm theo tên hoặc mã lớp..." maxLength={100} value={search} onChange={e => { setSearch(e.target.value); changePage(1); }} />{search && <button aria-label="Xóa tìm kiếm" onClick={() => { setSearch(''); changePage(1); }}><X size={13} /></button>}</label>
        <div className="personnel-select"><select aria-label="Quyền tham gia" value={accessType} onChange={e => { setAccess(e.target.value); changePage(1); }}><option value="">Tất cả quyền tham gia</option><option value="Public">Công khai</option><option value="CodeRequired">Cần mã tham gia</option></select><ChevronDown size={14} /></div>
        <div className="personnel-select"><select aria-label="Phân công giáo viên" value={hasTeacher} onChange={e => { setHasTeacher(e.target.value); changePage(1); }}><option value="">Tất cả phân công</option><option value="true">Có giáo viên</option><option value="false">Chưa có giáo viên</option></select><ChevronDown size={14} /></div>
        {selectedIds.length > 0 && <button className="personnel-selection-badge" onClick={() => setSelectedIds([])}>Đã chọn {selectedIds.length}<X size={12} /></button>}
        <div className="personnel-toolbar-actions">
          <div className="personnel-menu-anchor" data-personnel-menu><button className={`personnel-icon class-tool-button ${status || panel === 'filters' ? 'is-active' : ''}`} aria-label="Lọc trạng thái lớp học" title="Bộ lọc trạng thái" aria-expanded={panel === 'filters'} onClick={e => togglePanel('filters', e.currentTarget)}><Filter size={15} /><span>Bộ lọc</span>{status && <span className="personnel-filter-dot" />}</button>{panel === 'filters' && statusMenu()}</div>
          <div className="personnel-menu-anchor" data-personnel-menu><button className={`personnel-icon class-tool-button ${panel === 'columns' ? 'is-active' : ''}`} aria-label="Cài đặt cột hiển thị" title="Cài đặt cột" aria-expanded={panel === 'columns'} onClick={e => togglePanel('columns', e.currentTarget)}><Settings2 size={15} /><span>Cột hiển thị</span></button>{panel === 'columns' && <div className="personnel-popover personnel-column-options"><strong>Tùy chỉnh cột</strong><p>Kéo mép tiêu đề để đổi độ rộng.</p>{columns.map(c => <label key={c.id}><input type="checkbox" checked={!hiddenColumns.includes(c.id)} disabled={c.id === 'name'} onChange={() => setHiddenColumns(current => current.includes(c.id) ? current.filter(id => id !== c.id) : [...current, c.id])} />{c.label}</label>)}<button onClick={() => { setWidths({}); setHiddenColumns([]); }}>Khôi phục mặc định</button></div>}</div>
          <button className="personnel-icon class-tool-button" aria-label={selectedIds.length ? 'Xuất lớp đã chọn CSV' : 'Xuất danh sách CSV'} title={exporting ? 'Đang xuất báo cáo…' : selectedIds.length ? 'Xuất lớp đã chọn CSV' : 'Xuất danh sách CSV'} disabled={exporting || query.isLoading || query.isError} onClick={() => void exportCSV()}><FileOutput size={15} /><span>{exporting ? 'Đang xuất…' : 'Xuất báo cáo'}</span></button>
        </div>
      </div>
      <div ref={tableScrollRef} className={`personnel-table-scroll ${compact ? '' : 'personnel-comfortable'} ${actionsInView ? 'class-actions-in-view' : 'class-actions-offscreen'}`}>
        <table style={{ width: 236 + visibleColumns.reduce((sum, c) => sum + (widths[c.id] ?? c.width), 0) }}>
          <caption className="personnel-sr-only">Danh sách lớp học. Kéo mép phải tiêu đề cột hoặc dùng phím mũi tên để thay đổi độ rộng.</caption>
          <colgroup><col style={{ width: 48 }} /><col style={{ width: 64 }} />{visibleColumns.map(c => <col key={c.id} style={{ width: widths[c.id] ?? c.width }} />)}<col style={{ width: 124 }} /></colgroup>
          <thead><tr><th className="personnel-checkbox-cell"><input ref={selectAllRef} type="checkbox" aria-label="Chọn tất cả dòng trên trang" checked={items.length > 0 && selectedCount === items.length} onChange={e => setSelectedIds(e.target.checked ? items.map(c => c.id) : [])} /></th><th scope="col" className="class-number-cell">STT</th>{visibleColumns.map(c => <th key={c.id} className={c.id === 'status' ? 'personnel-status-cell' : undefined} scope="col" aria-sort={'sort' in c && sort === c.sort ? desc ? 'descending' : 'ascending' : 'none'}><button className="personnel-column-title" disabled={!('sort' in c)} onClick={() => { if ('sort' in c) { setSort(c.sort); setDesc(sort === c.sort ? !desc : false); changePage(1); } }}><span>{c.label}</span>{'sort' in c && sort === c.sort && <span>{desc ? '↓' : '↑'}</span>}</button><span className="personnel-resize-handle" role="separator" aria-label={`Độ rộng cột ${c.label}`} aria-orientation="vertical" aria-valuemin={c.min} aria-valuemax={600} aria-valuenow={widths[c.id] ?? c.width} tabIndex={0} onPointerDown={e => { e.preventDefault(); e.currentTarget.setPointerCapture(e.pointerId); resizeRef.current = { id: c.id, start: e.clientX, width: widths[c.id] ?? c.width }; rootRef.current?.classList.add('personnel-resizing'); }} onPointerMove={e => { const r = resizeRef.current; if (r?.id === c.id) setWidths(current => ({ ...current, [c.id]: Math.max(c.min, Math.min(600, r.width + e.clientX - r.start)) })); }} onPointerUp={endResize} onPointerCancel={endResize} onLostPointerCapture={endResize} onDoubleClick={() => setWidths(current => ({ ...current, [c.id]: c.width }))} onKeyDown={e => { if (e.key === 'ArrowLeft' || e.key === 'ArrowRight') { e.preventDefault(); setWidths(current => ({ ...current, [c.id]: Math.max(c.min, Math.min(600, (current[c.id] ?? c.width) + (e.key === 'ArrowRight' ? 16 : -16))) })); } }} /></th>)}<th ref={actionsHeadingRef} scope="col" className="class-actions-heading">Thao tác</th></tr></thead>
          <tbody>{items.map((item, index) => <tr key={item.id} className={selectedIds.includes(item.id) ? 'is-selected' : ''} onDoubleClick={() => openForm(item)}><td className="personnel-checkbox-cell"><input type="checkbox" aria-label={`Chọn ${item.name}`} checked={selectedIds.includes(item.id)} onChange={e => setSelectedIds(current => e.target.checked ? [...current, item.id] : current.filter(id => id !== item.id))} /></td><td className="class-number-cell">{start + index + 1}</td>{visibleColumns.map(c => <td key={c.id} className={c.id === 'status' ? 'personnel-status-cell' : undefined} title={valueFor(item, c.id)}>{c.id === 'name' ? <Link className="personnel-name" to={`/admin/classes/${item.id}`}><span>{item.name}</span></Link> : c.id === 'status' ? <span className={`personnel-status status-${item.status === 'Draft' ? 'Pending' : item.status === 'Archived' ? 'Disabled' : 'Active'}`}><i /><span>{statusLabels[item.status]}</span></span> : valueFor(item, c.id)}</td>)}<td className="class-actions-cell" onDoubleClick={e => e.stopPropagation()}><div className="class-row-actions"><Link className="personnel-row-action-btn" title="Xem chi tiết lớp học" aria-label={`Xem ${item.name}`} to={`/admin/classes/${item.id}`}><Eye size={15} /></Link><button className="personnel-row-action-btn edit" title="Chỉnh sửa lớp học" aria-label={`Chỉnh sửa ${item.name}`} onClick={() => openForm(item)}><Edit3 size={15} /></button><button className="personnel-row-action-btn danger" title="Xóa lớp học" aria-label={`Xóa ${item.name}`} onClick={() => setDeleting(item)}><Trash2 size={15} /></button></div></td></tr>)}</tbody>
        </table>
        {!items.length && !query.isLoading && !query.isError && <div className="personnel-empty"><Search size={28} /><strong>Không tìm thấy lớp học</strong><p>Thử từ khóa khác hoặc đặt lại bộ lọc.</p><button onClick={resetFilters}>Đặt lại bộ lọc</button></div>}
      </div>
      <footer className="personnel-pagination"><span>Tổng số: <strong>{total}</strong> lớp học</span><div><label htmlFor="class-page-size">Số dòng/trang</label><div className="personnel-select"><select id="class-page-size" value={pageSize} onChange={e => { setPageSize(Number(e.target.value)); changePage(1); }}>{[5, 10, 20, 50, 100].map(size => <option key={size}>{size}</option>)}</select><ChevronDown size={12} /></div><strong aria-live="polite">{total ? start + 1 : 0}–{Math.min(start + pageSize, total)}</strong><button className="personnel-icon" aria-label="Trang đầu" disabled={page === 1} onClick={() => changePage(1)}><ChevronsLeft size={14} /></button><button className="personnel-icon" aria-label="Trang trước" disabled={page === 1} onClick={() => changePage(page - 1)}><ChevronLeft size={14} /></button><button className="personnel-icon" aria-label="Trang sau" disabled={page >= totalPages} onClick={() => changePage(page + 1)}><ChevronRight size={14} /></button><button className="personnel-icon" aria-label="Trang cuối" disabled={page >= totalPages} onClick={() => changePage(totalPages)}><ChevronsRight size={14} /></button></div></footer>
    </section>
    {formOpen && <ClassFormModal initial={editing} onClose={() => setFormOpen(false)} />}
    {deleting && <ConfirmDialog title="Xóa lớp học" message={deleting.studentCount > 0 ? `Lớp ${deleting.name} còn ${deleting.studentCount} sinh viên. Xác nhận sẽ xóa mềm lớp và gỡ toàn bộ sinh viên đang tham gia.` : `Xác nhận xóa lớp ${deleting.name}?`} pending={mutation.isPending} onClose={() => setDeleting(null)} onConfirm={() => mutation.mutate({ path: `/admin/classes/${deleting.id}?force=${deleting.studentCount > 0}&confirm=true`, method: 'delete' }, { onSuccess: () => { setDeleting(null); setSelectedIds([]); } })} />}
  </div>;
}


