import React from 'react';
import { Search, X, RotateCcw } from 'lucide-react';
import type { UserFilterState, UserStats } from '../types/admin.types';
import { DEPARTMENTS } from '../data/mockAdminData';

interface UserFiltersBarProps {
  role?: 'Student' | 'Teacher';
  filters: UserFilterState;
  onChange: (updated: Partial<UserFilterState>) => void;
  onReset: () => void;
  stats: UserStats;
}

export const UserFiltersBar: React.FC<UserFiltersBarProps> = ({
  filters,
  onChange,
  onReset,
  stats,
  role,
}) => {
  const tabs = [
    { id: 'all' as const, label: 'Tất cả', count: stats.totalUsers },
    { id: 'teachers' as const, label: 'Giáo viên', count: stats.totalTeachers },
    { id: 'students' as const, label: 'Sinh viên', count: stats.totalStudents },
    { id: 'pending' as const, label: 'Chờ duyệt', count: stats.pendingApprovals, alert: stats.pendingApprovals > 0 },
    { id: 'disabled' as const, label: 'Đã khóa', count: stats.disabledUsers },
  ];

  const hasActiveFilters =
    Boolean(filters.search) ||
    filters.role !== 'ALL' ||
    filters.status !== 'ALL' ||
    Boolean(filters.department);

  return (
    <div className="bg-white rounded-2xl border border-slate-200/90 p-4 space-y-4 shadow-xs">
      {/* Top Segmented Tabs */}
      <div className="flex flex-wrap items-center gap-2 border-b border-slate-100 pb-3">
        {tabs.filter((tab) => !role || !['teachers', 'students'].includes(tab.id)).map((tab) => {
          const isActive = filters.activeTab === tab.id;
          return (
            <button
              key={tab.id}
              type="button"
              onClick={() => onChange({ activeTab: tab.id })}
              className={`flex items-center gap-2 px-3.5 py-1.5 rounded-lg text-xs font-medium transition-all ${
                isActive
                  ? 'bg-sky-600 text-white shadow-xs font-semibold'
                  : 'bg-slate-50 text-slate-600 hover:bg-slate-100'
              }`}
            >
              <span>{tab.label}</span>
              <span
                className={`px-1.5 py-0.5 rounded-full text-[10px] font-bold ${
                  isActive
                    ? 'bg-white/20 text-white'
                    : tab.alert
                    ? 'bg-amber-100 text-amber-700 font-extrabold'
                    : 'bg-slate-200/80 text-slate-600'
                }`}
              >
                {tab.count}
              </span>
            </button>
          );
        })}
      </div>

      {/* Search and Dropdowns */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-12 gap-3 items-center">
        {/* Search Input */}
        <div className="lg:col-span-5 relative">
          <Search className="w-4 h-4 text-slate-400 absolute left-3.5 top-1/2 -translate-y-1/2 pointer-events-none" />
          <input
            type="text"
            value={filters.search}
            onChange={(e) => onChange({ search: e.target.value })}
            placeholder={role === 'Student' ? 'Tìm tên, email hoặc mã sinh viên...' : 'Tìm tên, email hoặc mã giáo viên...'}
            className="w-full pl-9 pr-8 py-2 text-xs bg-slate-50 border border-slate-200 rounded-xl focus:bg-white focus:outline-none focus:ring-2 focus:ring-sky-500/20 focus:border-sky-500 text-slate-800 placeholder-slate-400 transition-all"
          />
          {filters.search && (
            <button
              type="button"
              onClick={() => onChange({ search: '' })}
              className="absolute right-2.5 top-1/2 -translate-y-1/2 p-1 text-slate-400 hover:text-slate-600"
              title="Xóa tìm kiếm"
            >
              <X className="w-3.5 h-3.5" />
            </button>
          )}
        </div>

        {/* Department Filter */}
        <div className="lg:col-span-3">
          <select
            value={filters.department}
            onChange={(e) => onChange({ department: e.target.value })}
            className="w-full py-2 px-3 text-xs bg-slate-50 border border-slate-200 rounded-xl focus:bg-white focus:outline-none focus:ring-2 focus:ring-sky-500/20 focus:border-sky-500 text-slate-700 cursor-pointer"
          >
            <option value="">Tất cả khoa / viện</option>
            {DEPARTMENTS.map((dept) => (
              <option key={dept} value={dept}>
                {dept}
              </option>
            ))}
          </select>
        </div>

        {/* Status Dropdown (only when in tab 'all' or specific) */}
        <div className="lg:col-span-2">
          <select
            value={filters.status}
            onChange={(e) => onChange({ status: e.target.value as any })}
            className="w-full py-2 px-3 text-xs bg-slate-50 border border-slate-200 rounded-xl focus:bg-white focus:outline-none focus:ring-2 focus:ring-sky-500/20 focus:border-sky-500 text-slate-700 cursor-pointer"
          >
            <option value="ALL">Tất cả trạng thái</option>
            <option value="Active">Hoạt động</option>
            <option value="Pending">Chờ duyệt</option>
            <option value="Disabled">Đã khóa</option>
            <option value="Rejected">Đã từ chối</option>
          </select>
        </div>

        {/* Sort Selector */}
        <div className="lg:col-span-2 flex items-center gap-2">
          <select
            value={filters.sortBy}
            onChange={(e) => onChange({ sortBy: e.target.value as any })}
            className="w-full py-2 px-3 text-xs bg-slate-50 border border-slate-200 rounded-xl focus:bg-white focus:outline-none focus:ring-2 focus:ring-sky-500/20 focus:border-sky-500 text-slate-700 cursor-pointer"
          >
            <option value="createdAt_desc">Mới nhất trước</option>
            <option value="createdAt_asc">Cũ nhất trước</option>
            <option value="name_asc">Tên (A → Z)</option>
            <option value="name_desc">Tên (Z → A)</option>
          </select>

          {hasActiveFilters && (
            <button
              type="button"
              onClick={onReset}
              className="p-2 text-slate-500 hover:text-slate-800 bg-slate-100 hover:bg-slate-200 rounded-xl transition-colors cursor-pointer shrink-0"
              title="Đặt lại bộ lọc"
            >
              <RotateCcw className="w-4 h-4" />
            </button>
          )}
        </div>
      </div>
    </div>
  );
};
