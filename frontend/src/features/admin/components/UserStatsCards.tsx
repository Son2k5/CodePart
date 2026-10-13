import React from 'react';
import { Users, UserCheck, GraduationCap, Clock, ArrowUpRight } from 'lucide-react';
import type { UserStats } from '../types/admin.types';

interface UserStatsCardsProps {
  role?: 'Student' | 'Teacher';
  stats: UserStats;
  onFilterTab?: (tab: 'all' | 'teachers' | 'students' | 'pending') => void;
  activeTab?: string;
}

export const UserStatsCards: React.FC<UserStatsCardsProps> = ({
  stats,
  onFilterTab,
  activeTab,
  role,
}) => {
  const cards = [
    {
      id: 'all' as const,
      title: role === 'Student' ? 'Tổng sinh viên' : role === 'Teacher' ? 'Tổng giáo viên' : 'Tổng người dùng',
      value: stats.totalUsers.toLocaleString(),
      subtext: `${stats.activeUsers} đang hoạt động`,
      icon: Users,
      iconBg: 'bg-blue-50 text-blue-600',
      badgeText: '+12% kỳ này',
      badgeClass: 'bg-blue-50 text-blue-700',
      borderColor: 'hover:border-blue-300',
      activeRing: activeTab === 'all' ? 'ring-2 ring-blue-500/20 border-blue-400 bg-blue-50/20' : '',
    },
    {
      id: 'teachers' as const,
      title: 'Hồ sơ giáo viên',
      value: stats.totalTeachers.toLocaleString(),
      subtext: `${stats.pendingApprovals} giảng viên chờ duyệt`,
      icon: UserCheck,
      iconBg: 'bg-indigo-50 text-indigo-600',
      badgeText: 'Giảng viên',
      badgeClass: 'bg-indigo-50 text-indigo-700',
      borderColor: 'hover:border-indigo-300',
      activeRing: activeTab === 'teachers' ? 'ring-2 ring-indigo-500/20 border-indigo-400 bg-indigo-50/20' : '',
    },
    {
      id: 'students' as const,
      title: 'Hồ sơ sinh viên',
      value: stats.totalStudents.toLocaleString(),
      subtext: 'Đã định danh theo mã SV',
      icon: GraduationCap,
      iconBg: 'bg-emerald-50 text-emerald-600',
      badgeText: 'Sinh viên',
      badgeClass: 'bg-emerald-50 text-emerald-700',
      borderColor: 'hover:border-emerald-300',
      activeRing: activeTab === 'students' ? 'ring-2 ring-emerald-500/20 border-emerald-400 bg-emerald-50/20' : '',
    },
    {
      id: 'pending' as const,
      title: 'Chờ phê duyệt',
      value: stats.pendingApprovals.toLocaleString(),
      subtext: stats.pendingApprovals > 0 ? 'Cần xử lý phê duyệt' : 'Đã duyệt toàn bộ',
      icon: Clock,
      iconBg: stats.pendingApprovals > 0 ? 'bg-amber-50 text-amber-600 animate-pulse' : 'bg-slate-100 text-slate-500',
      badgeText: stats.pendingApprovals > 0 ? 'Cần duyệt gấp' : 'Hoàn tất',
      badgeClass: stats.pendingApprovals > 0 ? 'bg-amber-100 text-amber-800' : 'bg-slate-100 text-slate-600',
      borderColor: 'hover:border-amber-300',
      activeRing: activeTab === 'pending' ? 'ring-2 ring-amber-500/20 border-amber-400 bg-amber-50/20' : '',
    },
  ];

  return (
    <div className={`grid grid-cols-1 sm:grid-cols-2 ${role ? '' : 'lg:grid-cols-4'} gap-4`}>
      {cards.filter((card) => !role || !['teachers', 'students'].includes(card.id)).map((card) => {
        const IconComponent = card.icon;
        return (
          <button
            key={card.id}
            type="button"
            onClick={() => onFilterTab?.(card.id)}
            className={`text-left group relative p-5 bg-white rounded-2xl border border-slate-200/90 shadow-xs hover:shadow-md transition-all duration-200 cursor-pointer ${card.borderColor} ${card.activeRing}`}
          >
            <div className="flex items-center justify-between gap-3 mb-3">
              <div className={`w-11 h-11 rounded-xl flex items-center justify-center transition-transform group-hover:scale-105 ${card.iconBg}`}>
                <IconComponent className="w-5 h-5" />
              </div>
              <span className={`text-[11px] font-semibold px-2.5 py-0.5 rounded-full ${card.badgeClass}`}>
                {card.badgeText}
              </span>
            </div>

            <div className="space-y-1">
              <div className="text-xs font-medium text-slate-500">{card.title}</div>
              <div className="text-2xl font-bold text-slate-900 tracking-tight flex items-baseline gap-2">
                {card.value}
                <span className="text-xs text-slate-400 font-normal group-hover:text-sky-600 transition-colors flex items-center">
                  Xem <ArrowUpRight className="w-3 h-3 ml-0.5" />
                </span>
              </div>
              <div className="text-xs text-slate-500 line-clamp-1">{card.subtext}</div>
            </div>
          </button>
        );
      })}
    </div>
  );
};
