import React from 'react';
import { GraduationCap, ShieldCheck, UserCheck } from 'lucide-react';
import type { Role } from '../../auth/types/auth.types';

interface UserRoleBadgeProps {
  role: Role;
  className?: string;
}

export const UserRoleBadge: React.FC<UserRoleBadgeProps> = ({ role, className = '' }) => {
  switch (role) {
    case 'Admin':
      return (
        <span
          className={`inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-[11.5px] font-medium bg-amber-50/90 text-amber-700 border border-amber-200/80 shadow-2xs ${className}`}
        >
          <ShieldCheck className="w-3.5 h-3.5 text-amber-600 shrink-0" />
          <span>Quản trị viên</span>
        </span>
      );
    case 'Teacher':
      return (
        <span
          className={`inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-[11.5px] font-medium bg-indigo-50/90 text-indigo-700 border border-indigo-200/70 shadow-2xs ${className}`}
        >
          <UserCheck className="w-3.5 h-3.5 text-indigo-600 shrink-0" />
          <span>Giáo viên</span>
        </span>
      );
    case 'Student':
    default:
      return (
        <span
          className={`inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-[11.5px] font-medium bg-sky-50/90 text-sky-700 border border-sky-200/70 shadow-2xs ${className}`}
        >
          <GraduationCap className="w-3.5 h-3.5 text-sky-600 shrink-0" />
          <span>Sinh viên</span>
        </span>
      );
  }
};
