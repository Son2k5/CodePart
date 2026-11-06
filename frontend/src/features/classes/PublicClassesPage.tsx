import { useState, useMemo, useEffect } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import {
  BookOpen,
  Users,
  ArrowRight,
  Search,
  X,
  KeyRound,
  GraduationCap,
  Check,
  Compass,
  RotateCcw,
  LogOut,
  AlertTriangle,
  Calendar,
  Code2,
  ChevronLeft,
  ChevronRight,
} from 'lucide-react';
import { toast } from 'sonner';
import { MemberHeader } from '../home/MemberHeader';
import {
  useClassesQuery,
  useClassMutation,
  useDebounced,
  type Page,
  type PublicClass,
} from './api';
import '@fontsource/be-vietnam-pro/400.css';
import '@fontsource/be-vietnam-pro/500.css';
import '@fontsource/be-vietnam-pro/600.css';
import '@fontsource/be-vietnam-pro/700.css';

const normalize = (value: string) =>
  value
    .toLocaleLowerCase('vi')
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/đ/g, 'd')
    .trim();

const formatDate = (dateStr?: string | null) => {
  if (!dateStr) return null;
  try {
    const d = new Date(dateStr);
    return d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' });
  } catch {
    return null;
  }
};

const toneStyles = [
  { bg: 'from-sky-50 via-cyan-50 to-blue-100', icon: 'text-sky-500' },
  { bg: 'from-amber-50 via-orange-50 to-yellow-100', icon: 'text-amber-500' },
  { bg: 'from-purple-50 via-violet-50 to-indigo-100', icon: 'text-purple-500' },
  { bg: 'from-emerald-50 via-teal-50 to-green-100', icon: 'text-emerald-500' },
];

