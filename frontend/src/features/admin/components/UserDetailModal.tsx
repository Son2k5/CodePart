import React from 'react';
import {
  X,
  Phone,
  Building,
  Calendar,
  Clock,
  Shield,
  CheckCircle,
  XCircle,
  Lock,
  Unlock,
  GraduationCap,
  BookOpen,
  Award,
  Trash2,
  AlertTriangle,
} from 'lucide-react';
import type { AdminUserItem } from '../types/admin.types';
import { UserRoleBadge } from './UserRoleBadge';
import { UserStatusBadge } from './UserStatusBadge';

interface UserDetailModalProps {
  busy?: boolean;
  detailError?: string;
  user: AdminUserItem | null;
  onClose: () => void;
  onApproveTeacher: (user: AdminUserItem) => void;
  onRejectTeacher: (user: AdminUserItem) => void;
  onToggleStatus: (user: AdminUserItem) => void;
  onDeleteUser?: (user: AdminUserItem) => void;
}

export const UserDetailModal: React.FC<UserDetailModalProps> = ({
  user,
  busy = false,
  detailError,
  onClose,
  onApproveTeacher,
  onRejectTeacher,
  onToggleStatus,
  onDeleteUser,
}) => {
  const [confirmDelete, setConfirmDelete] = React.useState(false);

  React.useEffect(() => {
    setConfirmDelete(false);
  }, [user?.id]);

  if (!user) return null;

  const getInitials = (name: string) => {
    const parts = name.trim().split(' ');
    if (parts.length >= 2) {
      return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    }
    return name.slice(0, 2).toUpperCase();
  };

  const formatDate = (dateStr?: string | null) => {
    if (!dateStr) return 'Chưa ghi nhận';
    return new Date(dateStr).toLocaleString('vi-VN', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/40 backdrop-blur-xs animate-in fade-in duration-150">
      <div className="relative w-full max-w-2xl bg-white rounded-3xl shadow-2xl border border-slate-100 overflow-hidden flex flex-col max-h-[90vh]">
        {/* Header / Banner */}
        <div className="relative bg-gradient-to-r from-sky-500 via-sky-600 to-indigo-600 p-6 text-white shrink-0">
          <button
            type="button"
            onClick={onClose}
            className="absolute top-4 right-4 p-2 rounded-full bg-black/10 hover:bg-black/20 text-white transition-colors cursor-pointer"
            title="Đóng"
          >
            <X className="w-5 h-5" />
          </button>

          <div className="flex items-center gap-4">
            <div className="w-16 h-16 rounded-2xl bg-white/20 backdrop-blur-md border border-white/30 text-white font-bold text-xl flex items-center justify-center shadow-lg shrink-0">
              {getInitials(user.fullName)}
            </div>
            <div>
              <div className="flex items-center gap-2 flex-wrap">
                <h3 className="text-xl font-bold">{user.fullName}</h3>
                <UserRoleBadge role={user.role} className="bg-white/90 text-slate-900" />
              </div>
              <p className="text-sky-100 text-xs mt-1">{user.email}</p>
            </div>
          </div>
        </div>

        {/* Content Body */}
        <div className="p-6 overflow-y-auto space-y-6">
          {busy && <p role="status" className="text-xs">Đang xử lý…</p>}
          {detailError && <p role="alert" className="text-xs text-rose-700">{detailError}</p>}
          {/* Status Alert for Pending User */}
          {user.status === 'Pending' && (
            <div className="p-4 rounded-2xl bg-amber-50 border border-amber-200 text-amber-900 flex items-start gap-3">
              <Clock className="w-5 h-5 text-amber-600 shrink-0 mt-0.5" />
              <div className="text-xs space-y-1">
                <div className="font-semibold text-amber-950">
                  Hồ sơ {user.role === 'Teacher' ? 'giáo viên' : 'sinh viên'} đang chờ xét duyệt
                </div>
                <p className="text-amber-800">
                  {user.role === 'Teacher'
                    ? (user.emailVerifiedAt ? 'Email nhà trường đã được xác thực. ' : 'Giáo viên chưa xác thực email; chưa thể phê duyệt. ')
                    : ''}
                  Vui lòng kiểm tra thông tin và nhấn duyệt để kích hoạt tài khoản.
                </p>
                <div className="pt-2 flex items-center gap-2">
                  <button
                    type="button"
                    disabled={busy || (user.role === 'Teacher' && !user.emailVerifiedAt)}
                    onClick={() => {
                      onApproveTeacher(user);
                    }}
                    className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-medium shadow-xs cursor-pointer transition-colors"
                  >
                    <CheckCircle className="w-3.5 h-3.5" />
                    Phê duyệt ngay
                  </button>
                  <button
                    type="button"
                    disabled={busy}
                    onClick={() => {
                      onRejectTeacher(user);
                    }}
                    className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-white hover:bg-rose-50 border border-rose-200 text-rose-700 text-xs font-medium shadow-xs cursor-pointer transition-colors"
                  >
                    <XCircle className="w-3.5 h-3.5" />
                    Từ chối hồ sơ
                  </button>
                </div>
              </div>
            </div>
          )}

          {/* Core Info Grid */}
          <div>
            <h4 className="text-xs font-semibold text-slate-400 uppercase tracking-wider mb-3">
              Thông tin chi tiết
            </h4>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3.5 text-xs">
              <div className="p-3.5 rounded-xl bg-slate-50 border border-slate-100 flex items-start gap-3">
                <Building className="w-4 h-4 text-slate-400 mt-0.5 shrink-0" />
                <div>
                  <div className="text-slate-400 text-[11px]">Đơn vị / Khoa phụ trách</div>
                  <div className="font-medium text-slate-800 mt-0.5">
                    {user.department || 'Chưa phân khoa'}
                  </div>
                </div>
              </div>

              <div className="p-3.5 rounded-xl bg-slate-50 border border-slate-100 flex items-start gap-3">
                <Award className="w-4 h-4 text-slate-400 mt-0.5 shrink-0" />
                <div>
                  <div className="text-slate-400 text-[11px]">Mã định danh</div>
                  <div className="font-mono font-semibold text-slate-800 mt-0.5">
                    {user.studentId || user.teacherId || 'Chưa thiết lập'}
                  </div>
                </div>
              </div>

              <div className="p-3.5 rounded-xl bg-slate-50 border border-slate-100 flex items-start gap-3">
                <Phone className="w-4 h-4 text-slate-400 mt-0.5 shrink-0" />
                <div>
                  <div className="text-slate-400 text-[11px]">Số điện thoại liên hệ</div>
                  <div className="font-medium text-slate-800 mt-0.5">
                    {user.phone || 'Chưa cập nhật'}
                  </div>
                </div>
              </div>

              <div className="p-3.5 rounded-xl bg-slate-50 border border-slate-100 flex items-start gap-3">
                <Shield className="w-4 h-4 text-slate-400 mt-0.5 shrink-0" />
                <div>
                  <div className="text-slate-400 text-[11px]">Trạng thái tài khoản</div>
                  <div className="mt-1">
                    <UserStatusBadge status={user.status} />
                  </div>
                </div>
              </div>

              <div className="p-3.5 rounded-xl bg-slate-50 border border-slate-100 flex items-start gap-3">
                <Calendar className="w-4 h-4 text-slate-400 mt-0.5 shrink-0" />
                <div>
                  <div className="text-slate-400 text-[11px]">Ngày tạo tài khoản</div>
                  <div className="font-medium text-slate-800 mt-0.5">
                    {formatDate(user.createdAt)}
                  </div>
                </div>
              </div>

              <div className="p-3.5 rounded-xl bg-slate-50 border border-slate-100 flex items-start gap-3">
                <Clock className="w-4 h-4 text-slate-400 mt-0.5 shrink-0" />
                <div>
                  <div className="text-slate-400 text-[11px]">Đăng nhập lần cuối</div>
                  <div className="font-medium text-slate-800 mt-0.5">
                    {formatDate(user.lastLoginAt)}
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* Academic & Teaching Summary */}
          <div>
            <h4 className="text-xs font-semibold text-slate-400 uppercase tracking-wider mb-3">
              Thông tin học tập & giảng dạy
            </h4>
            <div className="grid grid-cols-2 gap-3 text-xs">
              <div className="p-4 rounded-xl bg-sky-50/50 border border-sky-100 flex items-center gap-3">
                <BookOpen className="w-5 h-5 text-sky-600" />
                <div>
                  <div className="text-slate-500 text-[11px]">
                    {user.role === 'Teacher' ? 'Khóa học phụ trách' : 'Khóa học đã đăng ký'}
                  </div>
                  <div className="text-lg font-bold text-sky-900">
                    {user.coursesCount || 0} khóa
                  </div>
                </div>
              </div>

              <div className="p-4 rounded-xl bg-indigo-50/50 border border-indigo-100 flex items-center gap-3">
                <GraduationCap className="w-5 h-5 text-indigo-600" />
                <div>
                  <div className="text-slate-500 text-[11px]">
                    {user.role === 'Teacher' ? 'Lớp đang giảng dạy' : 'Lớp tín chỉ tham gia'}
                  </div>
                  <div className="text-lg font-bold text-indigo-900">
                    {user.classesCount || 0} lớp
                  </div>
                </div>
              </div>
            </div>
          </div>

          {user.managedClasses && <section className="text-xs space-y-2">
            <h4 className="font-semibold">Lớp niên khóa phụ trách</h4>
            {user.managedClasses.length ? user.managedClasses.map(c => <p key={c.id}>{c.name} · {c.academicYear} · {c.studentCount} sinh viên</p>) : <p>Chưa được phân công.</p>}
          </section>}
          {user.teachingCourses && <section className="text-xs space-y-2">
            <h4 className="font-semibold">Học phần giảng dạy</h4>
            {user.teachingCourses.length ? user.teachingCourses.map(c => <p key={c.id}>{c.subjectName} ({c.sectionCode}) · {c.semester} · {c.enrolledCount}/{c.capacity} sinh viên</p>) : <p>Chưa được phân công.</p>}
          </section>}
          {user.enrolledClasses && (
            <section className="text-xs space-y-2">
              <h4 className="font-semibold">Lớp niên khóa tham gia</h4>
              {user.enrolledClasses.length ? user.enrolledClasses.map(c => <p key={c.id}>{c.name} · {c.academicYear}</p>) : <p>Chưa tham gia lớp nào.</p>}
            </section>
          )}
          {user.enrolledCourses && (
            <section className="text-xs space-y-2">
              <h4 className="font-semibold">Học phần đã đăng ký</h4>
              {user.enrolledCourses.length ? user.enrolledCourses.map(c => <p key={c.id}>{c.subjectName} ({c.sectionCode}) · {c.semester}{c.teacherName ? ` · GV: ${c.teacherName}` : ''}</p>) : <p>Chưa đăng ký học phần nào.</p>}
            </section>
          )}
          {/* Bio / Notes */}
          {user.bio && (
            <div>
              <h4 className="text-xs font-semibold text-slate-400 uppercase tracking-wider mb-2">
                Giới thiệu & Ghi chú hồ sơ
              </h4>
              <p className="text-xs text-slate-600 leading-relaxed bg-slate-50 p-3.5 rounded-xl border border-slate-100">
                {user.bio}
              </p>
            </div>
          )}
        </div>

        {/* Footer Actions */}
        <div className="p-4 px-6 border-t border-slate-100 bg-slate-50/80 flex items-center justify-between gap-3 shrink-0">
          <div>
            {confirmDelete ? (
              <div className="flex items-center gap-2 flex-wrap bg-rose-50 border border-rose-200 px-3 py-1.5 rounded-xl">
                <span className="text-xs text-rose-800 font-medium flex items-center gap-1.5">
                  <AlertTriangle className="w-3.5 h-3.5 text-rose-600 shrink-0" />
                  Xác nhận xóa tài khoản này?
                </span>
                <button
                  type="button"
                  disabled={busy}
                  onClick={() => {
                    onDeleteUser?.(user);
                    setConfirmDelete(false);
                  }}
                  className="px-2.5 py-1 rounded-lg bg-rose-600 hover:bg-rose-700 text-white text-xs font-medium cursor-pointer shadow-xs transition-colors"
                >
                  {busy ? 'Đang xóa...' : 'Xác nhận xóa'}
                </button>
                <button
                  type="button"
                  disabled={busy}
                  onClick={() => setConfirmDelete(false)}
                  className="px-2.5 py-1 rounded-lg bg-white hover:bg-slate-100 border border-slate-200 text-slate-700 text-xs font-medium cursor-pointer transition-colors"
                >
                  Hủy
                </button>
              </div>
            ) : (
              <div className="flex items-center gap-2">
                {user.role !== 'Admin' && (user.status === 'Active' || user.status === 'Disabled') && (
                  <button
                    type="button"
                    disabled={busy}
                    onClick={() => {
                      onToggleStatus(user);
                    }}
                    className={`inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium transition-colors cursor-pointer ${
                      user.status === 'Disabled'
                        ? 'bg-emerald-50 text-emerald-700 hover:bg-emerald-100 border border-emerald-200 shadow-2xs'
                        : 'bg-amber-50 text-amber-800 hover:bg-amber-100 border border-amber-200 shadow-2xs'
                    }`}
                  >
                    {user.status === 'Disabled' ? (
                      <>
                        <Unlock className="w-3.5 h-3.5" />
                        Mở khóa tài khoản
                      </>
                    ) : (
                      <>
                        <Lock className="w-3.5 h-3.5" />
                        Khóa tài khoản
                      </>
                    )}
                  </button>
                )}

                {user.role !== 'Admin' && onDeleteUser && (
                  <button
                    type="button"
                    disabled={busy}
                    onClick={() => setConfirmDelete(true)}
                    className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium bg-rose-50 text-rose-700 hover:bg-rose-100 border border-rose-200 shadow-2xs transition-colors cursor-pointer"
                  >
                    <Trash2 className="w-3.5 h-3.5 text-rose-600" />
                    Xóa tài khoản
                  </button>
                )}
              </div>
            )}
          </div>

          <button
            type="button"
            onClick={onClose}
            className="px-3.5 py-1.5 rounded-lg bg-slate-200 hover:bg-slate-300 text-slate-800 text-xs font-medium transition-colors cursor-pointer"
          >
            Đóng
          </button>
        </div>
      </div>
    </div>
  );
};
