import React from 'react';
import { Clock, Lock, XCircle } from 'lucide-react';
import type { UserStatus } from '../../auth/types/auth.types';

interface UserStatusBadgeProps {
  status: UserStatus;
  className?: string;
}

export const UserStatusBadge: React.FC<UserStatusBadgeProps> = ({ status, className = '' }) => {
  switch (status) {
    case 'Active':
      return (
        <span
          className={`inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-[11.5px] font-medium bg-emerald-50 text-emerald-700 border border-emerald-200/80 shadow-2xs ${className}`}
        >
          <span className="w-1.5 h-1.5 rounded-full bg-emerald-500" />
          <span>Hoạt động</span>
        </span>
      );
    case 'Pending':
      return (
        <span
          className={`inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-[11.5px] font-medium bg-amber-50 text-amber-800 border border-amber-200/80 shadow-2xs ${className}`}
        >
          <Clock className="w-3 h-3 text-amber-600 shrink-0" />
          <span>Chờ phê duyệt</span>
        </span>
      );
    case 'Rejected':
      return (
        <span
          className={`inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-[11.5px] font-medium bg-rose-50 text-rose-700 border border-rose-200/80 shadow-2xs ${className}`}
        >
          <XCircle className="w-3 h-3 text-rose-500 shrink-0" />
          <span>Đã từ chối</span>
        </span>
      );
    case 'Disabled':
    default:
      return (
        <span
          className={`inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-[11.5px] font-medium bg-slate-50 text-slate-600 border border-slate-200 shadow-2xs ${className}`}
        >
          <Lock className="w-3 h-3 text-slate-400 shrink-0" />
          <span>Đã khóa</span>
        </span>
      );
  }
};
