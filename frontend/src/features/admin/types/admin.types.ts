import type { Role, UserStatus } from '../../auth/types/auth.types';

export interface AdminUserItem {
  id: string;
  fullName: string;
  email: string;
  role: Role;
  status: UserStatus;
  studentId?: string | null;
  teacherId?: string | null;
  department?: string; // Khoa (e.g. Khoa Công nghệ Thông tin)
  phone?: string;
  avatarUrl?: string;
  emailVerifiedAt?: string | null;
  createdAt: string;
  lastLoginAt?: string | null;
  coursesCount?: number;
  classesCount?: number;
  bio?: string;
}

export type UserRoleFilter = 'ALL' | Role;
export type UserStatusFilter = 'ALL' | UserStatus;

export interface UserFilterState {
  search: string;
  role: UserRoleFilter;
  status: UserStatusFilter;
  department: string;
  activeTab: 'all' | 'teachers' | 'students' | 'pending' | 'disabled';
  sortBy: 'createdAt_desc' | 'createdAt_asc' | 'name_asc' | 'name_desc';
}

export interface UserStats {
  totalUsers: number;
  totalTeachers: number;
  totalStudents: number;
  pendingApprovals: number;
  activeUsers: number;
  disabledUsers: number;
}
