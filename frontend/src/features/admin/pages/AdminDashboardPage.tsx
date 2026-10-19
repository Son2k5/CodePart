import React from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Users,
  UserCheck,
  GraduationCap,
  Clock,
  ArrowRight,
  Sparkles,
} from 'lucide-react';
import { useTeachers, useTeacherStats } from '../hooks/useTeachers';
import { teacherError } from '../api/adminTeacherApi';
import { UserRoleBadge } from '../components/UserRoleBadge';
import { UserStatusBadge } from '../components/UserStatusBadge';

export const AdminDashboardPage: React.FC = () => {
  const navigate = useNavigate();
  const overview = useTeacherStats();
  const pending = useTeachers({ status: 'Pending', page: 1, pageSize: 5, sortBy: 'created_asc' });
  const recent = useTeachers({ page: 1, pageSize: 5, sortBy: 'created_desc' });
  const stats = { totalTeachers: overview.data?.totalTeachers ?? 0, pendingApprovals: overview.data?.pendingApprovals ?? 0,
    activeTeachers: overview.data?.activeTeachers ?? 0, disabledTeachers: overview.data?.disabledTeachers ?? 0 };
  const pendingTeachers = pending.data?.items ?? [];
  const recentUsers = recent.data?.items ?? [];

  if (overview.isPending || pending.isPending || recent.isPending) return <p role="status">Đang tải tổng quan…</p>;
  if (overview.error || pending.error || recent.error) return <p role="alert">{teacherError(overview.error || pending.error || recent.error)} <button onClick={() => { void overview.refetch(); void pending.refetch(); void recent.refetch(); }}>Thử lại</button></p>;

  return (
    <div className="space-y-6">
      {/* Welcome Hero Card */}
      <div className="relative overflow-hidden rounded-3xl bg-gradient-to-r from-sky-600 via-sky-700 to-indigo-700 p-6 sm:p-8 text-white shadow-md">
        <div className="relative z-10 max-w-2xl space-y-3">
          <div className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-white/15 backdrop-blur-md text-sky-100 text-xs font-semibold">
            <Sparkles className="w-3.5 h-3.5 text-amber-300" />
            <span>Hệ thống Quản Trị Trung Tâm CodePart</span>
          </div>
          <h1 className="text-2xl sm:text-3xl font-extrabold tracking-tight">
            Chào mừng Quản trị viên, chúc một ngày làm việc hiệu quả!
          </h1>
          <p className="text-sky-100 text-xs sm:text-sm leading-relaxed">
            Hệ thống đang quản lý {stats.totalTeachers} giáo viên thuộc trường Đại học Hà Nội (HANU).
            {stats.pendingApprovals > 0 ? (
              <span className="font-semibold text-amber-300">
                {' '}Hiện có {stats.pendingApprovals} hồ sơ giáo viên đang chờ xét duyệt cấp quyền giảng dạy.
              </span>
            ) : null}
          </p>

          <div className="pt-2 flex items-center gap-3">
            <button
              type="button"
              onClick={() => navigate('/admin/teachers')}
              className="px-4 py-2 rounded-xl bg-white text-sky-800 text-xs font-bold hover:bg-sky-50 shadow-sm transition-all flex items-center gap-1.5 cursor-pointer"
            >
              <span>Quản lý giáo viên</span>
              <ArrowRight className="w-4 h-4" />
            </button>
            {stats.pendingApprovals > 0 && (
              <button
                type="button"
                onClick={() => navigate('/admin/teachers?tab=pending')}
                className="px-4 py-2 rounded-xl bg-amber-400 hover:bg-amber-300 text-slate-900 text-xs font-bold shadow-sm transition-all flex items-center gap-1.5 cursor-pointer"
              >
                <Clock className="w-4 h-4" />
                <span>Duyệt giáo viên ({stats.pendingApprovals})</span>
              </button>
            )}
          </div>
        </div>
      </div>

      {/* Quick KPI Overview */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div className="bg-white p-5 rounded-2xl border border-slate-200/90 shadow-xs flex items-center gap-4">
          <div className="w-12 h-12 rounded-xl bg-sky-50 text-sky-600 flex items-center justify-center shrink-0">
            <Users className="w-6 h-6" />
          </div>
          <div>
            <div className="text-xs text-slate-400 font-medium">Tổng giáo viên</div>
            <div className="text-2xl font-bold text-slate-900">{stats.totalTeachers}</div>
            <div className="text-[11px] text-emerald-600 font-medium flex items-center gap-1 mt-0.5">
              Số liệu từ hệ thống
            </div>
          </div>
        </div>

        <div className="bg-white p-5 rounded-2xl border border-slate-200/90 shadow-xs flex items-center gap-4">
          <div className="w-12 h-12 rounded-xl bg-indigo-50 text-indigo-600 flex items-center justify-center shrink-0">
            <UserCheck className="w-6 h-6" />
          </div>
          <div>
            <div className="text-xs text-slate-400 font-medium">Giáo viên đang hoạt động</div>
            <div className="text-2xl font-bold text-slate-900">{stats.activeTeachers}</div>
            <div className="text-[11px] text-slate-500 font-medium mt-0.5">
              {stats.pendingApprovals} giảng viên chờ duyệt
            </div>
          </div>
        </div>

        <div className="bg-white p-5 rounded-2xl border border-slate-200/90 shadow-xs flex items-center gap-4">
          <div className="w-12 h-12 rounded-xl bg-emerald-50 text-emerald-600 flex items-center justify-center shrink-0">
            <GraduationCap className="w-6 h-6" />
          </div>
          <div>
            <div className="text-xs text-slate-400 font-medium">Giáo viên đã khóa</div>
            <div className="text-2xl font-bold text-slate-900">{stats.disabledTeachers}</div>
            <div className="text-[11px] text-slate-500 font-medium mt-0.5">
              Có thể mở lại tài khoản
            </div>
          </div>
        </div>

        <div className="bg-white p-5 rounded-2xl border border-slate-200/90 shadow-xs flex items-center gap-4">
          <div className="w-12 h-12 rounded-xl bg-amber-50 text-amber-600 flex items-center justify-center shrink-0">
            <Clock className="w-6 h-6" />
          </div>
          <div>
            <div className="text-xs text-slate-400 font-medium">Hồ sơ chờ phê duyệt</div>
            <div className="text-2xl font-bold text-slate-900">{stats.pendingApprovals}</div>
            <div className="text-[11px] text-amber-700 font-medium mt-0.5">
              Ưu tiên xử lý duyệt
            </div>
          </div>
        </div>
      </div>

      {/* Two Column Layout: Pending Approvals & Recent Users */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Pending Teacher Approvals */}
        <div className="bg-white rounded-2xl border border-slate-200/90 p-5 shadow-xs space-y-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <div className="w-8 h-8 rounded-lg bg-amber-50 text-amber-600 flex items-center justify-center font-bold">
                <Clock className="w-4 h-4" />
              </div>
              <h2 className="text-sm font-bold text-slate-900">
                Hồ sơ giáo viên chờ duyệt ({stats.pendingApprovals})
              </h2>
            </div>
            <button
              type="button"
              onClick={() => navigate('/admin/teachers?tab=pending')}
              className="text-xs font-semibold text-sky-600 hover:text-sky-700"
            >
              Xem tất cả →
            </button>
          </div>

          <div className="space-y-2">
            {pendingTeachers.length === 0 ? (
              <p className="text-xs text-slate-400 py-6 text-center">
                Không có hồ sơ nào đang chờ duyệt.
              </p>
            ) : (
              pendingTeachers.map((teacher) => (
                <div
                  key={teacher.id}
                  className="p-3.5 rounded-xl border border-slate-100 bg-slate-50/60 hover:bg-slate-50 flex items-center justify-between gap-3 transition-colors"
                >
                  <div className="min-w-0">
                    <div className="font-semibold text-xs text-slate-900 truncate">
                      {teacher.fullName}
                    </div>
                    <div className="text-[11px] text-slate-400 truncate">{teacher.email}</div>
                    <div className="text-[10px] text-slate-500 mt-0.5">
                      {teacher.department} • Mã: {teacher.teacherId}
                    </div>
                  </div>
                  <button
                    type="button"
                    onClick={() => navigate('/admin/teachers?tab=pending')}
                    className="px-3 py-1.5 rounded-lg bg-sky-600 hover:bg-sky-700 text-white text-xs font-semibold shrink-0 cursor-pointer transition-colors"
                  >
                    Xem xét duyệt
                  </button>
                </div>
              ))
            )}
          </div>
        </div>

        {/* Recent Registered Users */}
        <div className="bg-white rounded-2xl border border-slate-200/90 p-5 shadow-xs space-y-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <div className="w-8 h-8 rounded-lg bg-sky-50 text-sky-600 flex items-center justify-center font-bold">
                <Users className="w-4 h-4" />
              </div>
              <h2 className="text-sm font-bold text-slate-900">Giáo viên mới tham gia</h2>
            </div>
            <button
              type="button"
              onClick={() => navigate('/admin/teachers')}
              className="text-xs font-semibold text-sky-600 hover:text-sky-700"
            >
              Toàn bộ danh sách →
            </button>
          </div>

          <div className="divide-y divide-slate-100">
            {recentUsers.map((u) => (
              <div key={u.id} className="py-2.5 flex items-center justify-between gap-3 text-xs">
                <div className="min-w-0">
                  <div className="font-semibold text-slate-900 truncate">{u.fullName}</div>
                  <div className="text-[11px] text-slate-400 truncate">{u.email}</div>
                </div>
                <div className="flex items-center gap-2 shrink-0">
                  <UserRoleBadge role={u.role} />
                  <UserStatusBadge status={u.status} />
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
};
