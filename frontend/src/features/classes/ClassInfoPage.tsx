import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import {
  AlertTriangle,
  ArrowRight,
  BookOpen,
  Calendar,
  ChevronLeft,
  GraduationCap,
  KeyRound,
  LogOut,
  Users,
  X,
} from 'lucide-react';
import { toast } from 'sonner';
import { MemberHeader } from '../home/MemberHeader';
import { useClassMutation, useClassesQuery, type StudentClassInfo } from './api';
import '@fontsource/be-vietnam-pro/400.css';
import '@fontsource/be-vietnam-pro/500.css';
import '@fontsource/be-vietnam-pro/600.css';
import '@fontsource/be-vietnam-pro/700.css';

const formatDate = (dateStr?: string | null) => {
  if (!dateStr) return null;
  try {
    const d = new Date(dateStr);
    return d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' });
  } catch {
    return null;
  }
};

const extractError = (error: unknown) => {
  const axiosErr = error as { response?: { data?: { error?: { code?: string; message?: string }; code?: string; message?: string } } };
  return {
    code: axiosErr?.response?.data?.error?.code || axiosErr?.response?.data?.code || '',
    message: axiosErr?.response?.data?.error?.message || axiosErr?.response?.data?.message || '',
  };
};

export function ClassInfoPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [joinCode, setJoinCode] = useState('');
  const [showCodeForm, setShowCodeForm] = useState(false);
  const [confirmLeave, setConfirmLeave] = useState(false);
  const mutation = useClassMutation();
  const query = useClassesQuery<StudentClassInfo>(`/classes/${id}/info`, undefined, !!id);

  const info = query.data;
  const isFull = info ? info.maxStudents !== null && info.studentCount >= info.maxStudents : false;

  const handleLeave = () => {
    if (!info) return;
    mutation.mutate(
      { path: `/classes/${info.id}/leave` },
      {
        onSuccess: () => {
          setConfirmLeave(false);
          toast.success('Đã rời khỏi lớp học thành công.');
          navigate('/classes');
        },
      }
    );
  };

  const enterClass = () => {
    if (!info) return;
    mutation.mutate(
      { path: `/classes/${info.id}/join` },
      {
        onSuccess: () => {
          toast.success(`Tham gia lớp "${info.name}" thành công!`);
          void query.refetch();
        },
        onError: (error: unknown) => {
          const { code, message } = extractError(error);
          // Class requires a join code: fall back to the code form.
          if (code === 'INVALID_JOIN_CODE' || message.includes('mã') || message.includes('Code') || message.includes('Phương thức')) {
            setShowCodeForm(true);
          } else {
            toast.error(message || 'Không thể tham gia lớp học.');
          }
        },
      }
    );
  };

  const joinByCode = (e: React.FormEvent) => {
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
          setShowCodeForm(false);
          void query.refetch();
          toast.success('Tham gia lớp học thành công!');
        },
      }
    );
  };

  return (
    <div className="min-h-screen bg-[#fbfcfe] text-[#344c59] font-['Be_Vietnam_Pro',sans-serif] flex flex-col">
      <MemberHeader />

      <main id="member-class-info-main" className="flex-1 w-full max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 py-6 sm:py-8">
        {/* Back link */}
        <Link
          to="/classes"
          className="inline-flex items-center gap-1.5 text-xs sm:text-sm font-semibold text-slate-500 hover:text-cyan-700 transition-colors mb-5"
        >
          <ChevronLeft size={16} />
          <span>Quay lại danh sách lớp học</span>
        </Link>

        {/* Loading state */}
        {query.isLoading && (
          <div className="animate-pulse bg-white rounded-3xl border border-slate-200 overflow-hidden">
            <div className="h-56 sm:h-72 bg-slate-100" />
            <div className="p-6 sm:p-8 space-y-4">
              <div className="h-7 bg-slate-100 rounded w-2/3" />
              <div className="h-4 bg-slate-100 rounded w-full" />
              <div className="h-4 bg-slate-100 rounded w-4/5" />
              <div className="h-12 bg-slate-100 rounded-2xl w-1/2 mt-6" />
            </div>
          </div>
        )}

        {/* Error state */}
        {query.isError && (
          <div className="text-center py-14 px-4 bg-white rounded-3xl border border-slate-200 max-w-lg mx-auto shadow-2xs">
            <div className="w-16 h-16 rounded-2xl bg-amber-50 text-amber-600 flex items-center justify-center mx-auto mb-4">
              <AlertTriangle size={34} />
            </div>
            <h3 className="text-lg font-bold text-slate-800">Không hiển thị được lớp học</h3>
            <p className="mt-2 text-sm text-slate-500 leading-relaxed">
              {extractError(query.error).message ||
                'Lớp học không tồn tại, đã ngừng hoạt động hoặc yêu cầu mã tham gia để xem thông tin.'}
            </p>
            <div className="mt-6 flex items-center justify-center gap-2.5 flex-wrap">
              <Link
                to="/classes"
                className="inline-flex items-center gap-1.5 px-4 py-2.5 rounded-xl border border-slate-200 text-xs font-semibold text-slate-600 hover:bg-slate-50 transition-colors"
              >
                <span>Về danh sách lớp</span>
              </Link>
              <button
                type="button"
                onClick={() => void query.refetch()}
                className="inline-flex items-center gap-1.5 px-4 py-2.5 rounded-xl bg-cyan-600 hover:bg-cyan-700 text-white text-xs font-semibold transition-colors cursor-pointer"
              >
                <span>Thử lại</span>
              </button>
            </div>
          </div>
        )}

        {/* Class information */}
        {query.isSuccess && info && (
          <article className="bg-white rounded-3xl border border-slate-200/90 shadow-xs overflow-hidden">
            {/* Cover */}
            <div className="relative h-56 sm:h-72 overflow-hidden bg-slate-100">
              {info.thumbnail ? (
                <img
                  src={info.thumbnail}
                  alt={info.name}
                  onError={e => {
                    e.currentTarget.style.display = 'none';
                  }}
                  className="w-full h-full object-cover"
                />
              ) : null}
              <div className="absolute inset-0 bg-gradient-to-br from-sky-50 via-cyan-50 to-blue-100 flex items-center justify-center pointer-events-none -z-10">
                <BookOpen size={72} className="text-sky-500 opacity-60 transform -rotate-6" />
              </div>
              <div className="absolute top-4 left-4 z-10">
                {info.isEnrolled ? (
                  <span className="inline-flex items-center gap-1.5 px-3.5 py-1.5 rounded-full text-xs font-semibold bg-emerald-600 text-white shadow-xs">
                    <GraduationCap size={14} />
                    <span>Bạn đã tham gia lớp này</span>
                  </span>
                ) : isFull ? (
                  <span className="inline-flex items-center gap-1.5 px-3.5 py-1.5 rounded-full text-xs font-semibold bg-amber-600 text-white shadow-xs">
                    <span>Lớp đã đủ học viên</span>
                  </span>
                ) : (
                  <span className="inline-flex items-center gap-1.5 px-3.5 py-1.5 rounded-full text-xs font-semibold bg-white/95 text-cyan-800 shadow-xs backdrop-blur-xs">
                    <span>Công khai · Miễn phí</span>
                  </span>
                )}
              </div>
            </div>

            {/* Body */}
            <div className="p-6 sm:p-8">
              <h1 className="text-xl sm:text-2xl font-bold text-slate-800 tracking-tight">{info.name}</h1>
              <p className="mt-2.5 text-sm text-slate-600 leading-relaxed">
                {info.description || 'Cùng khám phá kiến thức và rèn luyện kỹ năng lập trình mỗi ngày.'}
              </p>

              {/* Teacher */}
              {info.teachers.length > 0 && (
                <div className="mt-5 flex items-center gap-3">
                  <div className="flex -space-x-2">
                    {info.teachers.slice(0, 3).map((t, i) =>
                      t.avatarUrl ? (
                        <img key={i} src={t.avatarUrl} alt={t.fullName} className="w-9 h-9 rounded-full object-cover border-2 border-white" />
                      ) : (
                        <span
                          key={i}
                          className="w-9 h-9 rounded-full bg-cyan-100 text-cyan-700 border-2 border-white flex items-center justify-center text-xs font-bold"
                        >
                          {t.fullName.trim().charAt(0).toUpperCase() || 'G'}
                        </span>
                      )
                    )}
                  </div>
                  <div className="text-xs text-slate-500">
                    <span className="font-semibold text-slate-700">
                      {info.teachers.length === 1 ? 'Giảng viên' : `${info.teachers.length} giảng viên`}
                    </span>
                    <span className="ml-1.5">{info.teachers.map(t => t.fullName).join(', ')}</span>
                  </div>
                </div>
              )}

              {/* Metadata */}
              <div className="mt-6 grid grid-cols-1 sm:grid-cols-3 gap-3 text-sm">
                <div className="flex items-center gap-3 px-4 py-3.5 rounded-2xl bg-slate-50 border border-slate-100">
                  <span className="w-9 h-9 rounded-xl bg-cyan-100 text-cyan-700 flex items-center justify-center shrink-0">
                    <Users size={17} />
                  </span>
                  <div>
                    <p className="text-[11px] font-semibold text-slate-400 uppercase tracking-wide">Học viên</p>
                    <p className="font-bold text-slate-800">
                      {info.studentCount}
                      {info.maxStudents !== null ? ` / ${info.maxStudents}` : ''}
                    </p>
                  </div>
                </div>
                <div className="flex items-center gap-3 px-4 py-3.5 rounded-2xl bg-slate-50 border border-slate-100">
                  <span className="w-9 h-9 rounded-xl bg-emerald-100 text-emerald-700 flex items-center justify-center shrink-0">
                    <Calendar size={17} />
                  </span>
                  <div>
                    <p className="text-[11px] font-semibold text-slate-400 uppercase tracking-wide">Khai giảng</p>
                    <p className="font-bold text-slate-800">{formatDate(info.startDate) ?? 'Chưa xác định'}</p>
                  </div>
                </div>
                <div className="flex items-center gap-3 px-4 py-3.5 rounded-2xl bg-slate-50 border border-slate-100">
                  <span className="w-9 h-9 rounded-xl bg-amber-100 text-amber-700 flex items-center justify-center shrink-0">
                    <Calendar size={17} />
                  </span>
                  <div>
                    <p className="text-[11px] font-semibold text-slate-400 uppercase tracking-wide">Kết thúc</p>
                    <p className="font-bold text-slate-800">{formatDate(info.endDate) ?? 'Chưa xác định'}</p>
                  </div>
                </div>
              </div>

              {/* Action: only after reading the info may the user enter the class */}
              <div className="mt-7 pt-5 border-t border-slate-100">
                {info.isEnrolled ? (
                  <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                    <p className="text-xs text-slate-500 leading-relaxed">
                      Bạn đã là học viên của lớp này. Cảm thấy sẵn sàng thì vào học thôi!
                    </p>
                    <div className="flex items-center gap-2.5 shrink-0">
                      <button
                        type="button"
                        onClick={() => setConfirmLeave(true)}
                        className="inline-flex items-center justify-center gap-1.5 px-4 py-3 rounded-xl border border-slate-200 text-slate-500 hover:text-rose-600 hover:border-rose-200 hover:bg-rose-50 text-sm font-semibold transition-colors cursor-pointer"
                      >
                        <LogOut size={15} />
                        <span>Rời khỏi lớp</span>
                      </button>
                      <Link
                        to={`/classes/${info.id}/learning`}
                        className="inline-flex items-center justify-center gap-2 px-6 py-3 rounded-xl bg-cyan-600 hover:bg-cyan-700 text-white text-sm font-semibold shadow-xs transition-colors shrink-0"
                      >
                        <span>Vào lớp học</span>
                        <ArrowRight size={16} />
                      </Link>
                    </div>
                  </div>
                ) : showCodeForm ? (
                  <form onSubmit={joinByCode} className="max-w-md space-y-3">
                    <label htmlFor="class-info-join-code" className="flex items-center gap-2 text-xs font-bold text-slate-700 uppercase tracking-wider">
                      <KeyRound size={14} className="text-cyan-600" />
                      <span>Nhập mã lớp học (8 ký tự)</span>
                    </label>
                    <div className="flex items-center gap-2.5">
                      <input
                        id="class-info-join-code"
                        type="text"
                        autoFocus
                        required
                        minLength={8}
                        maxLength={8}
                        pattern="[A-HJ-NP-Za-hj-np-z2-9]{8}"
                        placeholder="VD: AB12CD34"
                        value={joinCode}
                        onChange={e => setJoinCode(e.target.value.toUpperCase().replace(/[^A-HJ-NP-Za-hj-np-z2-9]/g, ''))}
                        className="flex-1 text-center tracking-[0.25em] font-mono text-lg font-bold uppercase py-3 px-4 rounded-xl border-2 border-slate-200 focus:border-cyan-600 focus:ring-4 focus:ring-cyan-500/10 outline-none transition-all placeholder:tracking-normal placeholder:font-sans placeholder:text-sm placeholder:font-normal text-slate-800"
                      />
                      <button
                        type="submit"
                        disabled={joinCode.trim().length !== 8 || mutation.isPending}
                        className="inline-flex items-center gap-2 px-5 py-3 rounded-xl bg-cyan-600 hover:bg-cyan-700 disabled:opacity-50 text-white text-sm font-semibold shadow-xs transition-all cursor-pointer disabled:cursor-not-allowed"
                      >
                        <span>{mutation.isPending ? 'Đang kiểm tra…' : 'Tham gia'}</span>
                      </button>
                    </div>
                    <button
                      type="button"
                      onClick={() => setShowCodeForm(false)}
                      className="inline-flex items-center gap-1 text-xs font-semibold text-slate-500 hover:text-slate-700 transition-colors cursor-pointer"
                    >
                      <X size={13} />
                      <span>Quay lại</span>
                    </button>
                  </form>
                ) : (
                  <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                    <p className="text-xs text-slate-500 leading-relaxed">
                      {isFull
                        ? 'Lớp đã đạt số lượng học viên tối đa nên tạm dừng nhận thêm thành viên.'
                        : 'Đọc qua thông tin lớp học, nếu phù hợp thì bạn hãy tham gia để bắt đầu hành trình nhé!'}
                    </p>
                    <button
                      type="button"
                      disabled={isFull || mutation.isPending}
                      onClick={enterClass}
                      className="inline-flex items-center justify-center gap-2 px-6 py-3 rounded-xl bg-cyan-600 hover:bg-cyan-700 disabled:bg-slate-100 disabled:text-slate-400 text-white text-sm font-semibold shadow-xs transition-all cursor-pointer disabled:cursor-not-allowed shrink-0"
                    >
                      {mutation.isPending ? (
                        <span>Đang tham gia…</span>
                      ) : isFull ? (
                        <span>Lớp đã đủ người</span>
                      ) : (
                        <>
                          <span>Tham gia lớp học</span>
                          <ArrowRight size={16} />
                        </>
                      )}
                    </button>
                  </div>
                )}
              </div>
            </div>
          </article>
        )}
      </main>

      {/* MODAL: CONFIRM LEAVE CLASS */}
      {confirmLeave && info && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="info-confirm-leave-title"
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/40 backdrop-blur-xs animate-in fade-in duration-150"
          onClick={e => {
            if (e.target === e.currentTarget && !mutation.isPending) {
              setConfirmLeave(false);
            }
          }}
        >
          <div className="relative w-full max-w-md bg-white rounded-3xl p-6 sm:p-7 shadow-2xl border border-slate-100">
            <div className="flex items-center gap-3.5 mb-4">
              <div className="w-11 h-11 rounded-2xl bg-rose-100 text-rose-700 flex items-center justify-center shrink-0">
                <AlertTriangle size={22} />
              </div>
              <div>
                <h3 id="info-confirm-leave-title" className="text-lg font-bold text-slate-800">
                  Rời khỏi lớp học?
                </h3>
                <p className="text-xs text-slate-500">Xác nhận thao tác tài khoản</p>
              </div>
            </div>

            <p className="text-sm text-slate-600 leading-relaxed mb-6">
              Bạn có chắc chắn muốn rời khỏi lớp <strong className="text-slate-900">&ldquo;{info.name}&rdquo;</strong>?
              Bạn có thể mất quyền truy cập bài giảng và bài tập riêng của lớp này.
            </p>

            <div className="flex items-center justify-end gap-2.5">
              <button
                type="button"
                disabled={mutation.isPending}
                onClick={() => setConfirmLeave(false)}
                className="px-4 py-2.5 rounded-xl border border-slate-200 text-xs font-semibold text-slate-600 hover:bg-slate-50 transition-colors cursor-pointer"
              >
                Giữ lại
              </button>
              <button
                type="button"
                disabled={mutation.isPending}
                onClick={handleLeave}
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
