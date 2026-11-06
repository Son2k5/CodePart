import { useState, useEffect, useRef, useMemo } from 'react';
import { useParams } from 'react-router-dom';
import { toast } from 'sonner';
import { useQuery } from '@tanstack/react-query';
import {
  Search,
  X,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  ChevronsLeft,
  ChevronsRight,
  List,
  Columns2,
  Trash2,
  RotateCcw,
} from 'lucide-react';
import { useClassesQuery, useClassMutation, useDebounced, type ClassItem, type Page, type Student, type Teacher } from './api';
import { ConfirmDialog, QueryState, TeacherPicker } from './ClassUi';
import { ClassProblemsPanel } from '../code-judge/ClassProblemsPanel';
import { ClassAnalytics } from './ClassAnalytics';
import { ClassEditModal } from './ClassEditModal';
import { judgeApi } from '../code-judge/api';
import '../admin/styles/personnel.css';

const avatarColors = ['#35b6cf', '#55bb70', '#9a7cf0', '#f58b53', '#ed7099', '#5b92d9'];
const getInitials = (name: string) => {
  const parts = name.trim().split(' ').filter(Boolean);
  if (parts.length >= 2) {
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  }
  return name.slice(0, 2).toUpperCase();
};

export function ClassDetailPage() {
  const { id } = useParams();
  const root = `/admin/classes/${id}`;
  const query = useClassesQuery<ClassItem>(root);
  const mutation = useClassMutation();

  // Báo cáo & Thống kê hiển thị đầu tiên theo yêu cầu
  const [tab, setTab] = useState<'analytics' | 'problems' | 'students' | 'teachers' | 'info'>('analytics');
  const [teachers, setTeachers] = useState<Teacher[]>([]);

  // Student table states (Personnel Table System)
  const [studentSearch, setStudentSearch] = useState('');
  const [studentPage, setStudentPage] = useState(1);
  const [studentPageSize, setStudentPageSize] = useState(20);
  const [studentCompact, setStudentCompact] = useState(true);
  const [selectedStudentIds, setSelectedStudentIds] = useState<string[]>([]);
  const [joinFilter, setJoinFilter] = useState('');
  const debouncedStudentSearch = useDebounced(studentSearch);
  const selectAllStudentsRef = useRef<HTMLInputElement>(null);

  // Teacher table states
  const [teacherSearch, setTeacherSearch] = useState('');
  const [teacherCompact, setTeacherCompact] = useState(true);
  const [isAssignTeacherModalOpen, setIsAssignTeacherModalOpen] = useState(false);

  const [expiry, setExpiry] = useState('');
  const [copied, setCopied] = useState(false);
  const [customJoinCode, setCustomJoinCode] = useState('');
  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [confirm, setConfirm] = useState<{
    title: string;
    message: string;
    path: string;
    method: 'post' | 'delete';
    data?: unknown;
    onCustomConfirm?: () => Promise<void> | void;
  } | null>(null);

  const c = query.data;

  useEffect(() => {
    if (c?.joinCode) {
      setCustomJoinCode(c.joinCode);
    }
  }, [c?.joinCode]);

  const students = useClassesQuery<Page<Student>>(
    `${root}/students`,
    { search: debouncedStudentSearch, page: studentPage, pageSize: studentPageSize },
    tab === 'students'
  );

  const pageStudents = useMemo(() => {
    const raw = students.data?.items ?? [];
    if (!joinFilter) return raw;
    return raw.filter(s => (s.joinMethod || '').toLowerCase() === joinFilter.toLowerCase());
  }, [students.data?.items, joinFilter]);

  const totalStudents = students.data?.totalCount ?? 0;
  const totalStudentPages = Math.max(1, students.data?.totalPages ?? 1);
  const startStudentIndex = (studentPage - 1) * studentPageSize;
  const selectedStudentsOnPage = pageStudents.filter(s => selectedStudentIds.includes(s.id)).length;

  useEffect(() => {
    if (selectAllStudentsRef.current) {
      selectAllStudentsRef.current.indeterminate =
        selectedStudentsOnPage > 0 && selectedStudentsOnPage < pageStudents.length;
    }
  }, [selectedStudentsOnPage, pageStudents.length]);

  const filteredAssignedTeachers = useMemo(() => {
    const list = c?.teachers ?? [];
    if (!teacherSearch) return list;
    const q = teacherSearch.toLowerCase();
    return list.filter(t => t.fullName.toLowerCase().includes(q) || t.email.toLowerCase().includes(q));
  }, [c?.teachers, teacherSearch]);

  const handleBulkDeleteStudents = () => {
    if (!selectedStudentIds.length) return;
    setConfirm({
      title: `Gỡ ${selectedStudentIds.length} học sinh khỏi lớp`,
      message: `Bạn có chắc muốn gỡ ${selectedStudentIds.length} học sinh đã chọn khỏi lớp này? Dữ liệu bài nộp vẫn được giữ trong hệ thống.`,
      path: '',
      method: 'delete',
      onCustomConfirm: async () => {
        try {
          for (const sid of selectedStudentIds) {
            await mutation.mutateAsync({
              path: `${root}/students/${sid}`,
              method: 'delete',
            });
          }
          setSelectedStudentIds([]);
          void students.refetch();
          toast.success(`Đã gỡ ${selectedStudentIds.length} học sinh khỏi lớp`);
        } catch {
          toast.error('Có lỗi xảy ra khi gỡ học sinh');
        }
      },
    });
  };

  const problemsQuery = useQuery({
    queryKey: ['judge-admin-list', 1, `class:${id}`],
    queryFn: () => judgeApi.list(true, 1, `class:${id}`),
    enabled: !!id,
  });

  const handleCopyCode = (code: string) => {
    void navigator.clipboard.writeText(code).then(
      () => {
        setCopied(true);
        toast.success('Đã sao chép mã tham gia lớp học');
        setTimeout(() => setCopied(false), 2000);
      },
      () => toast.error('Không thể sao chép mã tham gia')
    );
  };

  const handleSaveJoinCode = () => {
    mutation.mutate({
      path: `${root}/join-code/regenerate`,
      method: 'post',
      data: {
        code: customJoinCode ? customJoinCode.trim().toUpperCase() : null,
        expiresAt: expiry ? new Date(expiry).toISOString() : null,
      },
    });
  };

  const statusBadge = (status: 'Draft' | 'Active' | 'Archived') => {
    if (status === 'Active') {
      return (
        <span className="inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-bold rounded border border-emerald-200 bg-emerald-50 text-emerald-700">
          <span className="w-1.5 h-1.5 rounded-full bg-emerald-600" />
          Đang hoạt động
        </span>
      );
    }
    if (status === 'Draft') {
      return (
        <span className="inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-bold rounded border border-amber-200 bg-amber-50 text-amber-700">
          <span className="w-1.5 h-1.5 rounded-full bg-amber-500" />
          Bản nháp
        </span>
      );
    }
    return (
      <span className="inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-bold rounded border border-slate-200 bg-slate-100 text-slate-600">
        <span className="w-1.5 h-1.5 rounded-full bg-slate-400" />
        Đã lưu trữ
      </span>
    );
  };

  const accessBadge = (type: 'Public' | 'CodeRequired') => {
    if (type === 'Public') {
      return (
        <span className="px-2.5 py-1 text-xs font-bold rounded border border-blue-200 bg-blue-50 text-blue-700">
          Công khai
        </span>
      );
    }
    return (
      <span className="px-2.5 py-1 text-xs font-bold rounded border border-purple-200 bg-purple-50 text-purple-700">
        Cần mã tham gia
      </span>
    );
  };

  return (
    <div className="w-full text-slate-800 font-sans space-y-4">
      <QueryState loading={query.isLoading} error={query.error} retry={() => query.refetch()} />

      {c && (
        <div className="space-y-4">
          {/* Unified Top Card: Khung viền xanh dương nhẹ nhàng, không có icon */}
          <div className="bg-white border border-blue-200/90 rounded-none shadow-none overflow-hidden border-t-3 border-t-blue-600">
            {/* Row 1: Topic Title and Action Buttons on the SAME ROW */}
            <div className="p-5 flex flex-col md:flex-row md:items-center justify-between gap-4">
              <div className="flex items-center gap-4 min-w-0">
                {c.thumbnail && (
                  <div className="w-20 h-14 shrink-0 border border-slate-300 bg-slate-100 overflow-hidden rounded-none">
                    <img src={c.thumbnail} alt={c.name} className="w-full h-full object-cover rounded-none" />
                  </div>
                )}
                <div className="min-w-0">
                  <h1 className="text-2xl font-bold tracking-tight text-slate-900 truncate">
                    {c.name}
                  </h1>
                  <p className="text-xs text-slate-500 mt-1 line-clamp-1">
                    {c.description || 'Chưa có thông tin mô tả chi tiết cho lớp học này.'}
                  </p>
                </div>
              </div>

              {/* Action Buttons: Cùng row với thanh topic, mở popup sửa thông tin trực tiếp */}
              <div className="flex items-center gap-2.5 flex-wrap shrink-0">
                <div className="flex items-center gap-1.5">
                  <span className="text-xs text-slate-500 font-medium">Trạng thái:</span>
                  <select
                    value={c.status}
                    disabled={mutation.isPending}
                    onChange={e =>
                      mutation.mutate({
                        path: `${root}/status`,
                        method: 'patch',
                        data: { status: e.target.value },
                      })
                    }
                    className="border border-blue-200 rounded-none px-2.5 py-1.5 text-xs font-semibold bg-white text-slate-700 hover:border-blue-400 focus:border-blue-500 cursor-pointer focus:outline-none"
                  >
                    <option value="Draft">Draft (Bản nháp)</option>
                    <option value="Active">Active (Đang chạy)</option>
                    <option value="Archived">Archived (Lưu trữ)</option>
                  </select>
                </div>

                <button
                  type="button"
                  onClick={() => setIsEditModalOpen(true)}
                  className="px-4 py-1.5 text-xs font-semibold rounded-none bg-blue-600 hover:bg-blue-700 text-white transition-colors inline-flex items-center shadow-none cursor-pointer"
                >
                  Sửa thông tin
                </button>
              </div>
            </div>

            {/* Row 2: Thanh Tab điều hướng gộp trực tiếp ở đáy khung trên, không dùng icon */}
            <div className="border-t border-blue-100 bg-blue-50/30 flex items-center gap-1 sm:gap-2 px-3 overflow-x-auto">
              {[
                { key: 'analytics', label: 'Báo cáo & Thống kê' },
                { key: 'problems', label: 'Bài tập lập trình', badge: problemsQuery.data?.items?.length },
                { key: 'students', label: 'Học sinh', badge: c.studentCount },
                { key: 'teachers', label: 'Giáo viên', badge: c.teachers.length },
                { key: 'info', label: 'Thông tin & Cài đặt' },
              ].map(t => {
                const isActive = tab === t.key;
                return (
                  <button
                    key={t.key}
                    type="button"
                    onClick={() => setTab(t.key as typeof tab)}
                    className={`px-4 py-3 text-xs font-semibold border-b-2 transition-colors cursor-pointer inline-flex items-center gap-2 whitespace-nowrap ${
                      isActive
                        ? 'border-blue-600 text-blue-700 font-bold bg-white'
                        : 'border-transparent text-slate-600 hover:text-blue-600 hover:bg-white/50'
                    }`}
                  >
                    <span>{t.label}</span>
                    {t.badge !== undefined && (
                      <span
                        className={`px-1.5 py-0.2 rounded text-[11px] font-bold ${
                          isActive
                            ? 'bg-blue-100 text-blue-800 border border-blue-200'
                            : 'bg-slate-100 text-slate-600 border border-slate-200'
                        }`}
                      >
                        {t.badge}
                      </span>
                    )}
                  </button>
                );
              })}
            </div>
          </div>

          {/* Tab 1 (Default): Báo cáo & Thống kê hiển thị đầu tiên */}
          {tab === 'analytics' && (
            <ClassAnalytics
              classItem={c}
              problems={problemsQuery.data?.items}
              loadingProblems={problemsQuery.isLoading}
            />
          )}

          {/* Tab 2: Bài tập lập trình */}
          {tab === 'problems' && (
            <div>
              <ClassProblemsPanel id={id!} kind="class" manage />
            </div>
          )}

          {/* Tab 3: Học sinh - Hệ thống bảng quản lí nhân sự */}
          {tab === 'students' && (
            <div className="personnel-page personnel-management !min-h-0 !p-0 !bg-transparent space-y-4">
              <section className="personnel-grid bg-white border border-emerald-200/90 rounded-none overflow-hidden shadow-none border-t-3 border-t-emerald-500" aria-label="Danh sách học sinh">
                {/* Header Band */}
                <div className="px-5 py-4 bg-emerald-50/40 border-b border-emerald-100 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                  <div>
                    <h2 className="text-base font-bold text-slate-900">Danh sách học sinh tham gia</h2>
                    <p className="text-xs text-slate-500 mt-0.5">
                      Quản lý toàn bộ {c.studentCount} học sinh trong lớp học. Theo dõi trạng thái và thời gian gia nhập.
                    </p>
                  </div>
                  <span className="px-3 py-1 text-xs font-semibold rounded-none bg-emerald-100/70 border border-emerald-200 text-emerald-800 shrink-0 self-start sm:self-auto">
                    Sĩ số: <strong>{c.studentCount}</strong> / {c.maxStudents ? c.maxStudents : '∞'} học sinh
                  </span>
                </div>

                {/* Personnel Toolbar */}
                <div className="personnel-toolbar">
                  <label className="personnel-search">
                    <Search size={14} />
                    <input
                      aria-label="Tìm kiếm học sinh"
                      placeholder="Tìm theo tên hoặc email học sinh..."
                      maxLength={100}
                      value={studentSearch}
                      onChange={e => {
                        setStudentSearch(e.target.value);
                        setStudentPage(1);
                        setSelectedStudentIds([]);
                      }}
                    />
                    {studentSearch && (
                      <button
                        type="button"
                        aria-label="Xóa tìm kiếm"
                        onClick={() => {
                          setStudentSearch('');
                          setStudentPage(1);
                        }}
                      >
                        <X size={13} />
                      </button>
                    )}
                  </label>

                  <div className="personnel-select">
                    <select
                      aria-label="Lọc hình thức tham gia"
                      value={joinFilter}
                      onChange={e => {
                        setJoinFilter(e.target.value);
                        setStudentPage(1);
                      }}
                    >
                      <option value="">Tất cả hình thức tham gia</option>
                      <option value="code">Mã tham gia</option>
                      <option value="direct">Thêm trực tiếp</option>
                      <option value="public">Ghi danh công khai</option>
                    </select>
                    <ChevronDown size={14} />
                  </div>

                  {selectedStudentIds.length > 0 && (
                    <button
                      type="button"
                      className="personnel-selection-badge"
                      onClick={() => setSelectedStudentIds([])}
                    >
                      Đã chọn {selectedStudentIds.length} <X size={12} />
                    </button>
                  )}

                  {selectedStudentIds.length > 0 && (
                    <div className="personnel-bulk-actions">
                      <button
                        type="button"
                        className="personnel-btn-danger"
                        onClick={handleBulkDeleteStudents}
                      >
                        <Trash2 size={14} /> Gỡ khỏi lớp ({selectedStudentIds.length})
                      </button>
                    </div>
                  )}

                  <div className="personnel-toolbar-actions">
                    <div className="personnel-density" aria-label="Mật độ bảng">
                      <button
                        type="button"
                        className={studentCompact ? 'is-active' : ''}
                        aria-label="Dòng thu gọn"
                        title="Dòng thu gọn"
                        aria-pressed={studentCompact}
                        onClick={() => setStudentCompact(true)}
                      >
                        <List size={15} />
                      </button>
                      <button
                        type="button"
                        className={!studentCompact ? 'is-active' : ''}
                        aria-label="Dòng rộng"
                        title="Dòng rộng"
                        aria-pressed={!studentCompact}
                        onClick={() => setStudentCompact(false)}
                      >
                        <Columns2 size={15} />
                      </button>
                    </div>

                    <button
                      type="button"
                      className="personnel-icon class-tool-button"
                      title="Làm mới danh sách"
                      onClick={() => void students.refetch()}
                    >
                      <RotateCcw size={14} />
                      <span>Làm mới</span>
                    </button>
                  </div>
                </div>

                {/* Personnel Table Scroll */}
                <div className={`personnel-table-scroll ${studentCompact ? '' : 'personnel-comfortable'}`}>
                  <table style={{ width: '100%', minWidth: 700 }}>
                    <caption className="personnel-sr-only">Danh sách học sinh của lớp học</caption>
                    <colgroup>
                      <col style={{ width: 44 }} />
                      <col style={{ width: 56 }} />
                      <col style={{ minWidth: 200 }} />
                      <col style={{ minWidth: 220 }} />
                      <col style={{ width: 150 }} />
                      <col style={{ width: 170 }} />
                      <col style={{ width: 100 }} />
                    </colgroup>
                    <thead>
                      <tr>
                        <th className="personnel-checkbox-cell">
                          <input
                            ref={selectAllStudentsRef}
                            type="checkbox"
                            aria-label="Chọn tất cả học sinh trên trang"
                            checked={pageStudents.length > 0 && selectedStudentsOnPage === pageStudents.length}
                            disabled={!pageStudents.length || students.isFetching}
                            onChange={e =>
                              setSelectedStudentIds(
                                e.target.checked ? pageStudents.map(s => s.id) : []
                              )
                            }
                          />
                        </th>
                        <th scope="col" className="class-number-cell">STT</th>
                        <th scope="col"><span className="personnel-column-title">Học sinh</span></th>
                        <th scope="col"><span className="personnel-column-title">Email trường</span></th>
                        <th scope="col" className="personnel-status-cell"><span className="personnel-column-title">Hình thức</span></th>
                        <th scope="col"><span className="personnel-column-title">Ngày tham gia</span></th>
                        <th scope="col" className="class-actions-heading">Thao tác</th>
                      </tr>
                    </thead>
                    <tbody>
                      {students.isPending && (
                        <tr>
                          <td colSpan={7} className="p-8 text-center text-slate-500">
                            Đang tải danh sách học sinh…
                          </td>
                        </tr>
                      )}
                      {students.isError && (
                        <tr>
                          <td colSpan={7} className="p-6 text-center text-rose-600">
                            Không thể tải danh sách học sinh.{' '}
                            <button
                              type="button"
                              onClick={() => void students.refetch()}
                              className="underline ml-2 cursor-pointer font-semibold"
                            >
                              Thử lại
                            </button>
                          </td>
                        </tr>
                      )}
                      {!students.isPending && !students.isError && pageStudents.length === 0 && (
                        <tr>
                          <td colSpan={7}>
                            <div className="personnel-empty">
                              <Search size={28} />
                              <strong>Không tìm thấy học sinh</strong>
                              <p>
                                {studentSearch || joinFilter
                                  ? 'Thử thay đổi từ khóa hoặc bộ lọc phương thức.'
                                  : 'Chưa có học sinh nào tham gia lớp học này.'}
                              </p>
                              {(studentSearch || joinFilter) && (
                                <button
                                  type="button"
                                  onClick={() => {
                                    setStudentSearch('');
                                    setJoinFilter('');
                                    setStudentPage(1);
                                  }}
                                >
                                  Đặt lại bộ lọc
                                </button>
                              )}
                            </div>
                          </td>
                        </tr>
                      )}
                      {!students.isPending && !students.isError && pageStudents.map((s, index) => {
                        const isSelected = selectedStudentIds.includes(s.id);
                        const initials = getInitials(s.fullName);
                        const avatarBg = avatarColors[(startStudentIndex + index) % avatarColors.length];
                        return (
                          <tr key={s.id} className={isSelected ? 'is-selected' : ''}>
                            <td className="personnel-checkbox-cell">
                              <input
                                type="checkbox"
                                aria-label={`Chọn ${s.fullName}`}
                                checked={isSelected}
                                onChange={e =>
                                  setSelectedStudentIds(curr =>
                                    e.target.checked ? [...curr, s.id] : curr.filter(id => id !== s.id)
                                  )
                                }
                              />
                            </td>
                            <td className="class-number-cell">{startStudentIndex + index + 1}</td>
                            <td>
                              <div className="personnel-name">
                                <span className="personnel-avatar" style={{ background: avatarBg }}>
                                  {initials}
                                </span>
                                <span>{s.fullName}</span>
                              </div>
                            </td>
                            <td>
                              <a className="personnel-email" href={`mailto:${s.email}`}>
                                {s.email}
                              </a>
                            </td>
                            <td className="personnel-status-cell">
                              <span className="personnel-status status-Active">
                                <i />
                                <span>
                                  {s.joinMethod === 'code'
                                    ? 'Mã tham gia'
                                    : s.joinMethod === 'direct'
                                    ? 'Trực tiếp'
                                    : 'Ghi danh'}
                                </span>
                              </span>
                            </td>
                            <td>
                              {s.joinedAt
                                ? new Date(s.joinedAt).toLocaleString('vi-VN', {
                                    day: '2-digit',
                                    month: '2-digit',
                                    year: 'numeric',
                                    hour: '2-digit',
                                    minute: '2-digit',
                                  })
                                : '–'}
                            </td>
                            <td className="class-actions-cell">
                              <div className="class-row-actions">
                                <button
                                  type="button"
                                  className="personnel-row-action-btn danger"
                                  title={`Gỡ học sinh ${s.fullName} khỏi lớp`}
                                  aria-label={`Gỡ ${s.fullName}`}
                                  disabled={mutation.isPending}
                                  onClick={() =>
                                    setConfirm({
                                      title: 'Xóa học sinh khỏi lớp',
                                      message: `Bạn có chắc muốn xóa học sinh ${s.fullName} (${s.email}) khỏi lớp này? Dữ liệu bài nộp vẫn được giữ trong hệ thống.`,
                                      path: `${root}/students/${s.id}`,
                                      method: 'delete',
                                    })
                                  }
                                >
                                  <Trash2 size={15} />
                                </button>
                              </div>
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>

                {/* Personnel Pagination Footer */}
                <footer className="personnel-pagination">
                  <span>Tổng số: <strong>{totalStudents}</strong> học sinh</span>
                  <div>
                    <label htmlFor="student-page-size">Số dòng/trang</label>
                    <div className="personnel-select">
                      <select
                        id="student-page-size"
                        value={studentPageSize}
                        onChange={e => {
                          setStudentPageSize(Number(e.target.value));
                          setStudentPage(1);
                        }}
                      >
                        {[10, 20, 50, 100].map(sz => (
                          <option key={sz} value={sz}>{sz}</option>
                        ))}
                      </select>
                      <ChevronDown size={12} />
                    </div>
                    <strong aria-live="polite">
                      {totalStudents ? startStudentIndex + 1 : 0}–{Math.min(startStudentIndex + studentPageSize, totalStudents)}
                    </strong>
                    <button
                      type="button"
                      className="personnel-icon"
                      aria-label="Trang đầu"
                      disabled={studentPage === 1}
                      onClick={() => setStudentPage(1)}
                    >
                      <ChevronsLeft size={14} />
                    </button>
                    <button
                      type="button"
                      className="personnel-icon"
                      aria-label="Trang trước"
                      disabled={studentPage === 1}
                      onClick={() => setStudentPage(studentPage - 1)}
                    >
                      <ChevronLeft size={14} />
                    </button>
                    <button
                      type="button"
                      className="personnel-icon"
                      aria-label="Trang sau"
                      disabled={studentPage >= totalStudentPages}
                      onClick={() => setStudentPage(studentPage + 1)}
                    >
                      <ChevronRight size={14} />
                    </button>
                    <button
                      type="button"
                      className="personnel-icon"
                      aria-label="Trang cuối"
                      disabled={studentPage >= totalStudentPages}
                      onClick={() => setStudentPage(totalStudentPages)}
                    >
                      <ChevronsRight size={14} />
                    </button>
                  </div>
                </footer>
              </section>
            </div>
          )}

          {/* Tab 4: Giáo viên - Hệ thống bảng quản lí nhân sự */}
          {tab === 'teachers' && (
            <div className="personnel-page personnel-management !min-h-0 !p-0 !bg-transparent space-y-5">
              {/* Bảng 1: Đội ngũ giáo viên hiện tại */}
              <section className="personnel-grid bg-white border border-emerald-200/90 rounded-none overflow-hidden shadow-none border-t-3 border-t-emerald-500" aria-label="Đội ngũ giáo viên hiện tại">
                <div className="px-5 py-4 bg-emerald-50/40 border-b border-emerald-100 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                  <div>
                    <h2 className="text-base font-bold text-slate-900">
                      Đội ngũ giáo viên phụ trách ({c.teachers.length})
                    </h2>
                    <p className="text-xs text-slate-500 mt-0.5">
                      Giảng viên có toàn quyền xem bài nộp, quản lý học liệu và xuất bản bài tập cho lớp
                    </p>
                  </div>
                  <button
                    type="button"
                    onClick={() => setIsAssignTeacherModalOpen(true)}
                    className="px-3.5 py-1.5 text-xs font-semibold rounded-none bg-emerald-600 hover:bg-emerald-700 text-white transition-colors cursor-pointer shrink-0 inline-flex items-center gap-1.5"
                  >
                    + Phân công giáo viên mới
                  </button>
                </div>

                <div className="personnel-toolbar">
                  <label className="personnel-search">
                    <Search size={14} />
                    <input
                      aria-label="Tìm giáo viên trong lớp"
                      placeholder="Tìm theo tên hoặc email giáo viên..."
                      value={teacherSearch}
                      onChange={e => setTeacherSearch(e.target.value)}
                    />
                    {teacherSearch && (
                      <button
                        type="button"
                        aria-label="Xóa tìm kiếm"
                        onClick={() => setTeacherSearch('')}
                      >
                        <X size={13} />
                      </button>
                    )}
                  </label>

                  <div className="personnel-toolbar-actions">
                    <div className="personnel-density" aria-label="Mật độ bảng">
                      <button
                        type="button"
                        className={teacherCompact ? 'is-active' : ''}
                        aria-label="Dòng thu gọn"
                        title="Dòng thu gọn"
                        aria-pressed={teacherCompact}
                        onClick={() => setTeacherCompact(true)}
                      >
                        <List size={15} />
                      </button>
                      <button
                        type="button"
                        className={!teacherCompact ? 'is-active' : ''}
                        aria-label="Dòng rộng"
                        title="Dòng rộng"
                        aria-pressed={!teacherCompact}
                        onClick={() => setTeacherCompact(false)}
                      >
                        <Columns2 size={15} />
                      </button>
                    </div>

                    <span className="text-xs text-slate-500 font-medium">
                      Hiển thị {filteredAssignedTeachers.length}/{c.teachers.length} giáo viên
                    </span>
                  </div>
                </div>

                <div className={`personnel-table-scroll ${teacherCompact ? '' : 'personnel-comfortable'} !min-h-0 !h-auto max-h-[360px]`}>
                  <table style={{ width: '100%', minWidth: 640 }}>
                    <caption className="personnel-sr-only">Danh sách giáo viên phụ trách lớp</caption>
                    <colgroup>
                      <col style={{ width: 56 }} />
                      <col style={{ minWidth: 240 }} />
                      <col style={{ minWidth: 260 }} />
                      <col style={{ width: 180 }} />
                      <col style={{ width: 160 }} />
                      <col style={{ width: 100 }} />
                    </colgroup>
                    <thead>
                      <tr>
                        <th scope="col" className="class-number-cell">STT</th>
                        <th scope="col"><span className="personnel-column-title">Giảng viên</span></th>
                        <th scope="col"><span className="personnel-column-title">Email trường</span></th>
                        <th scope="col" className="personnel-status-cell"><span className="personnel-column-title">Vai trò</span></th>
                        <th scope="col"><span className="personnel-column-title">Quyền hạn</span></th>
                        <th scope="col" className="class-actions-heading">Thao tác</th>
                      </tr>
                    </thead>
                    <tbody>
                      {filteredAssignedTeachers.length === 0 ? (
                        <tr>
                          <td colSpan={6}>
                            <div className="personnel-empty">
                              <Search size={28} />
                              <strong>Chưa có giáo viên nào</strong>
                              <p>
                                {teacherSearch
                                  ? 'Không tìm thấy giảng viên khớp với từ khóa tìm kiếm.'
                                  : 'Chưa có giáo viên nào được phân công cho lớp này.'}
                              </p>
                              {teacherSearch ? (
                                <button type="button" onClick={() => setTeacherSearch('')}>
                                  Đặt lại tìm kiếm
                                </button>
                              ) : (
                                <button
                                  type="button"
                                  onClick={() => setIsAssignTeacherModalOpen(true)}
                                  className="mt-1 px-3.5 py-1.5 text-xs font-semibold rounded-none bg-blue-600 hover:bg-blue-700 text-white cursor-pointer transition-colors"
                                >
                                  + Phân công giáo viên ngay
                                </button>
                              )}
                            </div>
                          </td>
                        </tr>
                      ) : (
                        filteredAssignedTeachers.map((t, index) => {
                          const initials = getInitials(t.fullName);
                          const avatarBg = avatarColors[index % avatarColors.length];
                          return (
                            <tr key={t.id}>
                              <td className="class-number-cell">{index + 1}</td>
                              <td>
                                <div className="personnel-name">
                                  <span className="personnel-avatar" style={{ background: avatarBg }}>
                                    {initials}
                                  </span>
                                  <span>{t.fullName}</span>
                                </div>
                              </td>
                              <td>
                                <a className="personnel-email" href={`mailto:${t.email}`}>
                                  {t.email}
                                </a>
                              </td>
                              <td className="personnel-status-cell">
                                <span className="personnel-status status-Active">
                                  <i />
                                  <span>Giảng viên phụ trách</span>
                                </span>
                              </td>
                              <td>
                                <span className="text-xs text-slate-600 font-medium">Toàn quyền quản trị</span>
                              </td>
                              <td className="class-actions-cell">
                                <div className="class-row-actions">
                                  <button
                                    type="button"
                                    className="personnel-row-action-btn danger"
                                    title={`Gỡ giảng viên ${t.fullName}`}
                                    aria-label={`Gỡ ${t.fullName}`}
                                    disabled={mutation.isPending}
                                    onClick={() =>
                                      setConfirm({
                                        title: 'Gỡ quyền giáo viên',
                                        message: `Gỡ giảng viên ${t.fullName} (${t.email}) khỏi danh sách phụ trách lớp này?`,
                                        path: `${root}/teachers/${t.id}`,
                                        method: 'delete',
                                      })
                                    }
                                  >
                                    <Trash2 size={15} />
                                  </button>
                                </div>
                              </td>
                            </tr>
                          );
                        })
                      )}
                    </tbody>
                  </table>
                </div>

                <footer className="personnel-pagination">
                  <span>Tổng số: <strong>{c.teachers.length}</strong> giáo viên phụ trách</span>
                  <div></div>
                </footer>
              </section>
            </div>
          )}

          {/* Tab 5: Thông tin & Cài đặt */}
          {tab === 'info' && (
            <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
              {/* Left Column: Badges, Description, Metadata */}
              <div className="lg:col-span-2 space-y-4">
                <div className="bg-white border border-blue-200/90 rounded-none overflow-hidden shadow-none border-t-3 border-t-blue-500">
                  <div className="px-5 py-3.5 bg-blue-50/40 border-b border-blue-100 flex items-center gap-2.5 flex-wrap">
                    <span className="text-xs text-slate-600 font-semibold">Trạng thái:</span>
                    {statusBadge(c.status)}
                    <span className="text-xs text-slate-600 font-semibold ml-2">Quyền truy cập:</span>
                    {accessBadge(c.accessType)}
                  </div>

                  <div className="p-5">
                    <h3 className="text-sm font-bold text-slate-900 mb-2">
                      Mô tả khóa học
                    </h3>
                    <div className="text-sm text-slate-700 leading-relaxed whitespace-pre-line">
                      {c.description || (
                        <span className="text-slate-400 italic">Chưa có thông tin mô tả chi tiết cho lớp học này.</span>
                      )}
                    </div>
                  </div>
                </div>

                <div className="bg-white border border-emerald-200/90 rounded-none overflow-hidden shadow-none border-t-3 border-t-emerald-500">
                  <div className="px-5 py-3.5 bg-emerald-50/40 border-b border-emerald-100">
                    <h3 className="text-sm font-bold text-slate-900">
                      Cấu hình lớp học
                    </h3>
                  </div>

                  <div className="p-5">
                    <dl className="grid grid-cols-1 sm:grid-cols-2 gap-4 text-xs">
                      <div>
                        <dt className="text-slate-500 mb-1">Phương thức truy cập</dt>
                        <dd className="font-semibold text-slate-800">
                          {c.accessType === 'Public' ? 'Công khai (Sinh viên tự do đăng ký)' : 'Yêu cầu mã lớp (Cần code tham gia)'}
                        </dd>
                      </div>
                      <div>
                        <dt className="text-slate-500 mb-1">Giới hạn sĩ số tối đa</dt>
                        <dd className="font-semibold text-slate-800">
                          {c.maxStudents ? `${c.maxStudents} học sinh` : 'Không giới hạn'}
                        </dd>
                      </div>
                      <div>
                        <dt className="text-slate-500 mb-1">Ngày khởi tạo</dt>
                        <dd className="font-semibold text-slate-800">
                          {c.createdAt ? new Date(c.createdAt).toLocaleString('vi-VN') : 'Không rõ'}
                        </dd>
                      </div>
                      <div>
                        <dt className="text-slate-500 mb-1">Trạng thái tuyển sinh</dt>
                        <dd className="font-semibold text-slate-800">
                          {c.status === 'Active' ? 'Đang mở tiếp nhận' : c.status === 'Draft' ? 'Chưa công bố' : 'Đã khóa'}
                        </dd>
                      </div>
                    </dl>
                  </div>
                </div>
              </div>

              {/* Right Column: Schedule & Cài đặt mã tham gia chỉnh sửa được */}
              <div className="space-y-4">
                <div className="bg-white border border-emerald-200/90 rounded-none overflow-hidden shadow-none border-t-3 border-t-emerald-500">
                  <div className="px-5 py-3.5 bg-emerald-50/40 border-b border-emerald-100">
                    <h3 className="text-sm font-bold text-slate-900">
                      Lịch trình đào tạo
                    </h3>
                  </div>
                  <div className="p-5 space-y-3 text-xs">
                    <div>
                      <span className="text-slate-500 block mb-1">Thời gian bắt đầu</span>
                      <strong className="text-slate-900 font-semibold block">
                        {c.startDate ? new Date(c.startDate).toLocaleString('vi-VN') : 'Chưa thiết lập'}
                      </strong>
                    </div>
                    <div>
                      <span className="text-slate-500 block mb-1">Thời gian kết thúc</span>
                      <strong className="text-slate-900 font-semibold block">
                        {c.endDate ? new Date(c.endDate).toLocaleString('vi-VN') : 'Chưa thiết lập'}
                      </strong>
                    </div>
                  </div>
                </div>

                {/* Cài đặt mã tham gia */}
                <div className="bg-white border border-blue-200/90 rounded-none overflow-hidden shadow-none border-t-3 border-t-blue-600 bg-blue-50/20">
                  <div className="px-5 py-3.5 bg-blue-50/60 border-b border-blue-100">
                    <h3 className="text-sm font-bold text-slate-900">
                      Cài đặt mã tham gia lớp
                    </h3>
                  </div>

                  <div className="p-5">
                    {c.accessType === 'Public' ? (
                      <div className="text-xs text-slate-500 leading-relaxed">
                        Lớp này đang đặt ở chế độ <strong>Công khai</strong> nên sinh viên có thể tự do tham gia mà không cần mã code.
                        <p className="mt-2">
                          Nếu muốn yêu cầu mã tham gia, hãy bấm nút{' '}
                          <button
                            type="button"
                            onClick={() => setIsEditModalOpen(true)}
                            className="text-blue-600 hover:underline font-bold cursor-pointer"
                          >
                            Sửa thông tin
                          </button>{' '}
                          để chuyển sang <em>Cần mã tham gia</em>.
                        </p>
                      </div>
                    ) : (
                      <div className="space-y-3 text-xs">
                        <div>
                          <label htmlFor="custom-join-code-input" className="text-slate-700 font-semibold block mb-1">
                            Mã tham gia lớp học (Nhập hoặc chỉnh sửa)
                          </label>
                          <div className="flex items-center gap-2">
                            <input
                              id="custom-join-code-input"
                              type="text"
                              placeholder="Nhập mã tham gia (VD: JAVA2026)"
                              value={customJoinCode}
                              onChange={e => setCustomJoinCode(e.target.value.toUpperCase())}
                              className="flex-1 font-mono text-sm font-bold uppercase border border-blue-300 rounded-none px-3 py-1.5 bg-white text-slate-900 focus:outline-none focus:border-blue-500"
                            />
                            {c.joinCode && (
                              <button
                                type="button"
                                onClick={() => handleCopyCode(c.joinCode!)}
                                className="px-3 py-1.5 border border-blue-200 rounded-none font-semibold bg-white hover:bg-blue-50 text-blue-700 cursor-pointer shrink-0"
                              >
                                {copied ? 'Đã chép' : 'Sao chép'}
                              </button>
                            )}
                          </div>
                        </div>

                        <div>
                          <label htmlFor="expiry-datetime-input" className="text-slate-700 font-semibold block mb-1">
                            Hạn sử dụng mã (tùy chọn)
                          </label>
                          <input
                            id="expiry-datetime-input"
                            type="datetime-local"
                            value={expiry}
                            onChange={e => setExpiry(e.target.value)}
                            className="w-full border border-slate-300 rounded-none p-1.5 text-xs bg-white text-slate-900 focus:outline-none focus:border-blue-500"
                          />
                          <span className="text-[11px] text-slate-400 mt-0.5 block">
                            Hiện tại: {c.joinCodeExpiresAt ? new Date(c.joinCodeExpiresAt).toLocaleString('vi-VN') : 'Không giới hạn thời gian'}
                          </span>
                        </div>

                        <div className="flex items-center gap-2 pt-2">
                          <button
                            type="button"
                            disabled={mutation.isPending}
                            onClick={handleSaveJoinCode}
                            className="flex-1 py-1.5 text-xs font-semibold rounded-none bg-blue-600 hover:bg-blue-700 text-white cursor-pointer transition-colors shadow-none"
                          >
                            Lưu mã tham gia
                          </button>
                          <button
                            type="button"
                            disabled={mutation.isPending || !c.joinCode}
                            onClick={() =>
                              setConfirm({
                                title: 'Thu hồi mã lớp',
                                message: 'Học sinh mới sẽ không thể tham gia lớp bằng mã hiện tại nữa. Bạn có chắc muốn thu hồi?',
                                path: `${root}/join-code`,
                                method: 'delete',
                              })
                            }
                            className="py-1.5 px-3 text-xs font-semibold rounded-none border border-rose-300 bg-white text-rose-600 hover:bg-rose-50 disabled:opacity-40 cursor-pointer transition-colors"
                          >
                            Thu hồi
                          </button>
                        </div>
                      </div>
                    )}
                  </div>
                </div>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Modal Phân công giáo viên phụ trách */}
      {isAssignTeacherModalOpen && (
        <div className="fixed inset-0 z-50 bg-black/40 flex items-center justify-center p-3 sm:p-4">
          <div className="bg-white border border-slate-300 rounded-none w-[min(840px,calc(100vw-24px))] max-h-[92vh] flex flex-col shadow-none overflow-hidden font-sans text-slate-800">
            {/* Header */}
            <div className="px-6 py-4 border-b border-slate-200 bg-slate-50 flex items-center justify-between">
              <div>
                <h2 className="text-base font-bold text-slate-900">
                  Phân công giáo viên phụ trách
                </h2>
                <p className="text-xs text-slate-500 mt-0.5">
                  Tìm kiếm và chọn giảng viên từ danh mục nhà trường để phân công vào lớp học này
                </p>
              </div>
              <button
                type="button"
                aria-label="Đóng popup"
                disabled={mutation.isPending}
                onClick={() => {
                  setIsAssignTeacherModalOpen(false);
                  setTeachers([]);
                }}
                className="w-8 h-8 rounded-none flex items-center justify-center text-slate-500 hover:text-slate-800 hover:bg-slate-200/60 border border-slate-300 bg-white transition-colors cursor-pointer"
              >
                <X size={16} />
              </button>
            </div>

            {/* Modal Body */}
            <div className="flex-1 overflow-y-auto p-5">
              <TeacherPicker value={teachers} onChange={setTeachers} />
            </div>

            {/* Modal Footer */}
            <div className="px-6 py-3.5 border-t border-slate-200 bg-slate-50 flex items-center justify-between">
              <span className="text-xs text-slate-600">
                Đang chọn: <strong>{teachers.length}</strong> giáo viên
              </span>
              <div className="flex items-center gap-2.5">
                <button
                  type="button"
                  disabled={mutation.isPending}
                  onClick={() => {
                    setIsAssignTeacherModalOpen(false);
                    setTeachers([]);
                  }}
                  className="px-4 py-2 text-xs font-semibold rounded-none border border-slate-300 bg-white text-slate-700 hover:bg-slate-50 transition-colors cursor-pointer"
                >
                  Hủy
                </button>
                <button
                  type="button"
                  disabled={!teachers.length || mutation.isPending}
                  onClick={() => {
                    mutation.mutate(
                      { path: `${root}/teachers`, data: { teacherIds: teachers.map(t => t.id) } },
                      {
                        onSuccess: () => {
                          setTeachers([]);
                          setIsAssignTeacherModalOpen(false);
                          void query.refetch();
                          toast.success('Đã lưu phân công giáo viên thành công');
                        },
                      }
                    );
                  }}
                  className="px-5 py-2 text-xs font-semibold rounded-none bg-blue-600 hover:bg-blue-700 text-white disabled:opacity-40 transition-colors cursor-pointer shadow-none"
                >
                  {mutation.isPending ? 'Đang lưu…' : `Lưu phân công (${teachers.length} giáo viên)`}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Popup chỉnh sửa thông tin lớp học trực tiếp, không redirect */}
      {isEditModalOpen && c && (
        <ClassEditModal
          initial={c}
          onClose={() => setIsEditModalOpen(false)}
          onSuccess={() => void query.refetch()}
        />
      )}

      {/* Confirmation Dialog */}
      {confirm && (
        <ConfirmDialog
          title={confirm.title}
          message={confirm.message}
          pending={mutation.isPending}
          onClose={() => setConfirm(null)}
          onConfirm={async () => {
            if (confirm.onCustomConfirm) {
              await confirm.onCustomConfirm();
              setConfirm(null);
            } else {
              mutation.mutate(
                { path: confirm.path, method: confirm.method, data: confirm.data },
                { onSuccess: () => setConfirm(null) }
              );
            }
          }}
        />
      )}
    </div>
  );
}
