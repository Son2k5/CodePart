import type { Role, UserStatus } from '../auth/types/auth.types';

/** Khớp với UserProfileResponse phía backend. */
export interface UserProfile {
  id: string;
  email: string;
  studentCode: string | null;
  fullName: string;
  phoneNumber: string | null;
  dateOfBirth: string | null; // 'yyyy-MM-dd'
  gender: Gender | null;
  avatarUrl: string | null;
  administrativeClassId: string | null;
  facultyId: string | null;
  academicTitle: string | null;
  role: Role;
  status: UserStatus;
  version: number;
}

export type Gender = 'Male' | 'Female' | 'Other';

export interface OptionItem {
  id: string;
  name: string;
}

export interface ProfileOptions {
  administrativeClasses: OptionItem[];
  faculties: OptionItem[];
}

/** Body cho PUT /users/me — backend dùng thay thế toàn bộ, nên phải gửi đủ field. */
export interface UpdateProfilePayload {
  fullName: string;
  phoneNumber: string | null;
  dateOfBirth: string | null;
  gender: Gender | null;
  avatarUrl: string | null;
  administrativeClassId: string | null;
  facultyId: string | null;
  academicTitle: string | null;
  version: number;
}

export interface ChangePasswordPayload {
  oldPassword: string;
  newPassword: string;
  confirmPassword: string;
}

export interface AuditLogItem {
  id: string;
  actorId: string;
  action: string;
  targetUserId: string;
  occurredAt: string;
  ipAddress: string | null;
  data?: unknown;
}
