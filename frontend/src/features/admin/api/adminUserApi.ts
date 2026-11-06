import { axiosInstance } from '../../../lib/axios';
import type { Role, UserStatus } from '../../auth/types/auth.types';
import type { AuditLogItem, UserProfile } from '../../profile/types';

export interface AdminUpdateUserPayload {
  fullName: string;
  phoneNumber: string | null;
  dateOfBirth: string | null;
  gender: string | null;
  avatarUrl: string | null;
  administrativeClassId: string | null;
  facultyId: string | null;
  academicTitle: string | null;
  version: number;
  role: Role;
  status: UserStatus;
}

interface AuditPage {
  items: AuditLogItem[];
  nextCursor?: string | null;
}

export const adminUserApi = {
  getUser: async (id: string, signal?: AbortSignal): Promise<UserProfile> =>
    (await axiosInstance.get<UserProfile>(`/admin/users/${id}`, { signal })).data,

  updateUser: async (id: string, payload: AdminUpdateUserPayload): Promise<UserProfile> =>
    (await axiosInstance.put<UserProfile>(`/admin/users/${id}`, payload)).data,

  auditLogs: async (id: string, limit = 8, signal?: AbortSignal): Promise<AuditLogItem[]> =>
    (await axiosInstance.get<AuditPage>('/admin/audit-logs', { params: { targetUserId: id, limit }, signal })).data.items,
};