export function PublicClassesPage() {
  const [params, setParams] = useSearchParams();
  const urlTab = params.get('tab');
  const [tab, setTab] = useState<'my' | 'explore' | 'all'>(
    urlTab === 'explore' || urlTab === 'all' || urlTab === 'my' ? urlTab : 'my'
  );

  const [search, setSearch] = useState('');
  const debouncedSearch = useDebounced(search);
  const [page, setPage] = useState(1);
  const [sort, setSort] = useState<'createdAt' | 'name' | 'studentCount'>('createdAt');
  const [desc, setDesc] = useState(true);

  // Modal state for code-required class join
  const [targetClassForJoin, setTargetClassForJoin] = useState<PublicClass | null>(null);
  const [joinCode, setJoinCode] = useState('');
  const [confirmLeaveClass, setConfirmLeaveClass] = useState<{ id: string; name: string } | null>(null);

  const mutation = useClassMutation();

  // Queries
  const joinedQuery = useClassesQuery<{ id: string; name: string }[]>('/learning/classes');
  const publicQuery = useClassesQuery<Page<PublicClass>>('/classes/public', {
    page,
    pageSize: 12,
    search: debouncedSearch.trim() || undefined,
    sort,
    desc,
  });

  // Keep url in sync with tab
  const handleTabChange = (newTab: 'my' | 'explore' | 'all') => {
    setTab(newTab);
    setPage(1);
    setParams(prev => {
      const next = new URLSearchParams(prev);
      next.set('tab', newTab);
      return next;
    });
  };

  // Switch to explore if user has 0 enrolled classes on first load when tab is default
  useEffect(() => {
    if (joinedQuery.isSuccess && joinedQuery.data.length === 0 && !urlTab) {
      setTab('explore');
    }
  }, [joinedQuery.isSuccess, joinedQuery.data, urlTab]);

  // Map public classes details by ID
  const publicClassMap = useMemo(() => {
    const map = new Map<string, PublicClass>();
    if (publicQuery.data?.items) {
      publicQuery.data.items.forEach(c => map.set(c.id, c));
    }
    return map;
  }, [publicQuery.data?.items]);

  // Filtered enrolled classes (Client-side search)
  const enrolledClasses = useMemo(() => {
    if (!joinedQuery.data) return [];
    const queryNorm = normalize(debouncedSearch);

    return joinedQuery.data
      .map(item => {
        const enriched = publicClassMap.get(item.id);
        return {
          id: item.id,
          name: item.name,
          description: enriched?.description || 'Lớp học lập trình trong chương trình học của bạn.',
          thumbnail: enriched?.thumbnail || null,
          maxStudents: enriched?.maxStudents ?? null,
          studentCount: enriched?.studentCount ?? 0,
          startDate: enriched?.startDate || null,
          endDate: enriched?.endDate || null,
          isEnrolled: true,
        };
      })
      .filter(item => {
        if (!queryNorm) return true;
        return normalize(item.name).includes(queryNorm) || normalize(item.description).includes(queryNorm);
      });
  }, [joinedQuery.data, publicClassMap, debouncedSearch]);

  // Handle clicking "Tham gia lớp" on a class card
  const handleJoinClass = (c: PublicClass) => {
    mutation.mutate(
      { path: `/classes/${c.id}/join` },
      {
        onSuccess: () => {
          toast.success(`Tham gia lớp "${c.name}" thành công!`);
          setTab('my');
          setParams(prev => {
            const next = new URLSearchParams(prev);
            next.set('tab', 'my');
            return next;
          });
        },
        onError: (error: unknown) => {
          const axiosErr = error as { response?: { data?: { error?: { code?: string; message?: string }; code?: string; message?: string } } };
          const errCode = axiosErr?.response?.data?.error?.code || axiosErr?.response?.data?.code || '';
          const errMsg = axiosErr?.response?.data?.error?.message || axiosErr?.response?.data?.message || '';

          // If the class requires a join code or there is a method mismatch
          if (errCode === 'INVALID_JOIN_CODE' || errMsg.includes('mã') || errMsg.includes('Phương thức') || errMsg.includes('Code')) {
            setTargetClassForJoin(c);
            setJoinCode('');
          } else {
            toast.error(errMsg || 'Không thể tham gia lớp học.');
          }
        },
      }
    );
  };

  // Submit Join by code when prompted for a specific class
  const handleJoinByCode = (e: React.FormEvent) => {
    e.preventDefault();
    const cleanCode = joinCode.trim().toUpperCase();
    if (cleanCode.length !== 8) {
      toast.error('Mã lớp học phải có đúng 8 ký tự.');
      return;
    }

    mutation.mutate(
      { path: '/classes/join-by-code', data: { code: cleanCode } },
      {
        onSuccess: () => {
          setJoinCode('');
          setTargetClassForJoin(null);
          setTab('my');
          setParams(prev => {
            const next = new URLSearchParams(prev);
            next.set('tab', 'my');
            return next;
          });
          toast.success('Tham gia lớp học thành công!');
        },
      }
    );
  };

  // Submit Leave class
  const handleConfirmLeave = () => {
    if (!confirmLeaveClass) return;
    mutation.mutate(
      { path: `/classes/${confirmLeaveClass.id}/leave` },
      {
        onSuccess: () => {
          setConfirmLeaveClass(null);
          toast.success('Đã rời khỏi lớp học thành công.');
        },
      }
    );
  };

  const totalJoinedCount = joinedQuery.data?.length ?? 0;
  const isSearchActive = !!search.trim();

  // Reset search & filters
  const handleResetFilters = () => {
    setSearch('');
    setSort('createdAt');
    setDesc(true);
    setPage(1);
  };

  return (
    <div className="min-h-screen bg-[#fbfcfe] text-[#344c59] font-['Be_Vietnam_Pro',sans-serif] flex flex-col">
      {/* Reusable System Header */}
      <MemberHeader />

      <main id="member-classes-main" className="flex-1 w-full max-w-[1250px] mx-auto px-4 sm:px-6 lg:px-8 py-6 sm:py-8">
        {/* Simple Page Title Layout */}
        <div className="mb-6">
          <h1 className="text-2xl sm:text-3xl font-bold text-slate-800 tracking-tight">
            Khám phá lớp học của tôi
          </h1>
          <p className="mt-1 text-sm text-slate-500">
            Quản lý các lớp học bạn đã đăng ký và khám phá các lớp học mới.
          </p>
        </div>

        {/* Horizontal Navigation & Search Bar (Same Row, No Background) */}
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-3.5 mb-6">
          {/* 3 Modern Tab Buttons */}
          <div className="flex items-center gap-2 flex-wrap">
            <button
              type="button"
              onClick={() => handleTabChange('my')}
              className={`inline-flex items-center gap-2 px-4 py-2 rounded-xl text-xs sm:text-sm font-semibold transition-all duration-150 cursor-pointer ${
                tab === 'my'
                  ? 'bg-cyan-600 text-white shadow-xs shadow-cyan-600/25 ring-1 ring-cyan-600'
                  : 'bg-white hover:bg-slate-50 text-slate-600 hover:text-slate-900 border border-slate-200/90 shadow-2xs'
              }`}
            >
              <GraduationCap size={16} className={tab === 'my' ? 'text-white' : 'text-slate-400'} />
              <span>Lớp học của tôi</span>
              <span
                className={`px-1.5 py-0.2 rounded-full text-[11px] font-bold ${
                  tab === 'my'
                    ? 'bg-white/25 text-white'
                    : 'bg-slate-100 text-slate-600'
                }`}
              >
                {totalJoinedCount}
              </span>
            </button>

            <button
              type="button"
              onClick={() => handleTabChange('explore')}
              className={`inline-flex items-center gap-2 px-4 py-2 rounded-xl text-xs sm:text-sm font-semibold transition-all duration-150 cursor-pointer ${
                tab === 'explore'
                  ? 'bg-cyan-600 text-white shadow-xs shadow-cyan-600/25 ring-1 ring-cyan-600'
                  : 'bg-white hover:bg-slate-50 text-slate-600 hover:text-slate-900 border border-slate-200/90 shadow-2xs'
              }`}
            >
              <Compass size={16} className={tab === 'explore' ? 'text-white' : 'text-slate-400'} />
              <span>Khám phá lớp mới</span>
            </button>

            <button
              type="button"
              onClick={() => handleTabChange('all')}
              className={`inline-flex items-center gap-2 px-4 py-2 rounded-xl text-xs sm:text-sm font-semibold transition-all duration-150 cursor-pointer ${
                tab === 'all'
                  ? 'bg-cyan-600 text-white shadow-xs shadow-cyan-600/25 ring-1 ring-cyan-600'
                  : 'bg-white hover:bg-slate-50 text-slate-600 hover:text-slate-900 border border-slate-200/90 shadow-2xs'
              }`}
            >
              <BookOpen size={16} className={tab === 'all' ? 'text-white' : 'text-slate-400'} />
              <span>Tất cả lớp học</span>
            </button>
          </div>

          {/* Right-aligned Compact Search Bar & Controls */}
          <div className="flex items-center gap-2 w-full md:w-auto shrink-0">
            {/* Compact Search Input */}
            <div className="relative w-full md:w-60 lg:w-72">
              <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400 pointer-events-none" />
              <input
                type="search"
                value={search}
                onChange={e => {
                  setSearch(e.target.value);
                  setPage(1);
                }}
                placeholder={
                  tab === 'my'
                    ? 'Tìm lớp đã đăng ký...'
                    : 'Tìm tên lớp học...'
                }
                aria-label="Tìm kiếm lớp học"
                className="w-full pl-9 pr-8 py-2 text-xs sm:text-sm bg-white border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-cyan-500/20 focus:border-cyan-600 transition-all text-slate-800 placeholder:text-slate-400 shadow-2xs"
              />
              {search && (
                <button
                  type="button"
                  onClick={() => {
                    setSearch('');
                    setPage(1);
                  }}
                  aria-label="Xóa từ khóa tìm kiếm"
                  className="absolute right-2.5 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 p-0.5 rounded-full hover:bg-slate-100 transition-colors cursor-pointer"
                >
                  <X size={14} />
                </button>
              )}
            </div>

            {/* Compact Sort Select */}
            {tab !== 'my' && (
              <select
                value={`${sort}_${desc ? 'desc' : 'asc'}`}
                onChange={e => {
                  const [newSort, order] = e.target.value.split('_') as [
                    'createdAt' | 'name' | 'studentCount',
                    'asc' | 'desc',
                  ];
                  setSort(newSort);
                  setDesc(order === 'desc');
                  setPage(1);
                }}
                aria-label="Sắp xếp danh sách lớp học"
                className="bg-white border border-slate-200 rounded-xl px-2.5 py-2 text-xs text-slate-700 focus:outline-none focus:border-cyan-600 focus:ring-2 focus:ring-cyan-500/20 transition-all cursor-pointer shadow-2xs shrink-0"
              >
                <option value="createdAt_desc">Mới nhất</option>
                <option value="createdAt_asc">Cũ nhất</option>
                <option value="name_asc">Tên (A → Z)</option>
                <option value="name_desc">Tên (Z → A)</option>
                <option value="studentCount_desc">Đông nhất</option>
              </select>
            )}

            {/* Reset / Refresh Button */}
            {isSearchActive ? (
              <button
                type="button"
                onClick={handleResetFilters}
                title="Xóa tìm kiếm"
                className="p-2 rounded-xl text-slate-500 hover:text-cyan-700 hover:bg-slate-100 border border-slate-200 transition-colors cursor-pointer shrink-0"
              >
                <RotateCcw size={15} />
              </button>
            ) : (
              <button
                type="button"
                onClick={() => {
                  void joinedQuery.refetch();
                  void publicQuery.refetch();
                }}
                disabled={joinedQuery.isFetching || publicQuery.isFetching}
                title="Làm mới danh sách"
                className="p-2 rounded-xl text-slate-400 hover:text-slate-700 hover:bg-slate-100 border border-slate-200 transition-colors cursor-pointer disabled:opacity-50 shrink-0"
              >
                <RotateCcw
                  size={15}
                  className={joinedQuery.isFetching || publicQuery.isFetching ? 'animate-spin' : ''}
                />
              </button>
            )}
          </div>
        </div>

        {/* TAB 1: LỚP HỌC CỦA TÔI */}
        {tab === 'my' && (
          <section aria-labelledby="my-classes-heading">
            <h2 id="my-classes-heading" className="sr-only">
              Danh sách lớp học đã đăng ký
            </h2>

            {/* Loading state */}
            {joinedQuery.isLoading ? (
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-5">
                {[1, 2, 3].map(i => (
                  <div key={i} className="animate-pulse rounded-2xl bg-white border border-slate-200 h-80 p-5 flex flex-col justify-between">
                    <div className="h-36 bg-slate-100 rounded-xl mb-4" />
                    <div className="h-5 bg-slate-100 rounded w-3/4 mb-2" />
                    <div className="h-4 bg-slate-100 rounded w-1/2" />
                    <div className="h-10 bg-slate-100 rounded-xl mt-6" />
                  </div>
                ))}
              </div>
            ) : totalJoinedCount === 0 ? (
              /* Empty state: No classes enrolled yet */
              <div className="text-center py-14 px-4 bg-white rounded-3xl border border-dashed border-slate-200 max-w-lg mx-auto shadow-2xs">
                <div className="w-16 h-16 rounded-2xl bg-cyan-50 text-cyan-600 flex items-center justify-center mx-auto mb-4">
                  <GraduationCap size={36} />
                </div>
                <h3 className="text-lg font-bold text-slate-800">Bạn chưa tham gia lớp học nào</h3>
                <p className="mt-2 text-sm text-slate-500 leading-relaxed">
                  Hãy khám phá các lớp học đang mở để bắt đầu hành trình lập trình của bạn.
                </p>
                <div className="mt-6 flex justify-center">
                  <button
                    type="button"
                    onClick={() => handleTabChange('explore')}
                    className="inline-flex items-center gap-2 px-5 py-2.5 rounded-xl bg-cyan-600 hover:bg-cyan-700 text-white text-xs sm:text-sm font-semibold transition-colors cursor-pointer"
                  >
                    <Compass size={16} />
                    <span>Khám phá lớp học ngay</span>
                  </button>
                </div>
              </div>
            ) : enrolledClasses.length === 0 ? (
              /* Empty state: Search yielded no enrolled classes */
              <div className="text-center py-12 px-4 bg-white rounded-2xl border border-slate-200 max-w-md mx-auto">
                <Search size={32} className="text-slate-300 mx-auto mb-3" />
                <h3 className="text-base font-bold text-slate-800">Không tìm thấy lớp học</h3>
                <p className="mt-1.5 text-xs text-slate-500">
                  Không có lớp học nào đã đăng ký khớp với từ khóa &ldquo;{search}&rdquo;.
                </p>
                <button
                  type="button"
                  onClick={handleResetFilters}
                  className="mt-4 inline-flex items-center gap-1.5 px-3.5 py-2 rounded-xl text-xs font-semibold text-cyan-700 bg-cyan-50 hover:bg-cyan-100 transition-colors cursor-pointer"
                >
                  <RotateCcw size={14} />
                  <span>Xóa tìm kiếm</span>
                </button>
              </div>
            ) : (
              /* Class Grid */
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
                {enrolledClasses.map((c, index) => {
                  const tone = toneStyles[index % toneStyles.length];
                  return (
                    <article
                      key={c.id}
                      className="group flex flex-col bg-white rounded-2xl border border-slate-200/90 hover:border-slate-300 shadow-xs hover:shadow-md transition-all duration-200 overflow-hidden"
                    >
                      {/* Card Cover */}
                      <div className="relative h-44 overflow-hidden bg-slate-100">
                        {c.thumbnail ? (
                          <img
                            src={c.thumbnail}
                            alt={c.name}
                            loading="lazy"
                            onError={e => {
                              e.currentTarget.style.display = 'none';
                            }}
                            className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300"
                          />
                        ) : null}

                        {/* Fallback pattern */}
                        <div
                          className={`absolute inset-0 bg-gradient-to-br ${tone.bg} flex items-center justify-center pointer-events-none -z-10`}
                        >
                          <BookOpen size={54} className={`${tone.icon} opacity-60 transform -rotate-6`} />
                        </div>

                        {/* Top Badges */}
                        <div className="absolute top-3 left-3 z-10">
                          <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold bg-emerald-600 text-white shadow-xs backdrop-blur-xs">
                            <Check size={12} strokeWidth={2.5} />
                            <span>Đã đăng ký</span>
                          </span>
                        </div>
                      </div>

                      {/* Card Body */}
                      <div className="p-5 flex-1 flex flex-col justify-between">
                        <div>
                          <h3
                            className="text-base font-bold text-slate-800 line-clamp-1 group-hover:text-cyan-700 transition-colors"
                            title={c.name}
                          >
                            {c.name}
                          </h3>
                          <p className="mt-2 text-xs text-slate-500 line-clamp-2 leading-relaxed min-h-[34px]">
                            {c.description}
                          </p>
                        </div>

                        {/* Metadata row */}
                        <div className="mt-4 pt-3 border-t border-slate-100 flex items-center justify-between text-xs text-slate-500">
                          <div className="flex items-center gap-1.5">
                            <Users size={14} className="text-slate-400" />
                            <span>
                              {c.studentCount}
                              {c.maxStudents ? ` / ${c.maxStudents}` : ''} học viên
                            </span>
                          </div>

                          {c.startDate && (
                            <div className="flex items-center gap-1 text-[11px] text-slate-400">
                              <Calendar size={13} />
                              <span>{formatDate(c.startDate)}</span>
                            </div>
                          )}
                        </div>

                        {/* Actions */}
                        <div className="mt-4 pt-3 border-t border-slate-100 flex items-center gap-2">
                          <Link
                            to={`/classes/${c.id}`}
                            className="flex-1 inline-flex items-center justify-center px-3.5 py-2.5 rounded-xl bg-cyan-600 hover:bg-cyan-700 text-white text-xs font-semibold shadow-xs transition-colors"
                          >
                            <span>Vào học</span>
                          </Link>
                        </div>
                      </div>
                    </article>
                  );
                })}
              </div>
            )}
          </section>
        )}

        {/* TAB 2 & 3: KHÁM PHÁ LỚP MỚI & TẤT CẢ LỚP HỌC */}
        {(tab === 'explore' || tab === 'all') && (
          <section aria-labelledby="explore-classes-heading">
            <h2 id="explore-classes-heading" className="sr-only">
              {tab === 'explore' ? 'Khám phá các lớp học đang mở' : 'Tất cả các lớp học'}
            </h2>

            {/* Query Loading */}
            {publicQuery.isLoading ? (
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-5">
                {[1, 2, 3, 4, 5, 6].map(i => (
                  <div key={i} className="animate-pulse rounded-2xl bg-white border border-slate-200 h-80 p-5 flex flex-col justify-between">
                    <div className="h-36 bg-slate-100 rounded-xl mb-4" />
                    <div className="h-5 bg-slate-100 rounded w-3/4 mb-2" />
                    <div className="h-4 bg-slate-100 rounded w-1/2" />
                    <div className="h-10 bg-slate-100 rounded-xl mt-6" />
                  </div>
                ))}
              </div>
            ) : publicQuery.isError ? (
              /* Query Error */
              <div className="text-center py-12 px-4 bg-white rounded-2xl border border-rose-200 max-w-md mx-auto">
                <AlertTriangle size={36} className="text-rose-500 mx-auto mb-3" />
                <h3 className="text-base font-bold text-slate-800">Không thể tải danh sách lớp học</h3>
                <p className="mt-1 text-xs text-slate-500">Đã xảy ra lỗi khi kết nối tới máy chủ. Vui lòng thử lại.</p>
                <button
                  type="button"
                  onClick={() => void publicQuery.refetch()}
                  className="mt-4 px-4 py-2 rounded-xl text-xs font-semibold bg-rose-600 hover:bg-rose-700 text-white transition-colors cursor-pointer"
                >
                  Thử lại ngay
                </button>
              </div>
            ) : !publicQuery.data?.items.length ? (
              /* Empty state */
              <div className="text-center py-12 px-4 bg-white rounded-2xl border border-slate-200 max-w-md mx-auto">
                <BookOpen size={36} className="text-slate-300 mx-auto mb-3" />
                <h3 className="text-base font-bold text-slate-800">Không tìm thấy lớp học nào</h3>
                <p className="mt-1 text-xs text-slate-500">
                  {isSearchActive
                    ? `Không có lớp học nào khớp với từ khóa "${search}".`
                    : 'Hiện chưa có lớp học công khai nào đang mở tuyển sinh.'}
                </p>
                {isSearchActive && (
                  <button
                    type="button"
                    onClick={handleResetFilters}
                    className="mt-4 inline-flex items-center gap-1.5 px-3.5 py-2 rounded-xl text-xs font-semibold text-cyan-700 bg-cyan-50 hover:bg-cyan-100 transition-colors cursor-pointer"
                  >
                    <RotateCcw size={14} />
                    <span>Xem tất cả lớp học</span>
                  </button>
                )}
              </div>
            ) : (
              /* Classes Grid */
              <>
                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
                  {publicQuery.data.items
                    .filter(c => (tab === 'explore' ? !c.isEnrolled : true))
                    .map((c, index) => {
                      const tone = toneStyles[index % toneStyles.length];
                      const isFull = c.maxStudents !== null && c.studentCount >= c.maxStudents;
                      const isMutatingThis =
                        mutation.isPending &&
                        (mutation.variables?.path === `/classes/${c.id}/join` ||
                          mutation.variables?.path === `/classes/${c.id}/leave`);

                      return (
                        <article
                          key={c.id}
                          className="group flex flex-col bg-white rounded-2xl border border-slate-200/90 hover:border-slate-300 shadow-xs hover:shadow-md transition-all duration-200 overflow-hidden"
                        >
                          {/* Card Cover */}
                          <div className="relative h-44 overflow-hidden bg-slate-100">
                            {c.thumbnail ? (
                              <img
                                src={c.thumbnail}
                                alt={c.name}
                                loading="lazy"
                                onError={e => {
                                  e.currentTarget.style.display = 'none';
                                }}
                                className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300"
                              />
                            ) : null}

                            <div
                              className={`absolute inset-0 bg-gradient-to-br ${tone.bg} flex items-center justify-center pointer-events-none -z-10`}
                            >
                              <BookOpen size={54} className={`${tone.icon} opacity-60 transform -rotate-6`} />
                            </div>

                            {/* Badge */}
                            <div className="absolute top-3 left-3 z-10">
                              {c.isEnrolled ? (
                                <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold bg-emerald-600 text-white shadow-xs">
                                  <Check size={12} strokeWidth={2.5} />
                                  <span>Đã tham gia</span>
                                </span>
                              ) : isFull ? (
                                <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold bg-amber-600 text-white shadow-xs">
                                  <span>Đã đủ học viên</span>
                                </span>
                              ) : (
                                <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold bg-white/95 text-cyan-800 shadow-xs backdrop-blur-xs">
                                  <span>Công khai · Miễn phí</span>
                                </span>
                              )}
                            </div>
                          </div>

                          {/* Body */}
                          <div className="p-5 flex-1 flex flex-col justify-between">
                            <div>
                              <h3
                                className="text-base font-bold text-slate-800 line-clamp-1 group-hover:text-cyan-700 transition-colors"
                                title={c.name}
                              >
                                {c.name}
                              </h3>
                              <p className="mt-2 text-xs text-slate-500 line-clamp-2 leading-relaxed min-h-[34px]">
                                {c.description || 'Cùng khám phá kiến thức và rèn luyện kỹ năng lập trình mỗi ngày.'}
                              </p>
                            </div>

                            {/* Meta */}
                            <div className="mt-4 pt-3 border-t border-slate-100 flex items-center justify-between text-xs text-slate-500">
                              <div className="flex items-center gap-1.5">
                                <Users size={14} className="text-slate-400" />
                                <span>
                                  {c.studentCount}
                                  {c.maxStudents ? ` / ${c.maxStudents}` : ''} học viên
                                </span>
                              </div>

                              {c.startDate && (
                                <div className="flex items-center gap-1 text-[11px] text-slate-400">
                                  <Calendar size={13} />
                                  <span>{formatDate(c.startDate)}</span>
                                </div>
                              )}
                            </div>

                            {/* Actions */}
                            <div className="mt-4 pt-3 border-t border-slate-100">
                              {c.isEnrolled ? (
                                <div className="flex items-center gap-2">
                                  <Link
                                    to={`/classes/${c.id}`}
                                    className="flex-1 inline-flex items-center justify-center gap-1.5 px-3.5 py-2.5 rounded-xl bg-cyan-600 hover:bg-cyan-700 text-white text-xs font-semibold shadow-xs transition-colors"
                                  >
                                    <span>Vào lớp học</span>
                                    <ArrowRight size={14} />
                                  </Link>
                                  <Link
                                    to={`/practice?scope=class:${c.id}`}
                                    title="Luyện bài tập"
                                    className="p-2.5 rounded-xl bg-slate-100 hover:bg-slate-200 text-slate-700 transition-colors"
                                  >
                                    <Code2 size={16} />
                                  </Link>
                                  <button
                                    type="button"
                                    onClick={() => setConfirmLeaveClass(c)}
                                    title="Rời khỏi lớp"
                                    className="p-2.5 rounded-xl text-slate-400 hover:text-rose-600 hover:bg-rose-50 transition-colors cursor-pointer"
                                  >
                                    <LogOut size={16} />
                                  </button>
                                </div>
                              ) : (
                                <button
                                  type="button"
                                  disabled={isFull || isMutatingThis}
                                  onClick={() => handleJoinClass(c)}
                                  className="w-full inline-flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl bg-cyan-600 hover:bg-cyan-700 disabled:bg-slate-100 disabled:text-slate-400 text-white text-xs font-semibold shadow-xs transition-all cursor-pointer disabled:cursor-not-allowed"
                                >
                                  {isMutatingThis ? (
                                    <span>Đang tham gia…</span>
                                  ) : isFull ? (
                                    <span>Lớp đã đủ người</span>
                                  ) : (
                                    <>
                                      <span>Tham gia lớp</span>
                                      <ArrowRight size={14} />
                                    </>
                                  )}
                                </button>
                              )}
                            </div>
                          </div>
                        </article>
                      );
                    })}
                </div>

                {/* Modern Pagination Bar */}
                {publicQuery.data.totalPages > 1 && (
                  <div className="mt-8 pt-5 border-t border-slate-200 flex flex-col sm:flex-row items-center justify-between gap-4 text-xs text-slate-600">
                    <div>
                      Hiển thị trang <strong className="font-semibold text-slate-900">{publicQuery.data.page}</strong> /{' '}
                      <strong className="font-semibold text-slate-900">{publicQuery.data.totalPages}</strong> (Tổng cộng{' '}
                      <strong className="font-semibold text-slate-900">{publicQuery.data.totalCount}</strong> lớp học)
                    </div>

                    <div className="flex items-center gap-1.5">
                      <button
                        type="button"
                        disabled={page <= 1}
                        onClick={() => setPage(p => Math.max(1, p - 1))}
                        className="inline-flex items-center gap-1 px-3 py-1.5 rounded-lg border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors cursor-pointer"
                      >
                        <ChevronLeft size={14} />
                        <span>Trước</span>
                      </button>

                      {Array.from({ length: publicQuery.data.totalPages }, (_, i) => i + 1)
                        .filter(p => p === 1 || p === publicQuery.data.totalPages || Math.abs(p - page) <= 1)
                        .map((p, idx, arr) => (
                          <span key={p} className="flex items-center">
                            {idx > 0 && arr[idx - 1] !== p - 1 && (
                              <span className="px-1 text-slate-400 select-none">…</span>
                            )}
                            <button
                              type="button"
                              onClick={() => setPage(p)}
                              className={`w-8 h-8 rounded-lg text-xs font-semibold transition-colors cursor-pointer ${
                                p === page
                                  ? 'bg-cyan-600 text-white shadow-2xs'
                                  : 'border border-slate-200 bg-white hover:bg-slate-50 text-slate-700'
                              }`}
                            >
                              {p}
                            </button>
                          </span>
                        ))}

                      <button
                        type="button"
                        disabled={page >= publicQuery.data.totalPages}
                        onClick={() => setPage(p => Math.min(publicQuery.data.totalPages, p + 1))}
                        className="inline-flex items-center gap-1 px-3 py-1.5 rounded-lg border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors cursor-pointer"
                      >
                        <span>Sau</span>
                        <ChevronRight size={14} />
                      </button>
                    </div>
                  </div>
                )}
              </>
            )}
          </section>
        )}
      </main>

      {/* MODAL: NHẬP MÃ ĐỂ THAM GIA LỚP HỌC KHI ĐƯỢC YÊU CẦU */}
      {targetClassForJoin && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="join-code-title"
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/40 backdrop-blur-xs animate-in fade-in duration-150"
          onClick={e => {
            if (e.target === e.currentTarget && !mutation.isPending) {
              setTargetClassForJoin(null);
            }
          }}
        >
          <div className="relative w-full max-w-md bg-white rounded-3xl p-6 sm:p-7 shadow-2xl border border-slate-100">
            {/* Close Button */}
            <button
              type="button"
              onClick={() => setTargetClassForJoin(null)}
              disabled={mutation.isPending}
              aria-label="Đóng cửa sổ"
              className="absolute top-5 right-5 p-1.5 rounded-full text-slate-400 hover:text-slate-600 hover:bg-slate-100 transition-colors cursor-pointer"
            >
              <X size={18} />
            </button>

            {/* Modal Header */}
            <div className="flex items-center gap-3.5 mb-4">
              <div className="w-11 h-11 rounded-2xl bg-cyan-100 text-cyan-700 flex items-center justify-center shrink-0">
                <KeyRound size={22} />
              </div>
              <div>
                <h3 id="join-code-title" className="text-lg font-bold text-slate-800">
                  Nhập mã tham gia lớp
                </h3>
                <p className="text-xs text-slate-500 line-clamp-1">{targetClassForJoin.name}</p>
              </div>
            </div>

            <p className="text-xs text-slate-600 leading-relaxed mb-4">
              Lớp học <strong className="text-slate-800">&ldquo;{targetClassForJoin.name}&rdquo;</strong> yêu cầu mã tham gia do giảng viên cung cấp. Vui lòng nhập mã để hoàn tất đăng ký.
            </p>

            <form onSubmit={handleJoinByCode} className="space-y-4">
              <div>
                <label htmlFor="class-join-code-input" className="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-2">
                  Mã lớp học (8 ký tự)
                </label>
                <input
                  id="class-join-code-input"
                  type="text"
                  autoFocus
                  required
                  minLength={8}
                  maxLength={8}
                  pattern="[A-HJ-NP-Za-hj-np-z2-9]{8}"
                  placeholder="VD: AB12CD34"
                  value={joinCode}
                  onChange={e => setJoinCode(e.target.value.toUpperCase().replace(/[^A-HJ-NP-Za-hj-np-z2-9]/g, ''))}
                  className="w-full text-center tracking-[0.25em] font-mono text-xl font-bold uppercase py-3.5 px-4 rounded-xl border-2 border-slate-200 focus:border-cyan-600 focus:ring-4 focus:ring-cyan-500/10 outline-none transition-all placeholder:tracking-normal placeholder:font-sans placeholder:text-sm placeholder:font-normal text-slate-800"
                />
                <span className="block mt-1.5 text-[11px] text-slate-400 text-center">
                  Mã lớp gồm 8 ký tự phân biệt chữ hoa, chữ cái và số.
                </span>
              </div>

              <div className="flex items-center justify-end gap-2.5 pt-2">
                <button
                  type="button"
                  disabled={mutation.isPending}
                  onClick={() => setTargetClassForJoin(null)}
                  className="px-4 py-2.5 rounded-xl border border-slate-200 text-xs font-semibold text-slate-600 hover:bg-slate-50 transition-colors cursor-pointer"
                >
                  Hủy bỏ
                </button>
                <button
                  type="submit"
                  disabled={joinCode.trim().length !== 8 || mutation.isPending}
                  className="inline-flex items-center gap-2 px-5 py-2.5 rounded-xl bg-cyan-600 hover:bg-cyan-700 disabled:opacity-50 text-white text-xs font-semibold shadow-xs transition-all cursor-pointer disabled:cursor-not-allowed"
                >
                  {mutation.isPending ? <span>Đang kiểm tra…</span> : <span>Xác nhận tham gia</span>}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL: CONFIRM LEAVE CLASS */}
      {confirmLeaveClass && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="confirm-leave-title"
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/40 backdrop-blur-xs animate-in fade-in duration-150"
          onClick={e => {
            if (e.target === e.currentTarget && !mutation.isPending) {
              setConfirmLeaveClass(null);
            }
          }}
        >
          <div className="relative w-full max-w-md bg-white rounded-3xl p-6 sm:p-7 shadow-2xl border border-slate-100">
            <div className="flex items-center gap-3.5 mb-4">
              <div className="w-11 h-11 rounded-2xl bg-rose-100 text-rose-700 flex items-center justify-center shrink-0">
                <AlertTriangle size={22} />
              </div>
              <div>
                <h3 id="confirm-leave-title" className="text-lg font-bold text-slate-800">
                  Rời khỏi lớp học?
                </h3>
                <p className="text-xs text-slate-500">Xác nhận thao tác tài khoản</p>
              </div>
            </div>

            <p className="text-sm text-slate-600 leading-relaxed mb-6">
              Bạn có chắc chắn muốn rời khỏi lớp <strong className="text-slate-900">&ldquo;{confirmLeaveClass.name}&rdquo;</strong>?
              Bạn có thể mất quyền truy cập bài giảng và bài tập riêng của lớp này.
            </p>

            <div className="flex items-center justify-end gap-2.5">
              <button
                type="button"
                disabled={mutation.isPending}
                onClick={() => setConfirmLeaveClass(null)}
                className="px-4 py-2.5 rounded-xl border border-slate-200 text-xs font-semibold text-slate-600 hover:bg-slate-50 transition-colors cursor-pointer"
              >
                Giữ lại
              </button>
              <button
                type="button"
                disabled={mutation.isPending}
                onClick={handleConfirmLeave}
                className="inline-flex items-center gap-2 px-5 py-2.5 rounded-xl bg-rose-600 hover:bg-rose-700 disabled:opacity-50 text-white text-xs font-semibold shadow-xs transition-all cursor-pointer"
              >
                {mutation.isPending ? <span>Đang xử lý…</span> : <span>Xác nhận rời lớp</span>}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
