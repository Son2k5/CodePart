import axios from 'axios';
import { axiosInstance } from '../../../lib/axios';
import type { AdminUserItem } from '../types/admin.types';

export interface TeacherFilter {
  page?: number;
  pageSize?: number;
  search?: string;
  facultyId?: string;
  status?: AdminUserItem['status'];
  sortBy?: 'name_asc' | 'name_desc' | 'created_asc' | 'created_desc';
}
export interface TeacherStats {
  totalTeachers: number; activeTeachers: number; pendingApprovals: number;
  disabledTeachers: number; rejectedTeachers: number;
}
export interface Faculty { id: string; name: string; code: string; teacherCount: number }
export type TeacherFormData = Partial<AdminUserItem> & { temporaryPassword?: string };
export type TeacherAction = 'approve' | 'reject' | 'disable' | 'enable';
type TeacherResponse = Omit<AdminUserItem, 'role' | 'department' | 'phone' | 'bio'> & {
  facultyName?: string | null; phone?: string | null; bio?: string | null;
};
interface TeacherPage { items: TeacherResponse[]; page: number; pageSize: number; totalCount: number; totalPages: number }
const mapTeacher = (t: TeacherResponse): AdminUserItem => ({ ...t, role: 'Teacher',
  department: t.facultyName ?? '', phone: t.phone ?? '', bio: t.bio ?? '' });
const profile = (d: TeacherFormData) => ({ fullName: d.fullName, teacherId: d.teacherId || null,
  facultyId: d.facultyId || null, phone: d.phone || null, bio: d.bio || null });

export const adminTeacherApi = {
  async list(params: TeacherFilter, signal?: AbortSignal) {
    const { data } = await axiosInstance.get<TeacherPage>('/admin/teachers', { params, signal });
    return { ...data, items: data.items.map(mapTeacher) };
  },
  async stats() { return (await axiosInstance.get<TeacherStats>('/admin/teachers/stats')).data; },
  async faculties() { return (await axiosInstance.get<Faculty[]>('/admin/faculties')).data; },
  async detail(id: string, signal?: AbortSignal) {
    return mapTeacher((await axiosInstance.get<TeacherResponse>(`/admin/teachers/${id}`, { signal })).data);
  },
  async create(d: TeacherFormData) {
    return (await axiosInstance.post('/admin/teachers', { ...profile(d), email: d.email,
      temporaryPassword: d.temporaryPassword, status: d.status ?? 'Active' })).data;
  },
  async update(id: string, d: TeacherFormData) { return (await axiosInstance.put(`/admin/teachers/${id}`, profile(d))).data; },
  async action(id: string, action: TeacherAction) { return (await axiosInstance.post(`/admin/users/${id}/${action}`)).data; },
  async bulk(userIds: string[], action: 'approve' | 'disable') {
    return (await axiosInstance.post<number>(`/admin/teachers/bulk-${action}`, { userIds })).data;
  },
  async export(params: TeacherFilter) {
    const { page: _page, pageSize: _pageSize, ...filters } = params;
    const { data } = await axiosInstance.get<Blob>('/admin/teachers/export', { params: filters, responseType: 'blob' });
    const url = URL.createObjectURL(data);
    const link = document.createElement('a');
    link.href = url; link.download = 'giao-vien.csv'; link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  },
};

export function teacherError(error: unknown): string {
  if (axios.isAxiosError(error)) {
    const status = error.response?.status;
    const data = error.response?.data;
    if (data?.errors) return Object.values(data.errors).flat().join(' ');
    if (status && status >= 500) {
      return data?.title || 'Đã có lỗi xảy ra từ máy chủ. Vui lòng thử lại sau.';
    }
    return data?.detail || data?.title || 'Không thể kết nối máy chủ. Vui lòng thử lại.';
  }
  return error instanceof Error ? error.message : 'Thao tác thất bại. Vui lòng thử lại.';
}
